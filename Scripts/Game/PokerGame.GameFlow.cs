using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokerGame
{
	private async Task DealInitialHands()
	{
		GD.Print("\\n=== Dealing Initial Hands ===");
		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

		playerHand.Add(deck.Deal());
		playerHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());

		// deal cards to AI opponent
		aiOpponent.Hand.Clear();
		foreach (var card in opponentHand)
		{
			aiOpponent.DealCard(card);
		}

		GD.Print($"Player hand: {playerHand[0]}, {playerHand[1]}");
		GD.Print($"Opponent hand: {opponentHand[0]}, {opponentHand[1]}");
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		
		// animate player Card 1
		sfxPlayer.PlaySound("card_flip");
		await playerCard1.RevealCard(playerHand[0]);
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		// animate player Card 2
		sfxPlayer.PlaySound("card_flip");
		await playerCard2.RevealCard(playerHand[1]);
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		// opponent cards stay face down
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
		
		// Show reaction to hole cards
		ShowTell(true);
	}

	public async Task DealCommunityCards(Street street)
	{
		GD.Print($"\\n=== Community Cards: {street} ===");
		switch (street)
		{
			case Street.Flop:
				communityCards.Add(deck.Deal());
				communityCards.Add(deck.Deal());
				communityCards.Add(deck.Deal());
				GD.Print($"Flop: {communityCards[0]}, {communityCards[1]}, {communityCards[2]}");
				ShowMessage("Flop dealt");
				
				sfxPlayer.PlaySound("card_flip");
				await flop1.RevealCard(communityCards[0]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				sfxPlayer.PlaySound("card_flip");
				await flop2.RevealCard(communityCards[1]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				sfxPlayer.PlaySound("card_flip");
				await flop3.RevealCard(communityCards[2]);
				break;
				
			case Street.Turn:
				communityCards.Add(deck.Deal());
				GD.Print($"Turn: {communityCards[3]}");
				ShowMessage("Turn card");
				
				sfxPlayer.PlaySound("card_flip");
				await turnCard.RevealCard(communityCards[3]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				break;
				
			case Street.River:
				communityCards.Add(deck.Deal());
				GD.Print($"River: {communityCards[4]}");
				ShowMessage("River card");
				
				sfxPlayer.PlaySound("card_flip");
				await riverCard.RevealCard(communityCards[4]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				break;
		}
		
		// Show reaction to the board
		ShowTell(true);
	}

	private async void AdvanceStreet()
	{
		ResetBettingRound();
		UpdateOpponentVisuals();
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

		currentStreet = nextStreet;
		
		await DealCommunityCards(nextStreet);

		// all-in scenarios
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

		// post-flop: non-button acts first
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
			
			if (!isPlayerTurn)
			{
				CheckAndProcessAITurn();
			}
		};
	}

	
	private async void ShowDown()
	{
		// Stop tells during showdown
		if (tellTimer != null) tellTimer.Stop();
		
		if (isShowdownInProgress) return;
		isShowdownInProgress = true;
		
		GD.Print("\\n=== Showdown ===");
		
		// process refunds first
		bool refundOccurred = ReturnUncalledChips();

		if (refundOccurred)
		{
			UpdateHud();
			await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
		}

		// reveal opponent hand
		sfxPlayer.PlaySound("card_flip");
		await opponentCard1.RevealCard(opponentHand[0]);
		await ToSignal(GetTree().CreateTimer(0.30f), SceneTreeTimer.SignalName.Timeout);
		sfxPlayer.PlaySound("card_flip");
		await opponentCard2.RevealCard(opponentHand[1]);
		await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
		string opponentHandName = HandEvaluator.GetHandDescription(opponentHand, communityCards);
		
		playerHandType.Text = playerHandName;
		opponentHandType.Text = opponentHandName;

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);
		string message;
		HandResult aiHandResult;
		int finalPot = pot;
		
		if (result > 0)
		{
			GD.Print("\\nPLAYER WINS!");
			message = $"You win ${finalPot} with {playerHandName}!";
			
			TriggerOpponentDialogue("OnLosePot");
			bool isBadBeat = (aiStrengthAtAllIn > 0.70f); 
			bool isCooler = (opponentRank <= 1609); 
			
			if (isBadBeat)
			{
				GD.Print($"{currentOpponentName} suffered a BAD BEAT! (Strength was {aiStrengthAtAllIn:F2})");
				aiHandResult = HandResult.BadBeat;
				SetExpression(Expression.Angry);
			}
			else if (isCooler)
			{
				GD.Print($"{currentOpponentName} suffered a COOLER!");
				aiHandResult = HandResult.BadBeat;
				SetExpression(Expression.Sad);
			}
			else if (aiBluffedThisHand && opponentRank > 6185)
			{
				GD.Print($"{currentOpponentName} was bluffing!");
				aiHandResult = HandResult.BluffCaught;
				SetExpression(Expression.Surprised); // Caught bluffing
			}
			else
			{
				aiHandResult = HandResult.Loss; 
				SetExpression(Expression.Sad);
			}
			
			playerChips += pot;
			aiOpponent.ProcessHandResult(aiHandResult, finalPot, bigBlind);
		}
		else if (result < 0)
		{
			GD.Print("\\nOPPONENT WINS!");
			message = $"{currentOpponentName} wins ${finalPot} with {opponentHandName}";
			
			TriggerOpponentDialogue("OnWinPot");
			if (aiBluffedThisHand)
			{
				GD.Print($"{currentOpponentName} won with a bluff!");
				SetExpression(Expression.Smirk);
			}
			else if (opponentRank < 1609)
			{
				GD.Print($"{currentOpponentName} had a strong hand!");
				SetExpression(Expression.Happy);
			}
			else
			{
				SetExpression(Expression.Happy);
			}
			
			opponentChips += pot;
			aiOpponent.ChipStack = opponentChips;
			aiOpponent.ProcessHandResult(HandResult.Win, pot, bigBlind);
			
			if (playerRank <= 2467)
			{
				GD.Print("You suffered a bad beat!");
			}
		}
		else
		{
			GD.Print("\\nSPLIT POT!");
			int split = pot / 2;
			message = $"Split pot - ${split} each!";
			playerChips += split;
			opponentChips += pot - split;
			aiOpponent.ChipStack = opponentChips;
			
			aiOpponent.ProcessHandResult(HandResult.Neutral, pot, bigBlind);
			SetExpression(Expression.Neutral);
		}
		pot = 0;
		ShowMessage(message);
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		GD.Print($"AI Tilt Level: {aiOpponent.Personality.TiltMeter:F1}");
		
		isShowdownInProgress = false;
		UpdateHud(); 
		EndHand();
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
		
		// --- VISUAL FEEDBACK: Set Expression based on Action ---
		UpdateOpponentExpression(action);
		if (tellTimer != null) tellTimer.Start(); // Reset idle tell timer after acting
		
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
		UpdateOpponentVisuals();
	}

	private void UpdateOpponentExpression(PlayerAction action)
	{
		// 1. Heavy Tilt Overrides (Angry/Monkey)
		if (aiOpponent.CurrentTiltState == TiltState.Steaming || aiOpponent.CurrentTiltState == TiltState.Monkey)
		{
			SetExpression(Expression.Angry);
			return;
		}

		// 2. Action-based Expressions
		switch (action)
		{
			case PlayerAction.Fold:
				SetExpression(Expression.Sad);
				break;
			case PlayerAction.Check:
				SetExpression(Expression.Neutral);
				break;
			case PlayerAction.Call:
				SetExpression(Expression.Neutral); 
				break;
			case PlayerAction.Raise:
				SetExpression(Expression.Neutral); 
				break;
			case PlayerAction.AllIn:
				SetExpression(Expression.Smirk);
				break;
		}

		//// 3. Bluff Override
		//// If they raised and it was flagged as a bluff
		//if (aiBluffedThisHand && action == PlayerAction.Raise)
		//{
			//SetExpression(Expression.Smirk);
		//}
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
			
			GameState snapState = new GameState 
			{ 
				CommunityCards = new List<Card>(communityCards),
				Street = currentStreet
			};
			aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(snapState);
			
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
		
		// track if this might be a bluff
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
		
		GameState snapState = new GameState 
		{ 
			CommunityCards = new List<Card>(communityCards),
			Street = currentStreet
		};
		aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(snapState);
		
		ShowMessage($"{currentOpponentName} goes ALL-IN for ${allInAmount}!");
		GD.Print($"{currentOpponentName} ALL-IN: {allInAmount}");
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
	
	private bool DetermineAIPosition()
	{
		if (currentStreet == Street.Preflop)
		{
			return playerHasButton;
		}
		else
		{
			return !playerHasButton;
		}
	}
	
	/// <summary>
	/// Handle game over state
	/// </summary>
	private void HandleGameOver(bool opponentSurrendered = false)
	{
		// Stop tells
		if (tellTimer != null) tellTimer.Stop();

		bool playerWon = opponentSurrendered || (opponentChips <= 0);
		
		if (playerWon)
		{
			int winnings = buyInAmount * 2;
			GameManager.Instance.OnMatchWon(currentOpponentName, winnings);
			
			string reason = opponentSurrendered ? "surrendered!" : "went bust!";
			ShowMessage($"VICTORY! {currentOpponentName} {reason}");
			GD.Print($"=== VICTORY vs {currentOpponentName} ===");
		}
		else
		{
			GameManager.Instance.OnMatchLost(currentOpponentName);
			ShowMessage($"{currentOpponentName} wins!");
			GD.Print($"=== DEFEAT vs {currentOpponentName} ===");
		}
		
		// return to menu after delay
		GetTree().CreateTimer(6.0).Timeout += () => 
		{
			GetTree().ChangeSceneToFile("res://Scenes/CharacterSelect.tscn");
		};
	}
}
