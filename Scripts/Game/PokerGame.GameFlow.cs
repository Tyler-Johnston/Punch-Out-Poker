using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokerGame
{
	// --- GAME LIFECYCLE ---
	
	private async void StartNewHand()
	{
		if (!waitingForNextGame && handInProgress) return; 
		
		waitingForNextGame = false;
		isMatchComplete = false;
		SetExpression(Expression.Neutral);

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}
		
		// reset AI opponent for new hand
		aiOpponent.ResetForNewHand();
		aiOpponent.ChipStack = opponentChips;

		GD.Print("\n=== New Hand ===");
		ShowMessage("");

		playerStackLabel.Visible = true;
		actionButtons.Visible = true;
		betweenHandsUI.Visible = false;
		potArea.Visible = true;
		opponentCard1.Visible = false;
		opponentCard2.Visible = false;
		speechBubble.Visible = false;
		sliderUI.Visible = true;
		foldButton.Visible = true;
		foldButton.Disabled = true;
		betRaiseButton.Visible = true;
		betRaiseButton.Disabled = true;
		aiStrengthAtAllIn = 0f;

		deck = new Deck();
		deck.Shuffle();
		
		pot = 0;
		displayPot = 0;
		_lastDisplayedPot = -1;
		_lastPotLabel = -1; 
		playerContributed = 0;
		opponentContributed = 0;
		playerTotalBetsThisHand = 0;
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;  // Reset this too for safety
		
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		
		aiBluffedThisHand = false;
		playerIsAllIn = false;
		opponentIsAllIn = false;
		isProcessingAIAction = false; 

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

		await DealInitialHands();
		
		tellTimer.Start();
		
		currentStreet = Street.Preflop;
		handInProgress = true;
		
		UpdateOpponentVisuals();
		await PostBlinds();
		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
		
		// if both are all-in (or player is forced all-in), skip betting logic
		if (playerIsAllIn || opponentIsAllIn)
		{
			GD.Print("[START HAND] Blind forced All-In! Skipping to next street.");
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
		}
		else if (!isPlayerTurn)
		{
			GetTree().CreateTimer(1.15).Timeout += () => CheckAndProcessAITurn();
		}
	}
	
	private async Task PostBlinds()
	{
		playerHasButton = !playerHasButton;
		
		// Disable AI processing during blind posting
		bool wasProcessing = isProcessingAIAction;
		isProcessingAIAction = true;
		
		foldButton.Disabled = true;
		checkCallButton.Disabled = true;
		betRaiseButton.Disabled = true;
		
		if (playerHasButton)
		{
			// SMALL BLIND: Human player
			int sbAmount = Math.Min(smallBlind, playerChips); 
			playerChips -= sbAmount;
			playerBet = sbAmount;
			playerChipsInPot = sbAmount;
			if (playerChips == 0) playerIsAllIn = true;
			
			ShowMessage($"You post the ${sbAmount} small blind");
			GD.Print($"Player posts SB: {sbAmount}");
			sfxPlayer.PlayRandomChip();
			
			AddToPot(true, sbAmount);
			UpdateHud(true);
			
			// Wait before big blind
			await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
			
			// BIG BLIND: Opponent
			int bbAmount = Math.Min(bigBlind, opponentChips); 
			opponentChips -= bbAmount;
			aiOpponent.ChipStack = opponentChips;
			opponentBet = bbAmount;
			opponentChipsInPot = bbAmount;
			currentBet = opponentBet;
			if (opponentChips == 0) opponentIsAllIn = true;
			
			ShowMessage($"{currentOpponentName} posts the ${bbAmount} big blind");
			GD.Print($"Opponent posts BB: {bbAmount}");
			
			// Add chips to pot immediately after BB
			AddToPot(false, bbAmount);
			UpdateHud(true);
			sfxPlayer.PlayRandomChip();
			
			isPlayerTurn = true;
			GD.Print($"Blinds posted: SB={sbAmount}, BB={bbAmount}. Pot: {pot}");
		}
		else
		{
			// SMALL BLIND: Opponent
			int sbAmount = Math.Min(smallBlind, opponentChips); 
			opponentChips -= sbAmount;
			aiOpponent.ChipStack = opponentChips;
			opponentBet = sbAmount;
			opponentChipsInPot = sbAmount;
			if (opponentChips == 0) opponentIsAllIn = true;
			
			ShowMessage($"{currentOpponentName} posts the ${sbAmount} small blind");
			GD.Print($"Opponent posts SB: {sbAmount}");
			
			// Add chips to pot immediately after SB
			AddToPot(false, sbAmount);
			UpdateHud(true);
			sfxPlayer.PlayRandomChip();
			
			// Wait before big blind
			await ToSignal(GetTree().CreateTimer(2.5f), SceneTreeTimer.SignalName.Timeout);
			
			// BIG BLIND: Human player
			int bbAmount = Math.Min(bigBlind, playerChips); 
			playerChips -= bbAmount;
			playerBet = bbAmount;
			playerChipsInPot = bbAmount;
			if (playerChips == 0) playerIsAllIn = true;
			
			ShowMessage($"You post the ${bbAmount} big blind");
			GD.Print($"Player posts BB: {bbAmount}");
			
			// Add chips to pot immediately after BB
			AddToPot(true, bbAmount);
			currentBet = Math.Max(playerBet, opponentBet);
			UpdateHud(true);
			sfxPlayer.PlayRandomChip();
			
			isPlayerTurn = false;
			GD.Print($"Blinds posted: SB={sbAmount}, BB={bbAmount}. Pot: {pot}");
		}
		
		// Re-enable AI processing after blinds complete
		isProcessingAIAction = wasProcessing;
	}



	private async void EndHand()
	{
		tellTimer.Stop();
		
		if (isShowdownInProgress) return;
		
		pot = 0;
		displayPot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		handInProgress = false;
		waitingForNextGame = true;

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}
		
		// check for rage quit or surrender
		OpponentExitType exitType = aiOpponent.CheckForEarlyExit();
		if (exitType != OpponentExitType.None)
		{
			await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
			if (exitType == OpponentExitType.RageQuit)
			{
				ShowMessage($"{aiOpponent.PlayerName} RAGE QUITS!");
				SetExpression(Expression.Angry);
				GD.Print($"[GAME OVER] Opponent Rage Quit! Tilt: {aiOpponent.Personality.TiltMeter}");
			}
			else if (exitType == OpponentExitType.Surrender)
			{
				ShowMessage($"{aiOpponent.PlayerName} SURRENDERS!");
				SetExpression(Expression.Worried);
				GD.Print($"[GAME OVER] Opponent Surrendered. Chips: {aiOpponent.ChipStack}");
			}
			await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);
			HandleGameOver(opponentSurrendered: true);
			return;
		}

		UpdateHud();
		RefreshBetSlider();
	}
	
	private void HandleGameOver(bool opponentSurrendered = false)
	{
		if (tellTimer != null) tellTimer.Stop();
		isMatchComplete = true;
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
		UpdateHud();
	}

	// --- CARD DEALING ---
	
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
		
		// animate player card 1
		sfxPlayer.PlaySound("card_flip");
		await playerCard1.RevealCard(playerHand[0]);
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		// animate player card 2
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
		GD.Print($"\n=== Community Cards: {street} ===");
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
				await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);
				sfxPlayer.PlaySound("card_flip");
				await flop2.RevealCard(communityCards[1]);
				await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);
				sfxPlayer.PlaySound("card_flip");
				await flop3.RevealCard(communityCards[2]);
				break;
				
			case Street.Turn:
				communityCards.Add(deck.Deal());
				GD.Print($"Turn: {communityCards[3]}");
				ShowMessage("Turn card");
				
				sfxPlayer.PlaySound("card_flip");
				await turnCard.RevealCard(communityCards[3]);
				await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);
				break;
				
			case Street.River:
				communityCards.Add(deck.Deal());
				GD.Print($"River: {communityCards[4]}");
				ShowMessage("River card");
				
				sfxPlayer.PlaySound("card_flip");
				await riverCard.RevealCard(communityCards[4]);
				await ToSignal(GetTree().CreateTimer(0.75f), SceneTreeTimer.SignalName.Timeout);
				break;
		}
		ShowTell(true);
	}

	// --- STREET PROGRESSION ---

	private async void AdvanceStreet()
	{		
		await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);
		ResetBettingRound();
		await ToSignal(GetTree().CreateTimer(1.2f), SceneTreeTimer.SignalName.Timeout);
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
		// stop tells during showdown
		if (tellTimer != null) tellTimer.Stop();
		
		if (isShowdownInProgress) return;
		isShowdownInProgress = true;
		
		pot += playerChipsInPot + opponentChipsInPot;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		GD.Print($"[Showdown] Combined all chips into pot: {pot}");
		
		GD.Print("\\n=== Showdown ===");
		
		// process refunds first
		bool refundOccurred = ReturnUncalledChips();

		if (refundOccurred)
		{
			UpdateHud();
			ShowMessage("Returned Uncalled Chips");
			await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
		}

		// Toss cards from top of screen
		await TossCard(opponentCard1, opponentHand[0]);
		await ToSignal(GetTree().CreateTimer(0.30f), SceneTreeTimer.SignalName.Timeout);

		await TossCard(opponentCard2, opponentHand[1]);
		await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
		string opponentHandName = HandEvaluator.GetHandDescription(opponentHand, communityCards);

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);
		string message;
		HandResult aiHandResult;
		int finalPot = pot;
		
		if (result > 0)
		{
			GD.Print("\\nPLAYER WINS!");
			message = $"You win ${finalPot} with {playerHandName}!";
			
			PlayReactionDialogue("OnLosePot");

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
				SetExpression(Expression.Surprised);
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
			
			PlayReactionDialogue("OnWinPot");

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
		displayPot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		ShowMessage(message);
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		GD.Print($"AI Tilt Level: {aiOpponent.Personality.TiltMeter:F1}");
		
		isShowdownInProgress = false;
		UpdateHud(); 
		EndHand();
	}

	// --- AI TURN PROCESSING ---

	private void CheckAndProcessAITurn()
	{
		GD.Print($"[CheckAndProcessAITurn] isProcessing={isProcessingAIAction}, isPlayerTurn={isPlayerTurn}, handInProgress={handInProgress}");
		
		if (IsAIDebugDisabled())
		{
			GD.Print("[DEBUG] AI turn skipped (AI manually disabled)");
			return;
		}
	
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

	private async void ProcessOpponentTurn()
	{
		GD.Print($"[ProcessOpponentTurn] isProcessing={isProcessingAIAction}, isPlayerTurn={isPlayerTurn}");
		
		if (isProcessingAIAction) return;
		if (isPlayerTurn || !handInProgress || waitingForNextGame) return;

		// lock to prevent re-entry
		isProcessingAIAction = true;
		opponentHasActedThisStreet = true;

		GameState gameState = CreateGameState();
		PlayerAction action = DecideAIAction(gameState);

		float waitTime = PlayActionDialogue(action, gameState);
		if (waitTime > 0)
		{
			await ToSignal(GetTree().CreateTimer(waitTime + 1.0f), SceneTreeTimer.SignalName.Timeout);
		}
		
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

	private void ExecuteAIAction(PlayerAction action)
	{
		GD.Print($"[AI ACTION] {currentOpponentName}: {action}");
		
		UpdateOpponentExpression(action);
		if (tellTimer != null) tellTimer.Start();
		
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

	// --- AI ACTION HANDLERS ---

	private async void OnOpponentFold()
	{
		ShowMessage($"{currentOpponentName} folds");
		GD.Print($"{currentOpponentName} folds");
		
		Task card1Task = TossCard(opponentCard1, opponentHand[0], 2.5f, 1.5f, false);
		Task card2Task = TossCard(opponentCard2, opponentHand[1], 1.5f, 1.5f, false);
		
		await Task.WhenAll(card1Task, card2Task);
		await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
			
		float betRatio = (pot > 0) ? (float)currentBet / pot : 0;
		aiOpponent.OnFolded(betRatio);

		// Combine all chips before awarding
		int winAmount = pot;
		playerChips += winAmount;
		pot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		
		aiOpponent.IsFolded = true;
		handInProgress = false;
		
		// Player wins by fold
		GetTree().CreateTimer(1.5).Timeout += () => {
			ShowMessage($"You win ${winAmount}!");
			EndHand();
		};
	}


	private void OnOpponentCheck()
	{
		sfxPlayer.PlaySound("check", true);
		ShowMessage($"{currentOpponentName} checks");
		GD.Print($"{currentOpponentName} checks");
	}

	private void OnOpponentCall()
	{
		int toCall = currentBet - opponentBet;
		int callAmount = Math.Min(toCall, opponentChips);
		
		opponentChips -= callAmount;
		aiOpponent.ChipStack = opponentChips;
		AddToPot(false, callAmount);
		opponentBet += callAmount;
		opponentChipsInPot += callAmount;  // Track in current betting round
		
		if (opponentChips == 0)
		{
			opponentIsAllIn = true;
			aiOpponent.IsAllIn = true;
			
			GameState gameState = CreateGameState();
			aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(gameState);
			
			ShowMessage($"{currentOpponentName} calls all-in for ${callAmount}");
			GD.Print($"{currentOpponentName} calls all-in: {callAmount}");
		}
		else
		{
			ShowMessage($"{currentOpponentName} calls ${callAmount}");
			GD.Print($"{currentOpponentName} calls: {callAmount}");
		}
		sfxPlayer.PlayRandomChip();
	}

	private void OnOpponentRaise()
	{
		GameState gameState = CreateGameState();
		
		// calculate bet size
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
		opponentChipsInPot += raiseAmount;  // Track in current betting round
		currentBet = opponentBet;
		
		playerHasActedThisStreet = false;
		
		bool isOpening = (currentBet == bigBlind && currentStreet == Street.Preflop) || (currentBet == 0 && currentStreet != Street.Preflop);
		
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
		sfxPlayer.PlayRandomChip();
		
		// track if this might be a bluff
		if (handStrength < 0.4f)
		{
			aiBluffedThisHand = true;
		}
	}

	private void OnOpponentAllIn()
	{
		int allInAmount = opponentChips;
		
		AddToPot(false, allInAmount);
		opponentBet += allInAmount;
		opponentChipsInPot += allInAmount;  // Track in current betting round
		opponentChips = 0;
		aiOpponent.ChipStack = 0;
		
		opponentIsAllIn = true;
		aiOpponent.IsAllIn = true;
		
		currentBet = Math.Max(currentBet, opponentBet);
		playerHasActedThisStreet = false;
		
		GameState gameState = CreateGameState();
		aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(gameState);
		
		ShowMessage($"{currentOpponentName} goes ALL-IN for ${allInAmount}!");
		GD.Print($"{currentOpponentName} ALL-IN: {allInAmount}");
		sfxPlayer.PlayRandomChip();
	}

	// --- AI HELPERS ---

	private PlayerAction DecideAIAction(GameState gameState)
	{
		return aiOpponent.MakeDecision(gameState);
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
}
