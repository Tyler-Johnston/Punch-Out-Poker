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
	private bool isProcessingAIAction = false; // Prevent re-entry

	private bool IsGameOver()
	{
		return playerChips < smallBlind || opponentChips < bigBlind;
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
		}

		handInProgress = false;
		waitingForNextGame = true;
		checkCallButton.Text = "GAME OVER";
		checkCallButton.Disabled = true;
		foldButton.Visible = false;
		betRaiseButton.Visible = false;
		UpdateHud();
	}

	private void StartNewHand()
	{
		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}

		GD.Print("\n=== New Hand ===");
		ShowMessage("New hand starting...");

		betSlider.Visible = true;
		foldButton.Visible = true;
		betRaiseButton.Visible = true;
		betSliderLabel.Visible = true;
		potLabel.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";

		deck = new Deck();
		deck.Shuffle();
		deckDealAudioPlayer.Play();

		pot = 0;
		aiBluffedThisHand = false;
		playerTotalBetsThisHand = 0;
		raisesThisStreet = 0;
		playerIsAllIn = false;
		opponentIsAllIn = false;
		isProcessingAIAction = false; // Reset flag

		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;

		playerBetOnStreet.Clear();
		playerBetSizeOnStreet.Clear();
		playerBetOnStreet[Street.Preflop] = false;
		playerBetOnStreet[Street.Flop] = false;
		playerBetOnStreet[Street.Turn] = false;
		playerBetOnStreet[Street.River] = false;

		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

		playerCard1.ShowBack();
		playerCard2.ShowBack();
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
		flop1.ShowBack();
		flop2.ShowBack();
		flop3.ShowBack();
		turnCard.ShowBack();
		riverCard.ShowBack();

		DealInitialHands();
		currentStreet = Street.Preflop;
		handInProgress = true;
		waitingForNextGame = false;

		playerHasButton = !playerHasButton;

		if (playerHasButton)
		{
			playerChips -= smallBlind;
			opponentChips -= bigBlind;
			pot += smallBlind + bigBlind;
			playerBet = smallBlind;
			opponentBet = bigBlind;
			currentBet = bigBlind;

			isPlayerTurn = true;
			ShowMessage($"Blinds: You {smallBlind}, Opponent {bigBlind}");
			GD.Print($"Player has button - Blinds posted: Player {smallBlind}, Opponent {bigBlind}, Pot: {pot}");
		}
		else
		{
			playerChips -= bigBlind;
			opponentChips -= smallBlind;
			pot += smallBlind + bigBlind;
			playerBet = bigBlind;
			opponentBet = smallBlind;
			currentBet = bigBlind;

			isPlayerTurn = false;
			ShowMessage($"Blinds: You {bigBlind}, Opponent {smallBlind}");
			GD.Print($"Opponent has button - Blinds posted: Player {bigBlind}, Opponent {smallBlind}, Pot: {pot}");
		}

		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
		
		// Single timer for AI turn
		if (!isPlayerTurn)
		{
			GetTree().CreateTimer(2.0).Timeout += () => CheckAndProcessAITurn();
		}
	}

	private void AdvanceStreet()
	{
		ResetBettingRound();
		aiBluffedThisHand = false;
		isProcessingAIAction = false; // Reset for new street

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

		// Deal and reveal cards with message
		DealCommunityCards(nextStreet);
		RevealCommunityCards(nextStreet);
		currentStreet = nextStreet;

		// All-in scenarios
		if (playerIsAllIn && opponentIsAllIn)
		{
			GetTree().CreateTimer(2.0).Timeout += () => {
				GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			};
			return;
		}

		if (playerIsAllIn || opponentIsAllIn)
		{
			GetTree().CreateTimer(2.0).Timeout += () => {
				GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			};
			return;
		}

		// Post-flop: non-button acts first
		isPlayerTurn = !playerHasButton;
		
		double waitTime = 0;
		if (!isPlayerTurn)
		{
			waitTime = 2;
		}

		// Wait 2 seconds, then continue
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
			GD.Print("⚠️ CheckAndProcessAITurn blocked: already processing");
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
			GD.Print("⚠️ ProcessOpponentTurn blocked: already processing");
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
			isProcessingAIAction = false; // Unlock before advancing
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
			// Back to player
			isProcessingAIAction = false;
			isPlayerTurn = true;
			UpdateHud();
			UpdateButtonLabels();
			RefreshBetSlider();
		}
	}

	private void ShowDown()
	{
		GD.Print("\n=== Showdown ===");
		opponentCard1.ShowCard(opponentHand[0]);
		opponentCard2.ShowCard(opponentHand[1]);

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandName(playerRank);
		string opponentHandName = HandEvaluator.GetHandName(opponentRank);

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

		pot = 0;
		handInProgress = false;
		waitingForNextGame = true;

		if (IsGameOver())
		{
			HandleGameOver();
		}
		else
		{
			UpdateHud();
			RefreshBetSlider();
		}
	}
}
