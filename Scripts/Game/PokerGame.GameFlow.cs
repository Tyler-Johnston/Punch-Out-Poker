// PokerGame.GameFlow.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	// Game state fields
	private bool isMatchComplete = false;

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

		PlayerAction action = DecideAIAction();
		ExecuteAIAction(action);

		if (action == PlayerAction.Fold || !handInProgress)
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
	
		/// <summary>
	/// Execute the AI's chosen action and update game state
	/// </summary>
	private void ExecuteAIAction(PlayerAction action)
	{
		GD.Print($"[AI ACTION] {currentOpponentName}: {action}");
		
		switch (action)
		{
			case PlayerAction.Fold:
				OnOpponentFold();
				break;
				
			case PlayerAction.Check:
				OnOpponentCheck();
				break;
				
			case PlayerAction.Call:
				OnOpponentCall();
				break;
				
			case PlayerAction.Raise:
				OnOpponentRaise();
				break;
				
			case PlayerAction.AllIn:
				OnOpponentAllIn();
				break;
		}
		
		UpdateHud();
		chipsAudioPlayer?.Play();
	}

	/// <summary>
	/// Handle opponent fold
	/// </summary>
	private void OnOpponentFold()
	{
		ShowMessage($"{currentOpponentName} folds");
		GD.Print($"{currentOpponentName} folds");
		
		int winAmount = pot;
		playerChips += pot;
		pot = 0;
		
		aiOpponent.IsFolded = true;
		handInProgress = false;
		
		// Player wins by fold
		GetTree().CreateTimer(1.5).Timeout += () => {
			ShowMessage($"You win ${winAmount}!");
			EndHand();
		};
	}

	/// <summary>
	/// Handle opponent check
	/// </summary>
	private void OnOpponentCheck()
	{
		ShowMessage($"{currentOpponentName} checks");
		GD.Print($"{currentOpponentName} checks");
	}

	/// <summary>
	/// Handle opponent call
	/// </summary>
	private void OnOpponentCall()
	{
		int toCall = currentBet - opponentBet;
		int callAmount = Math.Min(toCall, opponentChips);
		
		opponentChips -= callAmount;
		aiOpponent.ChipStack = opponentChips;
		AddToPot(false, callAmount);
		opponentBet += callAmount;
		
		if (opponentChips == 0)
		{
			opponentIsAllIn = true;
			aiOpponent.IsAllIn = true;
			ShowMessage($"{currentOpponentName} calls all-in for ${callAmount}");
			GD.Print($"{currentOpponentName} calls all-in: {callAmount}");
		}
		else
		{
			ShowMessage($"{currentOpponentName} calls ${callAmount}");
			GD.Print($"{currentOpponentName} calls: {callAmount}");
		}
	}

	/// <summary>
	/// Handle opponent raise/bet
	/// </summary>
	private void OnOpponentRaise()
	{
		// Create game state for bet size calculation
		GameState gameState = new GameState
		{
			CommunityCards = new List<Card>(communityCards),
			PotSize = pot,
			CurrentBet = currentBet,
			Stage = ConvertStreetToBettingStage(currentStreet),
			BigBlind = bigBlind
		};
		gameState.SetPlayerBet(aiOpponent, opponentBet);
		
		// Calculate bet size
		float handStrength = aiOpponent.DetermineHandStrengthCategory(gameState) switch
		{
			HandStrength.Strong => 0.8f,
			HandStrength.Medium => 0.5f,
			HandStrength.Weak => 0.3f,
			HandStrength.Bluffing => 0.2f,
			_ => 0.5f
		};
		
		int raiseAmount = decisionMaker.CalculateBetSize(aiOpponent, gameState, handStrength);
		
		// Ensure valid raise
		int minRaise = (currentBet - opponentBet) + bigBlind;
		raiseAmount = Math.Max(raiseAmount, minRaise);
		raiseAmount = Math.Min(raiseAmount, opponentChips);
		
		if (raiseAmount >= opponentChips)
		{
			// All-in
			OnOpponentAllIn();
			return;
		}
		
		opponentChips -= raiseAmount;
		aiOpponent.ChipStack = opponentChips;
		AddToPot(false, raiseAmount);
		opponentBet += raiseAmount;
		currentBet = opponentBet;
		
		raisesThisStreet++;
		playerHasActedThisStreet = false; // Player needs to respond
		
		bool isOpening = (currentBet == bigBlind && currentStreet == Street.Preflop) || 
						 (currentBet == 0 && currentStreet != Street.Preflop);
		
		if (isOpening)
		{
			ShowMessage($"{currentOpponentName} bets ${raiseAmount}");
			GD.Print($"{currentOpponentName} bets: {raiseAmount}");
		}
		else
		{
			ShowMessage($"{currentOpponentName} raises to ${currentBet}");
			GD.Print($"{currentOpponentName} raises to: {currentBet}");
		}
		
		// Track if this might be a bluff
		if (handStrength < 0.4f)
		{
			aiBluffedThisHand = true;
		}
	}

	/// <summary>
	/// Handle opponent all-in
	/// </summary>
	private void OnOpponentAllIn()
	{
		int allInAmount = opponentChips;
		
		AddToPot(false, allInAmount);
		opponentBet += allInAmount;
		opponentChips = 0;
		aiOpponent.ChipStack = 0;
		
		opponentIsAllIn = true;
		aiOpponent.IsAllIn = true;
		
		currentBet = Math.Max(currentBet, opponentBet);
		playerHasActedThisStreet = false; // Player needs to respond
		
		ShowMessage($"{currentOpponentName} goes ALL-IN for ${allInAmount}!");
		GD.Print($"{currentOpponentName} ALL-IN: {allInAmount}");
	}

	private async void ShowDown()
	{
		GD.Print("\n=== Showdown ===");
		
		// 1. Process Refunds First
		bool refundOccurred = ReturnUncalledChips();

		if (refundOccurred)
		{
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
		HandResult aiHandResult;
		
		// ✅ SAFE: Store pot amount before any modifications
		int finalPot = pot;

		if (result > 0)
		{
			// Player wins
			GD.Print("\nPLAYER WINS!");
			message = $"You win ${finalPot} with {playerHandName}!";  // ✅ Use finalPot
			
			if (opponentRank <= 2467)
			{
				GD.Print($"{currentOpponentName} suffered a bad beat!");
				aiHandResult = HandResult.BadBeat;
			}
			else if (aiBluffedThisHand && opponentRank > 6185)
			{
				GD.Print($"{currentOpponentName} was bluffing!");
				aiHandResult = HandResult.BluffCaught;
			}
			else
			{
				aiHandResult = HandResult.Loss;
			}
			
			playerChips += pot;
			aiOpponent.ProcessHandResult(aiHandResult);
		}
		else if (result < 0)
		{
			// Opponent wins
			GD.Print("\nOPPONENT WINS!");
			message = $"{currentOpponentName} wins ${finalPot} with {opponentHandName}";  // ✅ Use finalPot
			
			if (aiBluffedThisHand)
			{
				GD.Print($"{currentOpponentName} won with a bluff!");
			}
			else if (opponentRank < 1609)
			{
				GD.Print($"{currentOpponentName} had a strong hand!");
			}
			
			opponentChips += pot;
			aiOpponent.ChipStack = opponentChips;
			aiOpponent.ProcessHandResult(HandResult.Win);
			
			if (playerRank <= 2467)
			{
				GD.Print("You suffered a bad beat!");
			}
		}
		else
		{
			// Split pot
			GD.Print("\nSPLIT POT!");
			int split = pot / 2;
			message = $"Split pot - ${split} each!";
			playerChips += split;
			opponentChips += pot - split;
			aiOpponent.ChipStack = opponentChips;
			
			aiOpponent.ProcessHandResult(HandResult.Neutral);
		}

		ShowMessage(message);
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		GD.Print($"AI Tilt Level: {aiOpponent.Personality.TiltMeter:F1}");
		
		EndHand();  // This might set pot = 0, so we stored it earlier
	}

}
