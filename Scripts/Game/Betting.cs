// PokerGame.Betting.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	// Betting-related fields
	private int playerChips = 1000;
	private int opponentChips = 1000;
	private int pot = 0;
	private int betAmount = 20;
	private int currentBet = 0;
	private int playerBet = 0;
	private int opponentBet = 0;
	private int smallBlind = 5;
	private int bigBlind = 10;

	private bool handInProgress = false;
	private bool waitingForNextGame = false;
	private bool isPlayerTurn = true;
	private bool playerIsAllIn = false;
	private bool opponentIsAllIn = false;

	// Action tracking to prevent infinite loops
	private bool playerHasActedThisStreet = false;
	private bool opponentHasActedThisStreet = false;

	private int raisesThisStreet = 0;
	private const int MAX_RAISES_PER_STREET = 4;

	private void StartNewHand()
	{
		// Check if either player is out of chips
		if (playerChips < smallBlind)
		{
			ShowMessage("GAME OVER - You ran out of chips!");
			GD.Print("GAME OVER - Player has no chips");
			waitingForNextGame = true;
			handInProgress = false;
			UpdateHud();
			return;
		}

		if (opponentChips < bigBlind)
		{
			ShowMessage("YOU WIN - Opponent ran out of chips!");
			GD.Print("GAME OVER - Opponent has no chips");
			waitingForNextGame = true;
			handInProgress = false;
			UpdateHud();
			return;
		}

		GD.Print("\n=== New Hand ===");
		ShowMessage("New hand starting...");

		foldButton.Visible = true;
		betRaiseButton.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";

		deck = new Deck();
		deck.Shuffle();

		pot = 0;
		currentBet = bigBlind;
		playerBet = smallBlind;
		opponentBet = bigBlind;
		aiBluffedThisHand = false;
		playerTotalBetsThisHand = 0;
		raisesThisStreet = 0;
		playerIsAllIn = false;
		opponentIsAllIn = false;

		// Reset action tracking
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

		// Player is small blind, opponent is big blind
		playerChips -= smallBlind;
		opponentChips -= bigBlind;
		pot += smallBlind + bigBlind;

		ShowMessage($"Blinds posted: You {smallBlind}, Opponent {bigBlind}");
		GD.Print($"Blinds posted: Player {smallBlind}, Opponent {bigBlind}, Pot: {pot}");

		// Player acts first preflop (small blind position)
		isPlayerTurn = true;
		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
	}

	private void OnFoldPressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		playerHasActedThisStreet = true;

		ShowMessage("You fold");
		GD.Print("Player folds");
		opponentChips += pot;
		pot = 0;
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		handInProgress = false;
		waitingForNextGame = true;
		UpdateHud();
		RefreshBetSlider();
	}

	private void OnCheckCallPressed()
	{
		if (!handInProgress)
		{
			checkCallButton.Text = "Check";
			StartNewHand();
			return;
		}

		if (!isPlayerTurn) return;

		playerHasActedThisStreet = true;

		int toCall = currentBet - playerBet;

		if (toCall == 0)
		{
			ShowMessage("You check");
			GD.Print($"Player checks on {currentStreet}");
		}
		else
		{
			int actualCall = Math.Min(toCall, playerChips);
			playerChips -= actualCall;
			playerBet += actualCall;
			pot += actualCall;

			if (playerChips == 0)
			{
				playerIsAllIn = true;
				ShowMessage($"You call {actualCall} chips (ALL-IN!)");
				GD.Print($"Player calls {actualCall} (ALL-IN)");
			}
			else
			{
				ShowMessage($"You call {actualCall} chips");
				GD.Print($"Player calls {actualCall}, Player stack: {playerChips}, Pot: {pot}");
			}
		}

		isPlayerTurn = false;
		UpdateHud();
		RefreshBetSlider();

		// Delay AI action slightly
		GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
	}

	private void OnBetRaisePressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		bool isRaise = currentBet > 0;

		// Check raise limit
		if (isRaise && raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			ShowMessage("Maximum raises reached - can only call or fold");
			GD.Print("Max raises reached this street");
			return;
		}

		playerHasActedThisStreet = true;

		// Final safety clamp in case slider/state was stale
		betAmount = Math.Clamp(betAmount, 1, playerChips);

		int raiseAmount = betAmount;
		int totalBet = currentBet + raiseAmount;
		int toAdd = totalBet - playerBet;
		int actualBet = Math.Min(toAdd, playerChips);

		playerChips -= actualBet;
		playerBet += actualBet;
		pot += actualBet;
		currentBet = playerBet;

		playerBetOnStreet[currentStreet] = true;
		playerBetSizeOnStreet[currentStreet] = actualBet;
		playerTotalBetsThisHand++;

		// Only increment raise counter if this is actually a raise
		if (isRaise)
		{
			raisesThisStreet++;
		}

		if (playerChips == 0)
		{
			playerIsAllIn = true;
			ShowMessage($"You raise to {playerBet} chips (ALL-IN!)");
			GD.Print($"Player raises to {playerBet} (ALL-IN)");
		}
		else if (isRaise)
		{
			ShowMessage($"You raise to {playerBet} chips");
			GD.Print($"Player raises to {playerBet}");
		}
		else
		{
			ShowMessage($"You bet {actualBet} chips");
			GD.Print($"Player bets {actualBet}");
		}

		isPlayerTurn = false;
		UpdateHud();
		RefreshBetSlider();

		// Delay AI action
		GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
	}

	private void ProcessAIAction()
	{
		if (!handInProgress) return;

		AIAction action = DecideAIAction();
		ExecuteAIAction(action);

		opponentHasActedThisStreet = true;

		// Check if hand ended (AI folded or won)
		if (!handInProgress) return;

		// Proper betting round completion logic
		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool someoneAllIn = playerIsAllIn || opponentIsAllIn;

		// Round is complete when bets match AND both have acted, OR someone is all-in
		if ((betsAreEqual && bothPlayersActed) || someoneAllIn)
		{
			GD.Print($"Betting round complete: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}, allIn={someoneAllIn}");
			// Add delay before advancing street for better UX
			GetTree().CreateTimer(1.0).Timeout += AdvanceStreet;
		}
		else
		{
			// Betting continues - give turn to player
			GD.Print($"Betting continues: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}");
			isPlayerTurn = true;
			UpdateHud();
			UpdateButtonLabels();
			RefreshBetSlider();
		}
	}

	private void UpdateButtonLabels()
	{
		int toCall = currentBet - playerBet;
		var (minBet, maxBet) = GetLegalBetRange();

		bool allInOnly = (minBet == maxBet && maxBet == playerChips);
		bool sliderAllIn = (maxBet == playerChips && betAmount == maxBet);

		if (toCall == 0)
		{
			checkCallButton.Text = "Check";

			if (allInOnly || sliderAllIn)
			{
				// Max slider => ALL IN for current stack
				betRaiseButton.Text = $"ALL IN ({maxBet})";
			}
			else
			{
				betRaiseButton.Text = $"Bet {betAmount}";
			}
		}
		else
		{
			checkCallButton.Text = $"Call {Math.Min(toCall, playerChips)}";

			int raiseTotal = currentBet + betAmount;
			int toAddForRaise = raiseTotal - playerBet;

			if (allInOnly || sliderAllIn)
			{
				betRaiseButton.Text = $"ALL IN ({maxBet})";
			}
			else
			{
				betRaiseButton.Text = $"Raise {toAddForRaise}";
			}
		}

		// Disable raise button if max raises reached
		if (raisesThisStreet >= MAX_RAISES_PER_STREET && !waitingForNextGame)
		{
			betRaiseButton.Disabled = true;
			betRaiseButton.Text = "Max raises";
		}
	}


	private void UpdateHud()
	{
		if (waitingForNextGame)
		{
			// Check if game is over
			if (playerChips < smallBlind || opponentChips < bigBlind)
			{
				checkCallButton.Text = "GAME OVER";
				checkCallButton.Disabled = true;
			}
			else
			{
				checkCallButton.Text = $"Next Hand";
				checkCallButton.Disabled = false;
			}

			foldButton.Visible = false;
			betRaiseButton.Visible = false;
		}
		else
		{
			UpdateButtonLabels();
			foldButton.Visible = true;
			betRaiseButton.Visible = true;

			// Disable buttons during AI turn to prevent race conditions
			bool enableButtons = isPlayerTurn && handInProgress && !playerIsAllIn;
			foldButton.Disabled = !enableButtons;
			checkCallButton.Disabled = !enableButtons;

			// Special handling for raise button
			if (!enableButtons || raisesThisStreet >= MAX_RAISES_PER_STREET)
			{
				betRaiseButton.Disabled = true;
			}
			else
			{
				betRaiseButton.Disabled = false;
			}
		}

		playerStackLabel.Text = $"You: {playerChips}";
		opponentStackLabel.Text = $"{currentOpponent.Name}: {opponentChips}";
		potLabel.Text = $"Pot: {pot}";
	}

	private void AdvanceStreet()
	{
		// Reset bets, action tracking, AND bluff flag for new street
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		raisesThisStreet = 0;
		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;
		aiBluffedThisHand = false; // Reset bluff flag each street
		isPlayerTurn = true;

		switch (currentStreet)
		{
			case Street.Preflop:
				DealCommunityCards(Street.Flop);
				RevealCommunityCards(Street.Flop);
				currentStreet = Street.Flop;
				break;
			case Street.Flop:
				DealCommunityCards(Street.Turn);
				RevealCommunityCards(Street.Turn);
				currentStreet = Street.Turn;
				break;
			case Street.Turn:
				DealCommunityCards(Street.River);
				RevealCommunityCards(Street.River);
				currentStreet = Street.River;
				break;
			case Street.River:
				ShowDown();
				return; // Don't update HUD/buttons, showdown handles it
		}

		// If both players are all-in, just keep dealing to showdown
		if (playerIsAllIn && opponentIsAllIn)
		{
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			return;
		}

		// If one player is all-in, skip betting and advance
		if (playerIsAllIn || opponentIsAllIn)
		{
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			return;
		}

		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
	}

	private (int minBet, int maxBet) GetLegalBetRange()
	{
		// Stack cap: slider must never go above this
		int maxBet = playerChips;
		if (maxBet <= 0)
			return (0, 0);

		bool opening = currentBet == 0;
		int minBet;

		if (opening)
		{
			// Opening the betting: at least big blind, but you can always go all‑in if shorter
			minBet = Math.Min(Math.Max(bigBlind, 1), maxBet);
		}
		else
		{
			// There is an existing bet (currentBet)
			int toCall = currentBet - playerBet;

			// Minimum legal *raise* size approximation: +big blind over currentBet
			int minRaiseSize = bigBlind;
			int minTotalBet = currentBet + minRaiseSize; // target total bet size
			int minToAdd = minTotalBet - playerBet;      // chips you must put in to make that

			// Base minimum: at least call, or a full raise if stack allows
			int fullMin = Math.Max(toCall, minToAdd);

			if (fullMin <= maxBet)
			{
				// You have enough to make a full legal raise
				minBet = fullMin;
			}
			else
			{
				// Short stack: you can only go all‑in as a raise, or call if you can afford it
				// From slider perspective, this is effectively "ALL IN"
				minBet = maxBet;
			}
		}

		// Final safety clamp
		if (minBet > maxBet)
			minBet = maxBet;

		return (minBet, maxBet);
	}

	private void RefreshBetSlider()
	{
		if (betSlider == null)
			return;

		var (minBet, maxBet) = GetLegalBetRange();

		if (maxBet <= 0)
		{
			betSlider.MinValue = 0;
			betSlider.MaxValue = 0;
			betSlider.Value = 0;
			betSlider.Editable = false;
			return;
		}

		betSlider.Editable = true;
		betSlider.MinValue = minBet;
		betSlider.MaxValue = maxBet;

		// Clamp betAmount explicitly
		betAmount = Math.Clamp(betAmount, minBet, maxBet);
		betSlider.Value = betAmount;
	}

	private void ShowDown()
	{
		GD.Print("\n=== Showdown ===");
		opponentCard1.ShowCard(opponentHand[0]);
		opponentCard2.ShowCard(opponentHand[1]);

		// Use your HandEvaluator
		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandName(playerRank);
		string opponentHandName = HandEvaluator.GetHandName(opponentRank);

		playerHandType.Text = playerHandName;
		opponentHandType.Text = opponentHandName;

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);
		string message = "";

		if (result > 0)
		{
			GD.Print("\n🎉 PLAYER WINS! 🎉");
			message = $"You win with {playerHandName}!";
			if (aiBluffedThisHand && opponentRank > 6185)
			{
				message += " Opponent was bluffing!";
			}
			playerChips += pot;
		}
		else if (result < 0)
		{
			GD.Print("\n😞 OPPONENT WINS! 😞");
			message = $"Opponent wins with {opponentHandName}";
			if (aiBluffedThisHand)
			{
				message += " (was bluffing with weak hand!)";
			}
			else if (opponentRank < 1609)
			{
				message += " - Opponent had a strong hand!";
			}
			opponentChips += pot;
		}
		else
		{
			GD.Print("\n🤝 TIE! SPLIT POT! 🤝");
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

		// Immediate bust-out detection
		if (opponentChips <= 0)
		{
			ShowMessage("YOU WIN - Opponent ran out of chips!");
			GD.Print("GAME OVER - Opponent has no chips");
			checkCallButton.Text = "GAME OVER";
			checkCallButton.Disabled = true;
			foldButton.Visible = false;
			betRaiseButton.Visible = false;
		}
		else if (playerChips <= 0)
		{
			ShowMessage("GAME OVER - You ran out of chips!");
			GD.Print("GAME OVER - Player has no chips");
			checkCallButton.Text = "GAME OVER";
			checkCallButton.Disabled = true;
			foldButton.Visible = false;
			betRaiseButton.Visible = false;
		}

		UpdateHud();
		RefreshBetSlider();
	}

	private void OnBetSliderValueChanged(double value)
	{
		int sliderValue = (int)Math.Round(value);

		var (minBet, maxBet) = GetLegalBetRange();
		sliderValue = Math.Clamp(sliderValue, minBet, maxBet);

		betAmount = sliderValue;
		betSlider.Value = betAmount; // snap back if user dragged outside
		UpdateButtonLabels();
	}
}
