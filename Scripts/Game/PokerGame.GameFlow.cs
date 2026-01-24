using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokerGame
{
	// Game state fields
	private bool isMatchComplete = false;

	private async void AdvanceStreet()
	{
		ResetBettingRound();
		aiBluffedThisHand = false;
		isProcessingAIAction = false;
		
		// Ensure visuals are fresh for new street
		UpdateOpponentVisuals();

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

		await DealCommunityCards(nextStreet);
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
		UpdateOpponentVisuals(); // Update visuals (shake if steaming, etc)
	}

	/// <summary>
	/// Handle opponent fold
	/// </summary>
	private void OnOpponentFold()
	{
		ShowMessage($"{currentOpponentName} folds");
		GD.Print($"{currentOpponentName} folds");
		
		float betRatio = (pot > 0) ? (float)currentBet / pot : 0;
		aiOpponent.OnFolded(betRatio);

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
		sfxPlayer.PlaySound("check", true);
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
			
			// --- SNAPSHOT CAPTURE FOR BAD BEAT ---
			GameState snapState = new GameState 
			{ 
				CommunityCards = new List<Card>(communityCards),
				Street = currentStreet
			};
			aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(snapState);
			GD.Print($"[SNAPSHOT] AI Called All-In. Strength: {aiStrengthAtAllIn:F2}");
			// ------------------------------------
			
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
			Street = currentStreet,
			BigBlind = bigBlind,
		 	IsAIInPosition = DetermineAIPosition()
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
			OnOpponentAllIn();
			return;
		}
		
		opponentChips -= raiseAmount;
		aiOpponent.ChipStack = opponentChips;
		AddToPot(false, raiseAmount);
		opponentBet += raiseAmount;
		currentBet = opponentBet;
		
		raisesThisStreet++;
		playerHasActedThisStreet = false;
		
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
		playerHasActedThisStreet = false;
		
		// --- SNAPSHOT CAPTURE FOR BAD BEAT ---
		GameState snapState = new GameState 
		{ 
			CommunityCards = new List<Card>(communityCards),
			Street = currentStreet
		};
		aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(snapState);
		GD.Print($"[SNAPSHOT] AI Pushed All-In. Strength: {aiStrengthAtAllIn:F2}");
		// ------------------------------------
		
		ShowMessage($"{currentOpponentName} goes ALL-IN for ${allInAmount}!");
		GD.Print($"{currentOpponentName} ALL-IN: {allInAmount}");
	}
	
	public bool IsTrueBadBeat(float equityWhenMoneyWentIn, bool aiLostHand, float aiFinalHandStrength)
	{
		// Must have been ahead when committing chips
		if (equityWhenMoneyWentIn < 0.65f) return false;
		
		// Must have lost
		if (!aiLostHand) return false;
		
		// Must have ended with a legitimately strong hand (got outdrawn, not just whiffed)
		if (aiFinalHandStrength < 0.65f) return false;
		
		return true;
	}

	
	/// <summary>
	/// AI decision making using personality + dialogue system
	/// </summary>
	private PlayerAction DecideAIAction()
	{
		// Create game state for AI decision maker
		GameState gameState = new GameState
		{
			CommunityCards = new List<Card>(communityCards),
			PotSize = pot,
			CurrentBet = currentBet,
			Street = currentStreet,
			BigBlind = bigBlind,
			IsAIInPosition = DetermineAIPosition()
		};
		gameState.SetPlayerBet(aiOpponent, opponentBet);
		
		// 1) Get AI decision (Fold/Check/Call/Raise/AllIn)
		PlayerAction action = aiOpponent.MakeDecision(gameState);
		
		// 2) Evaluate hand strength category for tells / dialogue
		HandStrength strength = aiOpponent.DetermineHandStrengthCategory(gameState);
		
		// 3) Get a spoken dialogue line (what appears in the label)
		string dialogueLine = aiOpponent.GetDialogueForAction(
			action,
			strength,
			aiBluffedThisHand 
		);
		
		// 4) Apply chattiness: chance that they say nothing
		float chatRoll = GD.Randf();
		bool alwaysTalk = (aiOpponent.CurrentTiltState >= TiltState.Steaming);
		
		if ((chatRoll <= aiOpponent.Personality.Chattiness || alwaysTalk) && !string.IsNullOrEmpty(dialogueLine))
		{
			opponentDialogueLabel.Text = dialogueLine;
		}
		else
		{
			opponentDialogueLabel.Text = "";
		}
		
		return action;
	}

}
