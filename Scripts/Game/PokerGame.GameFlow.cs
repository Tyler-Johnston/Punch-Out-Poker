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

		waitingForNextGame = false;
		isMatchComplete = false;
		SetExpression(Expression.Neutral);

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}

		// Reset AI opponent for new hand
		aiOpponent.ResetForNewHand();
		SetOpponentChips(opponentChips);

		GD.Print("\n=== New Hand ===");
		ShowMessage("");

		ResetBetUI();
		ResetHandState();
		ResetBoardVisuals();

		deck = new Deck();
		deck.Shuffle();

		await DealInitialHands();

		tellTimer.Start();

		currentStreet = Street.Preflop;
		handInProgress = true;

		RefreshAllInFlagsFromStacks();
		await PostBlinds();
		UpdateHud();

		// If both are all-in (or player forced all-in), skip betting logic
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

		bool wasProcessing = isProcessingAIAction;
		isProcessingAIAction = true;

		foldButton.Disabled = true;
		checkCallButton.Disabled = true;
		betRaiseButton.Disabled = true;

		if (playerHasButton)
		{
			// Player SB → Opponent BB
			PostBlind(true, smallBlind, "small blind");
			await ToSignal(GetTree().CreateTimer(2.0f), SceneTreeTimer.SignalName.Timeout);
			PostBlind(false, bigBlind, "big blind");
		}
		else
		{
			// Opponent SB → Player BB
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
			GD.Print($"Player posts {blindType}: {amount}");
		}
		else
		{
			SpendOpponentChips(amount);
			opponentBet = amount;
			ShowMessage($"{currentOpponentName} posts the ${amount} {blindType}");
			GD.Print($"Opponent posts {blindType}: {amount}");
		}

		CommitToStreetPot(isPlayer, amount);

		currentBet = Math.Max(playerBet, opponentBet);
		sfxPlayer.PlayRandomChip();
		UpdateHud(true);

		GD.Print($"Blind posted. EffectivePot: {GetEffectivePot()} (Settled pot: {pot})");
	}

	private async void EndHand()
	{
		tellTimer.Stop();

		if (isShowdownInProgress) return;

		pot = 0;
		displayPot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		betAmount = 0;
		handInProgress = false;
		waitingForNextGame = true;

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

	//  CARD DEALING 
	private async Task DealInitialHands()
	{
		GD.Print("\n=== Dealing Initial Hands ===");

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

		GD.Print($"Player hand: {playerHand[0]}, {playerHand[1]}");
		GD.Print($"Opponent hand: {opponentHand[0]}, {opponentHand[1]}");

		await AnimateInitialPlayerCards();

		// Opponent cards stay face down
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
				await DealFlop();
				break;

			case Street.Turn:
				await DealTurn();
				break;

			case Street.River:
				await DealRiver();
				break;
		}

		ShowTell(true);
	}

	//  STREET PROGRESSION 
	private async void AdvanceStreet()
	{
		await ToSignal(GetTree().CreateTimer(0.8f), SceneTreeTimer.SignalName.Timeout);

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
			UpdateButtonLabels();
			RefreshBetSlider();

			if (!isPlayerTurn)
				CheckAndProcessAITurn();
		};
	}

	private async void ShowDown()
	{
		// Stop tells during showdown
		if (tellTimer != null) tellTimer.Stop();
		if (isShowdownInProgress) return;

		isShowdownInProgress = true;

		GD.Print("\n=== Showdown ===");

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
			GD.Print($"[Showdown] Settled final street into pot: {pot}");
		}
		else
		{
			GD.Print($"[Showdown] Pot already settled: {pot}");
		}

		await RevealOpponentHand();

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
		string opponentHandName = HandEvaluator.GetHandDescription(opponentHand, communityCards);

		lastHandDescription = $"You: {playerHandName} VS {currentOpponentName}: {opponentHandName}";

		handTypeLabel.Text = lastHandDescription;
		handTypeLabel.Visible = true;

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

		// Lock to prevent re-entry
		isProcessingAIAction = true;
		opponentHasActedThisStreet = true;

		GameState gameState = CreateGameState();
		PlayerAction action = DecideAIAction(gameState);

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

	private async Task ExecuteAIAction(PlayerAction action)
	{
		string actionText = action.ToString();

		if (action == PlayerAction.Raise)
		{
			bool isBet = (currentBet == 0);
			actionText = isBet ? "Bet" : "Raise";
		}

		GD.Print($"[AI ACTION] {currentOpponentName}: {actionText}");

		UpdateOpponentExpression(action);
		if (tellTimer != null) tellTimer.Start();

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
		GD.Print($"{currentOpponentName} folds");

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
		GD.Print($"{currentOpponentName} checks");
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
			GD.Print($"{currentOpponentName} refunded {refund}");
			sfxPlayer.PlayRandomChip();
			return;
		}

		if (result.BecameAllIn)
		{
			GameState gameState = CreateGameState();
			aiStrengthAtAllIn = aiOpponent.EvaluateCurrentHandStrength(gameState);

			ShowMessage($"{currentOpponentName} calls all-in for ${result.AmountMoved}");
			GD.Print($"{currentOpponentName} calls all-in: {result.AmountMoved}");
		}
		else
		{
			ShowMessage($"{currentOpponentName} calls ${result.AmountMoved}");
			GD.Print($"{currentOpponentName} calls: {result.AmountMoved}");
		}

		sfxPlayer.PlayRandomChip();
	}

	private async Task OnOpponentRaise()
	{
		GameState gameState = CreateGameState();
		float hs = aiOpponent.EvaluateCurrentHandStrength(gameState);

		int raiseToTotal = decisionMaker.CalculateRaiseToTotal(aiOpponent, gameState, hs);
		float effPot = Mathf.Max(gameState.PotSize, 1f);
		GD.Print($"[AI SIZE] hs={hs:F2} effPot={effPot:F0} raiseToTotal={raiseToTotal} finalRatio={(raiseToTotal / effPot):F2}x");

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
					GD.Print($"[RAISE SAFETY] ⚠️ AI short (max={maxPossible} <= current={currentBet}). Converting to Call/All-In.");
					await OnOpponentCall();
					return;
				}
				else
				{
					GD.Print($"[RAISE SAFETY] ⚠️ AI short (max={maxPossible} < min={minLegalRaiseTotal}). Converting to All-In Shove.");
					await OnOpponentAllIn();
					return;
				}
			}
			else
			{
				raiseToTotal = minLegalRaiseTotal;
				GD.Print($"[RAISE SAFETY] ⚠️ raiseToTotal ({originalRaiseToTotal}) < minLegal ({minLegalRaiseTotal}). Bumped to min.");
			}
		}

		if (raiseToTotal <= opponentBet)
		{
			GD.Print($"[RAISE SAFETY] ❌ CRITICAL: raiseToTotal ({raiseToTotal}) <= opponentBet ({opponentBet}). Logic Error. Converting to Check.");
			ShowMessage($"{currentOpponentName} checks");
			return;
		}

		GD.Print($"[RAISE DEBUG BEFORE] raiseToTotal={raiseToTotal}, currentBet={currentBet}, opponentBet={opponentBet}, chips={opponentChips}");

		var result = ApplyAction(isPlayer: false, action: PlayerAction.Raise, raiseToTotal: raiseToTotal);

		GD.Print($"[RAISE DEBUG AFTER] AmountMoved={result.AmountMoved}, NewOpponentBet={opponentBet}, NewCurrentBet={currentBet}, IsBet={result.IsBet}, BecameAllIn={result.BecameAllIn}");

		if (result.AmountMoved <= 0)
		{
			ShowMessage($"{currentOpponentName} checks");
			GD.Print($"[RAISE DEBUG ERROR] ❌ Raise produced 0 chips moved! Bug.");
			return;
		}

		bool isBet = result.IsBet;
		string actionWord = isBet ? "bets" : "raises to";

		ShowMessage($"{currentOpponentName} {actionWord}: {opponentBet}");
		GD.Print($"{currentOpponentName} {actionWord}: {opponentBet}" + (result.BecameAllIn ? " (ALL-IN)" : ""));

		sfxPlayer.PlayRandomChip();

		if (result.BecameAllIn)
		{
			aiStrengthAtAllIn = hs;
			GD.Print($"[AI ALL-IN] Recorded strength: {aiStrengthAtAllIn:F2}");
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
		GD.Print($"{currentOpponentName} ALL-IN: {result.AmountMoved}");
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

		GD.Print($"Flop: {communityCards[0]}, {communityCards[1]}, {communityCards[2]}");
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
		GD.Print($"Turn: {communityCards[3]}");
		ShowMessage("Turn card");

		sfxPlayer.PlaySound("card_flip");
		await turnCard.RevealCard(communityCards[3]);
		await ToSignal(GetTree().CreateTimer(0.45f), SceneTreeTimer.SignalName.Timeout);
	}

	private async Task DealRiver()
	{
		communityCards.Add(deck.Deal());
		GD.Print($"River: {communityCards[4]}");
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

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);

		if (result > 0)
		{
			GD.Print("\nPLAYER WINS!");
			message = $"You won the ${finalPot} pot!";
			PlayReactionDialogue("OnLosePot");

			bool isBadBeat = (aiStrengthAtAllIn > 0.70f);
			bool isCooler = (opponentRank <= 1609);

			if (isBadBeat)
			{
				aiHandResult = HandResult.BadBeat;
				SetExpression(Expression.Angry);
			}
			else if (isCooler)
			{
				aiHandResult = HandResult.BadBeat;
				SetExpression(Expression.Sad);
			}
			else if (aiBluffedThisHand && opponentRank > 6185)
			{
				aiHandResult = HandResult.BluffCaught;
				SetExpression(Expression.Surprised);
			}
			else
			{
				aiHandResult = HandResult.Loss;
				SetExpression(Expression.Sad);
			}

			AddPlayerChips(finalPot);
			aiOpponent.ProcessHandResult(aiHandResult, finalPot, bigBlind);
		}
		else if (result < 0)
		{
			GD.Print("\nOPPONENT WINS!");
			message = $"{currentOpponentName} won the ${finalPot} pot!";
			PlayReactionDialogue("OnWinPot");

			AddOpponentChips(finalPot);
			aiOpponent.ProcessHandResult(HandResult.Win, finalPot, bigBlind);
		}
		else
		{
			GD.Print("\nSPLIT POT!");
			int split = finalPot / 2;
			message = $"Split pot. ${split} each!";

			AddPlayerChips(split);
			AddOpponentChips(finalPot - split);
			aiOpponent.ProcessHandResult(HandResult.Neutral, finalPot, bigBlind);
			SetExpression(Expression.Neutral);
		}

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
	}

	private void HandlePostAIActionBettingState()
	{
		bool betsEqual = (playerBet == opponentBet);
		bool bothActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;

		bool playerCannotAct = playerIsAllIn || !playerCanReopenBetting;
		bool opponentCannotAct = opponentIsAllIn || !opponentCanReopenBetting;

		GD.Print($"After AI action: betsEqual={betsEqual}, bothActed={bothActed}, bothAllIn={bothAllIn}");
		GD.Print($"Reopening: playerCan={playerCanReopenBetting} (allIn={playerIsAllIn}), opponentCan={opponentCanReopenBetting} (allIn={opponentIsAllIn})");

		if (bothAllIn ||
			(betsEqual && bothActed && playerCannotAct && opponentCannotAct) ||
			(opponentIsAllIn && !opponentCanReopenBetting && bothActed))
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
		GD.Print($"[TILT] Bullied ratio={betRatio:F2}, currentBet={currentBet}, effectivePot={effectivePot}");
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
