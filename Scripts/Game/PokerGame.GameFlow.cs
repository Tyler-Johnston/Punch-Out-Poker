using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokerGame
{
	//  GAME LIFECYCLE 
	private async void StartNewHand()
	{
		if (!waitingForNextGame && handInProgress) return;
		
		VerifyTotalChips("StartNewHand");

		waitingForNextGame = false;
		isMatchComplete = false;
		SetExpression(Expression.Neutral);
		ResetPlayerWaitTime(); 

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}

		// Reset AI opponent for new hand
		aiOpponent.ResetForNewHand();
		SetOpponentChips(opponentChips);

		ResetBetUI();
		ResetHandState();
		ResetBoardVisuals();

		deck = new Deck();
		deck.Shuffle();

		await DealInitialHands();

		currentStreet = Street.Preflop;
		if (dialogueManager != null)
		{
			dialogueManager.ResetForNewHand();
		}
		handInProgress = true;

		RefreshAllInFlagsFromStacks();
		await PostBlinds();
		UpdateHud();

		// If both are all-in (or player forced all-in), skip betting logic
		if (playerIsAllIn || opponentIsAllIn)
		{
			GD.Print("> FORCED ALL-IN (Blinds). Skipping to runout.");
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
		isPlayerTurn = playerHasButton;

		bool wasProcessing = isProcessingAIAction;
		isProcessingAIAction = true;

		foldButton.Disabled = true;
		checkCallButton.Disabled = true;
		betRaiseButton.Disabled = true;

		if (playerHasButton)
		{
			// Player SB → Opponent BB
			Assert(isPlayerTurn == true, "Player is Button (SB) but isPlayerTurn is false!");
			PostBlind(true, smallBlind, "small blind");
			await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
			PostBlind(false, bigBlind, "big blind");
		}
		else
		{
			// Opponent SB → Player BB
			Assert(isPlayerTurn == false, "Opponent is Button (SB) but isPlayerTurn is true!");
			PostBlind(false, smallBlind, "small blind");
			await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
			PostBlind(true, bigBlind, "big blind");
		}

		RefreshAllInFlagsFromStacks();
		playerCanReopenBetting = true;
		opponentCanReopenBetting = true;
		lastRaiseAmount = bigBlind;

		isProcessingAIAction = wasProcessing;
		AssertOpponentChipsSynced("PostBlinds");
	}

	private void PostBlind(bool isPlayer, int blindAmount, string blindType)
	{
		int amount = Math.Min(blindAmount, isPlayer ? playerChips : opponentChips);

		if (isPlayer)
		{
			SpendPlayerChips(amount);
			playerBet = amount;
			ShowMessage($"You post the ${amount} {blindType}");
			GD.Print($"> Player posts {blindType}: ${amount}");
		}
		else
		{
			SpendOpponentChips(amount);
			opponentBet = amount;
			ShowMessage($"{currentOpponentName} posts the ${amount} {blindType}");
			GD.Print($"> {currentOpponentName} posts {blindType}: ${amount}");
		}

		CommitToStreetPot(isPlayer, amount);

		currentBet = Math.Max(playerBet, opponentBet);
		sfxPlayer.PlayRandomChip();
		UpdateHud(true);

		GameManager.LogVerbose($"Blind posted. EffectivePot: {GetEffectivePot()} (Settled pot: {pot})");
	}

	private async void EndHand()
	{
		if (isShowdownInProgress) return;

		pot = 0;
		displayPot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		betAmount = 0;
		handInProgress = false;
		waitingForNextGame = true;
		ResetPlayerWaitTime();

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}

		// Check for rage quit or surrender
		OpponentExitType exitType = aiOpponent.CheckForEarlyExit();
		if (exitType != OpponentExitType.None)
		{
			await HandleOpponentExit(exitType);
			return;
		}

		UpdateHud();
	}

	private void HandleGameOver(bool opponentSurrendered = false)
	{
		isMatchComplete = true;
		bool playerWon = opponentSurrendered || (opponentChips <= 0);

		if (playerWon)
		{
			int winnings = buyInAmount * 2;
			GameManager.Instance.OnMatchWon(currentOpponentName, winnings);

			string reason = opponentSurrendered ? "surrendered!" : "went bust!";
			ShowMessage($"VICTORY! {currentOpponentName} {reason}");
			GD.Print($"\n=== VICTORY vs {currentOpponentName} ===");
		}
		else
		{
			GameManager.Instance.OnMatchLost(currentOpponentName);
			ShowMessage($"{currentOpponentName} wins!");
			GD.Print($"\n=== DEFEAT vs {currentOpponentName} ===");
		}

		UpdateHud();
	}

	//  CARD DEALING 
	private async Task DealInitialHands()
	{
		GD.Print($"\n=== HAND STARTED (Blinds {smallBlind}/{bigBlind}) ===");

		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

		playerHand.Add(deck.Deal());
		playerHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());

		// Sync AI hand
		aiOpponent.Hand.Clear();
		foreach (var card in opponentHand)
		{
			aiOpponent.DealCard(card);
		}

		GD.Print($"[PLAYER] {CardListToString(playerHand)}   vs   [{currentOpponentName.ToUpper()}] {CardListToString(opponentHand)}");
		GD.Print($"[STACKS] Player: ${playerChips} | {currentOpponentName}: ${opponentChips}");
		await AnimateInitialPlayerCards();

		// Opponent cards stay face down
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();

		// Show reaction to hole cards
		ShowPreflopTell();
	}
	
	private string CardListToString(List<Card> cards)
	{
		return string.Join(" ", cards);
	}

	public async Task DealCommunityCards(Street street)
	{
		switch (street)
		{
			case Street.Flop:
				await DealFlop();
				break;

			case Street.Turn:
				await DealTurn();
				break;

			case Street.River:
				await DealRiver();
				break;
		}
	}

	//  STREET PROGRESSION 
	private async void AdvanceStreet()
	{
		await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);

		bool betsAreEqual = (playerBet == opponentBet);
		bool someoneIsAllIn = (playerIsAllIn || opponentIsAllIn);
		
		// If bets aren't equal and nobody is all-in, we CANNOT advance. It means someone still has an action pending.
		Assert(betsAreEqual || someoneIsAllIn, $"Attempting to advance street but bets are unequal! P: {playerBet} vs O: {opponentBet}");

		bool shouldReset =
			(playerBet != 0) || (opponentBet != 0) || (currentBet != 0) || (previousBet != 0) ||
			(playerChipsInPot != 0) || (opponentChipsInPot != 0) ||
			playerHasActedThisStreet || opponentHasActedThisStreet;

		if (shouldReset)
		{
			ResetBettingRound();
		}

		await ToSignal(GetTree().CreateTimer(1.2f), SceneTreeTimer.SignalName.Timeout);

		aiBluffedThisHand = false;
		isProcessingAIAction = false;

		Street nextStreet = GetNextStreetOrShowdown();
		
		if (nextStreet == Street.River && currentStreet == Street.River)
			return;

		currentStreet = nextStreet;
		if (dialogueManager != null)
		{
			dialogueManager.SetCurrentStreet(currentStreet);
		}

		await DealCommunityCards(nextStreet);

		// All-in runout: just keep dealing streets; no betting round to reset/settle.
		if (playerIsAllIn || opponentIsAllIn)
		{
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			return;
		}

		// Post-flop: non-button acts first
		isPlayerTurn = !playerHasButton;

		double waitTime = (!isPlayerTurn) ? 1.15 : 0.0;
		GetTree().CreateTimer(waitTime).Timeout += () =>
		{
			UpdateHud();

			if (!isPlayerTurn)
				CheckAndProcessAITurn();
		};
	}

	private async void ShowDown()
	{
		if (isShowdownInProgress) return;

		isShowdownInProgress = true;

		GD.Print("\n=== SHOWDOWN ===");

		bool refundOccurred = ReturnUncalledChips();
		if (refundOccurred)
		{
			displayPot = pot;
			UpdateHud();
			ShowMessage("Returned Uncalled Chips");
			await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
		}

		if (playerChipsInPot > 0 || opponentChipsInPot > 0)
		{
			SettleStreetIntoPot();
			GameManager.LogVerbose($"[Showdown] Settled final street into pot: {pot}");
		}

		await RevealOpponentHand();

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
		string opponentHandName = HandEvaluator.GetHandDescription(opponentHand, communityCards);

		lastHandDescription = $"You: {playerHandName} VS {currentOpponentName}: {opponentHandName}";

		handTypeLabel.Text = lastHandDescription;
		handTypeLabel.Visible = true;
		
		GD.Print($"[PLAYER] {string.Join(" ", playerHand)} -> {playerHandName}");
		GD.Print($"[{currentOpponentName.ToUpper()}] {string.Join(" ", opponentHand)} -> {opponentHandName}");
		ResolveShowdownResult(playerRank, opponentRank);

		RefreshAllInFlagsFromStacks();
		ClearPotTracking();

		isShowdownInProgress = false;
		UpdateHud();
		EndHand();
	}

	//  AI TURN PROCESSING 

	private void CheckAndProcessAITurn()
	{
		GameManager.LogVerbose($"[CheckAndProcessAITurn] isProcessing={isProcessingAIAction}, isPlayerTurn={isPlayerTurn}, handInProgress={handInProgress}");

		if (IsAIDebugDisabled())
		{
			GameManager.LogVerbose("[DEBUG] AI turn skipped (AI manually disabled)");
			return;
		}

		if (isProcessingAIAction)
		{
			GameManager.LogVerbose("CheckAndProcessAITurn blocked: already processing");
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
		GameManager.LogVerbose($"[ProcessOpponentTurn] isProcessing={isProcessingAIAction}, isPlayerTurn={isPlayerTurn}");

		if (isProcessingAIAction) return;
		if (isPlayerTurn || !handInProgress || waitingForNextGame) return;

		// Lock to prevent re-entry
		isProcessingAIAction = true;
		opponentHasActedThisStreet = true;

		GameState gameState = CreateGameState();
		PlayerAction action = DecideAIAction(gameState);
		
		float strength = aiOpponent.EvaluateCurrentHandStrength(gameState);
		HandStrength cat = aiOpponent.DetermineHandStrengthCategory(gameState);

		bool isAggressive = (action == PlayerAction.Raise || action == PlayerAction.AllIn);
		bool bluffThisAction = false;

		// Stricter: only call it a bluff when they’re truly weak
		if (isAggressive && cat == HandStrength.Weak && strength < 0.35f)
		{
			bluffThisAction = true;
			GameManager.LogVerbose($"[TELL SYSTEM] Flagged BLUFF (Action:{action}, Str:{strength:F2}, Cat:{cat})");
		}
		else
		{
			GameManager.LogVerbose($"[TELL SYSTEM] Not a bluff (Action:{action}, Str:{strength:F2}, Cat:{cat})");
		}

		aiBluffedThisHand = bluffThisAction;

		float waitTime = PlayActionDialogue(action, gameState);
		if (waitTime > 0)
		{
			await ToSignal(GetTree().CreateTimer(waitTime + 1.0f), SceneTreeTimer.SignalName.Timeout);
		}

		await ExecuteAIAction(action);

		if (action == PlayerAction.Fold || !handInProgress)
		{
			isProcessingAIAction = false;
			return;
		}

		HandlePostAIActionBettingState();
	}
	
	private PlayerAction DecideAIAction(GameState gameState)
	{
		return aiOpponent.MakeDecision(gameState);
	}

	private async Task ExecuteAIAction(PlayerAction action)
	{
		GameState gameState = CreateGameState();
		bool isBluffing = aiBluffedThisHand;
		float strength = aiOpponent.EvaluateCurrentHandStrength(gameState);

		ShowActionTell(action, isBluffing, strength);

		await ToSignal(GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);

		switch (action)
		{
			case PlayerAction.Fold:
				await OnOpponentFold();
				break;

			case PlayerAction.Check:
				await OnOpponentCheck();
				break;

			case PlayerAction.Call:
				await OnOpponentCall();
				break;

			case PlayerAction.Raise:
				await OnOpponentRaise();
				break;

			case PlayerAction.AllIn:
				await OnOpponentAllIn();
				break;
		}

		UpdateHud();
		AssertOpponentChipsSynced("ExecuteAIAction");
	}

	//  AI ACTION HANDLERS 

	private async Task OnOpponentFold()
	{
		ShowMessage($"{currentOpponentName} folds");
		GD.Print($"> {currentOpponentName} Folds");

		Task card1Task = TossCard(opponentCard1, opponentHand[0], 2.5f, 1.5f, false);
		Task card2Task = TossCard(opponentCard2, opponentHand[1], 1.5f, 1.5f, false);

		await Task.WhenAll(card1Task, card2Task);
		await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);

		HandleOpponentFoldWin();
	}

	private async Task OnOpponentCheck()
	{
		ApplyAction(isPlayer: false, action: PlayerAction.Check);
		sfxPlayer.PlaySound("check", true);
		ShowMessage($"{currentOpponentName} checks");
		GD.Print($"> {currentOpponentName} Checks");
	}

	private async Task OnOpponentCall()
	{
		var result = ApplyAction(isPlayer: false, action: PlayerAction.Call);

		if (result.AmountMoved == 0)
		{
			await OnOpponentCheck();
			return;
		}

		if (result.AmountMoved < 0)
		{
			int refund = -result.AmountMoved;
			ShowMessage($"{currentOpponentName} takes back ${refund} excess chips");
			GameManager.LogVerbose($"{currentOpponentName} refunded {refund}");
			sfxPlayer.PlayRandomChip();
			return;
		}

		if (result.BecameAllIn)
		{
			GameState gameState = CreateGameState();
			aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(gameState);

			ShowMessage($"{currentOpponentName} calls all-in for ${result.AmountMoved}");
			GD.Print($"> {currentOpponentName} CALLS ALL-IN (${result.AmountMoved})");
		}
		else
		{
			ShowMessage($"{currentOpponentName} calls ${result.AmountMoved}");
			GD.Print($"> {currentOpponentName} Calls ${result.AmountMoved}");
		}

		sfxPlayer.PlayRandomChip();
	}

	private async Task OnOpponentRaise()
	{
		GameState gameState = CreateGameState();
		float hs = aiOpponent.EvaluateCurrentHandStrength(gameState);

		int raiseToTotal = decisionMaker.CalculateRaiseToTotal(aiOpponent, gameState, hs);
		float effPot = Mathf.Max(gameState.PotSize, 1f);
		GameManager.LogVerbose($"[AI SIZE] hs={hs:F2} effPot={effPot:F0} raiseToTotal={raiseToTotal}");

		int originalRaiseToTotal = raiseToTotal;
		int minLegalRaiseTotal = PokerRules.CalculateMinRaiseTotal(
			currentBet,
			previousBet,
			lastRaiseAmount,
			bigBlind
		);

		int maxPossible = opponentBet + opponentChips;

		if (raiseToTotal < minLegalRaiseTotal)
		{
			if (maxPossible < minLegalRaiseTotal)
			{
				if (maxPossible <= currentBet)
				{
					GameManager.LogVerbose($"[RAISE SAFETY] AI short (max={maxPossible} <= current={currentBet}). Converting to Call/All-In.");
					await OnOpponentCall();
					return;
				}
				else
				{
					GameManager.LogVerbose($"[RAISE SAFETY] AI short (max={maxPossible} < min={minLegalRaiseTotal}). Converting to All-In Shove.");
					await OnOpponentAllIn();
					return;
				}
			}
			else
			{
				raiseToTotal = minLegalRaiseTotal;
				GameManager.LogVerbose($"[RAISE SAFETY] Bumped to min.");
			}
		}

		if (raiseToTotal <= opponentBet)
		{
			GD.PrintErr($"[RAISE SAFETY] CRITICAL: raiseToTotal ({raiseToTotal}) <= opponentBet ({opponentBet}). Logic Error. Converting to Check.");
			ShowMessage($"{currentOpponentName} checks");
			return;
		}

		var result = ApplyAction(isPlayer: false, action: PlayerAction.Raise, raiseToTotal: raiseToTotal);

		if (result.AmountMoved <= 0)
		{
			ShowMessage($"{currentOpponentName} checks");
			GD.PrintErr($"[RAISE DEBUG ERROR] Raise produced 0 chips moved!");
			return;
		}

		bool isBet = result.IsBet;
		string actionWord = isBet ? "bets" : "raises to";

		ShowMessage($"{currentOpponentName} {actionWord}: ${opponentBet}");
		
		if (result.BecameAllIn)
		{
			GD.Print($"> {currentOpponentName} {actionWord.ToUpper()} ${opponentBet} (ALL-IN)");
		}
		else
		{
			GD.Print($"> {currentOpponentName} {actionWord} ${opponentBet}");
		}

		sfxPlayer.PlayRandomChip();

		if (result.BecameAllIn)
		{
			aiStrengthAtAllIn = hs;
			GameManager.LogVerbose($"[AI ALL-IN] Strength: {aiStrengthAtAllIn:F2}");
		}
	}

	private async Task OnOpponentAllIn()
	{
		var result = ApplyAction(isPlayer: false, action: PlayerAction.AllIn);

		if (result.AmountMoved <= 0)
		{
			GD.PrintErr($"{currentOpponentName} tried to go all-in with 0 chips.");
			return;
		}

		GameState gameState = CreateGameState();
		aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(gameState);

		ShowMessage($"{currentOpponentName} goes ALL-IN for ${result.AmountMoved}!");
		GD.Print($"> {currentOpponentName} GOES ALL-IN (${result.AmountMoved})");
		sfxPlayer.PlayRandomChip();
	}

	//  PRIVATE HELPERS 

	private void ResetBetUI()
	{
		betAmount = 0;
		if (betSlider != null)
		{
			betSlider.MinValue = 0;
			betSlider.MaxValue = 100;
			betSlider.SetValueNoSignal(0);
		}

		ShowMessage("");
		lastRaiseAmount = 0;
		lastHandDescription = "";
		handTypeLabel.Text = "";
		handTypeLabel.Visible = false;
		playerStackLabel.Visible = true;
		actionButtons.Visible = true;
		activePlayUI.Visible = true;
		betweenHandsUI.Visible = false;
		potArea.Visible = true;
		speechBubble.Visible = false;
		sliderUI.Visible = true;

		foldButton.Visible = true;
		foldButton.Disabled = true;
		betRaiseButton.Visible = true;
		betRaiseButton.Disabled = true;
		checkCallButton.Text = "Check";
		betRaiseButton.Text = "Bet";
	}

	private void ResetHandState()
	{
		pot = 0;
		displayPot = 0;
		lastDisplayedPot = -1;
		lastPotLabel = -1;

		playerContributed = 0;
		opponentContributed = 0;
		playerTotalBetsThisHand = 0;

		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		previousBet = 0;

		playerChipsInPot = 0;
		opponentChipsInPot = 0;

		isPlayerTurn = false;
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
	}

	private void ResetBoardVisuals()
	{
		playerCard1.ShowBack();
		playerCard2.ShowBack();
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
		flop1.ShowBack();
		flop2.ShowBack();
		flop3.ShowBack();
		turnCard.ShowBack();
		riverCard.ShowBack();
		
		opponentCard1.Visible = false;
		opponentCard2.Visible = false;
	}

	private async Task AnimateInitialPlayerCards()
	{
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		sfxPlayer.PlaySound("card_flip");
		await playerCard1.RevealCard(playerHand[0]);
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		sfxPlayer.PlaySound("card_flip");
		await playerCard2.RevealCard(playerHand[1]);
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
	}

	private async Task DealFlop()
	{
		communityCards.Add(deck.Deal());
		communityCards.Add(deck.Deal());
		communityCards.Add(deck.Deal());

		// VISUAL IMPROVEMENT 3: Contextual Street Header
		GD.Print($"\n--- FLOP [{communityCards[0]} {communityCards[1]} {communityCards[2]}] (Pot: ${pot}) ---");
		ShowMessage("Flop dealt");

		sfxPlayer.PlaySound("card_flip");
		await flop1.RevealCard(communityCards[0]);
		await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);

		sfxPlayer.PlaySound("card_flip");
		await flop2.RevealCard(communityCards[1]);
		await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);

		sfxPlayer.PlaySound("card_flip");
		await flop3.RevealCard(communityCards[2]);
	}

	private async Task DealTurn()
	{
		communityCards.Add(deck.Deal());
		// VISUAL IMPROVEMENT 3: Contextual Street Header
		GD.Print($"\n--- TURN [{communityCards[3]}] (Pot: ${pot}) ---");
		ShowMessage("Turn card");

		sfxPlayer.PlaySound("card_flip");
		await turnCard.RevealCard(communityCards[3]);
		await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
	}

	private async Task DealRiver()
	{
		communityCards.Add(deck.Deal());
		GD.Print($"\n--- RIVER [{communityCards[4]}] (Pot: ${pot}) ---");
		ShowMessage("River card");

		sfxPlayer.PlaySound("card_flip");
		await riverCard.RevealCard(communityCards[4]);
		await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
	}

	private Street GetNextStreetOrShowdown()
	{
		switch (currentStreet)
		{
			case Street.Preflop:
				return Street.Flop;
			case Street.Flop:
				return Street.Turn;
			case Street.Turn:
				return Street.River;
			case Street.River:
				ShowDown();
				return Street.River;
			default:
				return Street.River;
		}
	}

	private async Task RevealOpponentHand()
	{
		await TossCard(opponentCard1, opponentHand[0]);
		await ToSignal(GetTree().CreateTimer(0.30f), SceneTreeTimer.SignalName.Timeout);
		await TossCard(opponentCard2, opponentHand[1]);
		await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);
	}

	private void ResolveShowdownResult(int playerRank, int opponentRank)
	{
		string message;
		HandResult aiHandResult;
		int finalPot = pot;
		
		Assert(finalPot > 0, "Showdown triggered with Empty Pot!");
		Assert(playerChipsInPot + opponentChipsInPot == 0, "Showdown triggered but chips are still in 'Street Bets' tracking vars!");
		
		int result = HandEvaluator.CompareHands(playerRank, opponentRank);

		if (result > 0)
		{
			message = $"You won the ${finalPot} pot!";
			PlayReactionDialogue("OnLosePot");

			bool isBadBeat = (aiStrengthAtAllIn > 0.70f);
			bool isCooler = (opponentRank <= 1609);
			int tiltDelta = 0;

			if (isBadBeat)
			{
				aiHandResult = HandResult.BadBeat;
				tiltDelta = 15; 
			}
			else if (isCooler)
			{
				aiHandResult = HandResult.BadBeat;
				tiltDelta = 12;
			}
			else if (aiBluffedThisHand && opponentRank > 6185)
			{
				aiHandResult = HandResult.BluffCaught;
				tiltDelta = 5;
			}
			else
			{
				aiHandResult = HandResult.Loss;
				tiltDelta = 3; 
			}

			ShowResultTell(false, tiltDelta);
			AddPlayerChips(finalPot);
			aiOpponent.ProcessHandResult(aiHandResult, finalPot, bigBlind);
		}
		else if (result < 0)
		{
			message = $"{currentOpponentName} won the ${finalPot} pot!";
			PlayReactionDialogue("OnWinPot");
			ShowResultTell(true, 0);
			AddOpponentChips(finalPot);
			aiOpponent.ProcessHandResult(HandResult.Win, finalPot, bigBlind);
		}
		else
		{
			int split = finalPot / 2;
			message = $"Split pot. ${split} each!";

			AddPlayerChips(split);
			AddOpponentChips(finalPot - split);
			aiOpponent.ProcessHandResult(HandResult.Neutral, finalPot, bigBlind);
			SetExpression(Expression.Neutral);
		}

		GD.Print("> " + message);
		ShowMessage(message);
	}

	private void ClearPotTracking()
	{
		pot = 0;
		displayPot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		playerContributed = 0;
		opponentContributed = 0;
		
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		previousBet = 0;
		lastRaiseAmount = 0;
	}

	private void HandlePostAIActionBettingState()
	{
		bool betsEqual = (playerBet == opponentBet);
		bool bothActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;

		bool playerCannotAct = playerIsAllIn || !playerCanReopenBetting;
		bool opponentCannotAct = opponentIsAllIn || !opponentCanReopenBetting;

		GameManager.LogVerbose($"After AI action: betsEqual={betsEqual}, bothActed={bothActed}, bothAllIn={bothAllIn}");

		if (bothAllIn ||
			(betsEqual && bothActed && playerCannotAct && opponentCannotAct) ||
			(opponentIsAllIn && !opponentCanReopenBetting && bothActed))
		{
			GameManager.LogVerbose("Betting round complete after AI action");
			isProcessingAIAction = false;
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (playerIsAllIn && !betsEqual)
		{
			GameManager.LogVerbose("Betting round complete: Player all-in, cannot match AI raise");
			isProcessingAIAction = false;
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else
		{
			isProcessingAIAction = false;
			isPlayerTurn = true;
			UpdateHud();
		}
	}

	private async Task HandleOpponentExit(OpponentExitType exitType)
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
	}

	private void HandleOpponentFoldWin()
	{
		int effectivePot = GetEffectivePot();
		float betRatio = (effectivePot > 0) ? (float)currentBet / effectivePot : 0f;
		
		GameManager.LogVerbose($"[TILT] Bullied ratio={betRatio:F2}, currentBet={currentBet}, effectivePot={effectivePot}");
		aiOpponent.OnFolded(betRatio);

		int winAmount = effectivePot;
		AddPlayerChips(winAmount);
		RefreshAllInFlagsFromStacks();

		string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
		lastHandDescription = $"You: {playerHandName} VS {currentOpponentName}: ???";

		ClearPotTracking();

		aiOpponent.IsFolded = true;
		handInProgress = false;

		GetTree().CreateTimer(1.5).Timeout += () =>
		{
			ShowMessage($"You win ${winAmount}!");
			EndHand();
		};
	}
}
