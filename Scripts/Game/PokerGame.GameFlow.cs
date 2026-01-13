// PokerGame.GameFlow.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	// Game state fields
	private bool handInProgress = false;
	private bool waitingForNextGame = false;
	private bool isPlayerTurn = true;
	private bool playerIsAllIn = false;
	private bool opponentIsAllIn = false;
	private bool playerHasButton = false;
	private bool isProcessingAIAction = false;
	private bool isMatchComplete = false;

	private bool IsGameOver()
	{
		return playerChips <= 0 || opponentChips <= 0;
	}

	private void HandleGameOver()
	{
		if (playerChips < smallBlind)
		{
			ShowMessage("GAME OVER - You ran out of chips!");
			GD.Print("GAME OVER - Player has no chips");
		}
		else if (opponentChips < bigBlind)
		{
			ShowMessage("YOU WIN - Opponent ran out of chips!");
			GD.Print("GAME OVER - Opponent has no chips");
			GameManager.Instance.PlayerMoney += playerChips;
			GameManager.Instance.OnMatchWon(GameManager.Instance.SelectedOpponent);
		}

		handInProgress = false;
		waitingForNextGame = true;
		isMatchComplete = true;
		UpdateHud();
	}

	private void AdvanceStreet()
	{
		ResetBettingRound();
		aiBluffedThisHand = false;
		isProcessingAIAction = false;

		Street nextStreet;
		
		switch (currentStreet)
		{
			case Street.Preflop:
				nextStreet = Street.Flop;
				break;
			case Street.Flop:
				nextStreet = Street.Turn;
				break;
			case Street.Turn:
				nextStreet = Street.River;
				break;
			case Street.River:
				ShowDown();
				return;
			default:
				return;
		}

		DealCommunityCards(nextStreet);
		RevealCommunityCards(nextStreet);
		currentStreet = nextStreet;

		// All-in scenarios
		if (playerIsAllIn && opponentIsAllIn)
		{
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			return;
		}

		if (playerIsAllIn || opponentIsAllIn)
		{
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			return;
		}

		// Post-flop: non-button acts first
		isPlayerTurn = !playerHasButton;
		
		double waitTime = 0;
		if (!isPlayerTurn)
		{
			waitTime = 1.15;
		}

		GetTree().CreateTimer(waitTime).Timeout += () => {
			UpdateHud();
			UpdateButtonLabels();
			RefreshBetSlider();
			
			// Only call AI if it's their turn
			if (!isPlayerTurn)
			{
				CheckAndProcessAITurn();
			}
		};
	}

	private void CheckAndProcessAITurn()
	{
		GD.Print($"[CheckAndProcessAITurn] isProcessing={isProcessingAIAction}, isPlayerTurn={isPlayerTurn}, handInProgress={handInProgress}");
		
		if (isProcessingAIAction)
		{
			GD.Print("CheckAndProcessAITurn blocked: already processing");
			return;
		}
		
		if (!isPlayerTurn && handInProgress && !waitingForNextGame)
		{
			float baseThinkTime = 0.5f;
			GetTree().CreateTimer(baseThinkTime).Timeout += () => ProcessOpponentTurn();
		}
	}

	private void ProcessOpponentTurn()
	{
		GD.Print($"[ProcessOpponentTurn] isProcessing={isProcessingAIAction}, isPlayerTurn={isPlayerTurn}");
		
		if (isProcessingAIAction)
		{
			GD.Print("ProcessOpponentTurn blocked: already processing");
			return;
		}
		
		if (isPlayerTurn || !handInProgress || waitingForNextGame)
		{
			GD.Print("ProcessOpponentTurn aborted: invalid state");
			return;
		}

		// Lock to prevent re-entry
		isProcessingAIAction = true;
		opponentHasActedThisStreet = true;

		AIAction action = DecideAIAction();
		ExecuteAIAction(action);

		if (action == AIAction.Fold || !handInProgress)
		{
			isProcessingAIAction = false;
			return;
		}

		bool betsEqual = (playerBet == opponentBet);
		bool bothActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;

		GD.Print($"After AI action: betsEqual={betsEqual}, bothActed={bothActed}, bothAllIn={bothAllIn}");

		if ((betsEqual && bothActed) || bothAllIn || (playerIsAllIn && betsEqual))
		{
			GD.Print("Betting round complete after AI action");
			isProcessingAIAction = false;
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (playerIsAllIn && !betsEqual)
		{
			GD.Print("Betting round complete: Player all-in, cannot match AI raise");
			isProcessingAIAction = false;
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else
		{
			isProcessingAIAction = false;
			isPlayerTurn = true;
			UpdateHud();
			UpdateButtonLabels();
			RefreshBetSlider();
		}
	}

	private async void ShowDown()
	{
		GD.Print("\n=== Showdown ===");
		
		// 1. Process Refunds First
		bool refundOccurred = ReturnUncalledChips();

		if (refundOccurred)
		{
			// Wait to read the "Returned X chips" message
			await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
		}

		opponentCard1.ShowCard(opponentHand[0]);
		opponentCard2.ShowCard(opponentHand[1]);

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
		string opponentHandName = HandEvaluator.GetHandDescription(opponentHand, communityCards);

		playerHandType.Text = playerHandName;
		opponentHandType.Text = opponentHandName;

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);
		string message;

		if (result > 0)
		{
			GD.Print("\nPLAYER WINS!");
			message = $"You win with {playerHandName}!";
			if (aiBluffedThisHand && opponentRank > 6185)
				GD.Print("Opponent was bluffing!");
			playerChips += pot;
		}
		else if (result < 0)
		{
			GD.Print("\nOPPONENT WINS!");
			message = $"Opponent wins with {opponentHandName}";
			if (aiBluffedThisHand)
				GD.Print("Opponent was bluffing with weak hand!");
			else if (opponentRank < 1609)
				GD.Print("Opponent had a strong hand!");
			opponentChips += pot;
		}
		else
		{
			GD.Print("\nSPLIT POT!");
			message = "Split pot!";
			int split = pot / 2;
			playerChips += split;
			opponentChips += pot - split;
		}

		ShowMessage(message);
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		EndHand();
	}
}
