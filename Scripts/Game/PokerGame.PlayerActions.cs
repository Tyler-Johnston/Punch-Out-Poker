// PokerGame.PlayerActions.cs
// Step 1 (Settled-pot semantics):
// - Do NOT use AddToPot anywhere.
// - Use CommitToStreetPot(...) when chips move from stack to the middle during the street.
// - Use GetEffectivePot() for award amounts when a hand ends mid-street.

using Godot;
using System;

public partial class PokerGame
{
	// Helper: remove chips from current street pot + contributed totals (for uncalled bet refunds)
	// This is needed because you allow a "refund" path when toCall < 0.
	private void UncommitFromStreetPot(bool isPlayer, int amount)
	{
		if (amount <= 0) return;

		if (isPlayer)
		{
			playerChipsInPot = Math.Max(0, playerChipsInPot - amount);
			playerContributed = Math.Max(0, playerContributed - amount);
		}
		else
		{
			opponentChipsInPot = Math.Max(0, opponentChipsInPot - amount);
			opponentContributed = Math.Max(0, opponentContributed - amount);
		}
	}

	private void OnFoldPressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		playerHasActedThisStreet = true;

		ShowMessage("You fold");
		GD.Print("Player folds");

		// Step 1: opponent wins the effective pot (settled + current street commits)
		int winAmount = GetEffectivePot();

		AddOpponentChips(winAmount);
		
		// Clear all pot tracking for end of hand
		pot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		playerContributed = 0;
		opponentContributed = 0;

		aiOpponent.ProcessHandResult(HandResult.Win, winAmount, bigBlind);

		UpdateOpponentVisuals();
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		EndHand();
	}

	private void OnCheckCallPressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		playerHasActedThisStreet = true;
		int toCall = currentBet - playerBet;

		if (toCall == 0)
		{
			ShowMessage("You check");
			sfxPlayer.PlaySound("check");
			GD.Print($"Player checks on {currentStreet}");
		}
		else if (toCall < 0)
		{
			// Uncalled bet return (e.g., opponent all-in short and player had more invested this street)
			int refundAmount = Math.Abs(toCall);

			playerChips += refundAmount;
			playerBet -= refundAmount;

			// Step 1: undo current-street commit + contributed totals
			UncommitFromStreetPot(true, refundAmount);

			playerIsAllIn = false;

			ShowMessage($"You take back {refundAmount} excess chips (Match All-In)");
			GD.Print($"Player matched All-In. Refunded {refundAmount} chips.");
			sfxPlayer.PlayRandomChip();
		}
		else
		{
			// Normal call
			int actualCall = Math.Min(toCall, playerChips);
			playerChips -= actualCall;
			playerBet += actualCall;

			// Step 1: commit to current street pot only
			CommitToStreetPot(true, actualCall);

			if (playerChips == 0)
			{
				playerIsAllIn = true;
				ShowMessage($"You call ${actualCall} (ALL-IN!)");
				GD.Print($"Player calls {actualCall} (ALL-IN)");
			}
			else
			{
				ShowMessage($"You call ${actualCall}");
				GD.Print($"Player calls {actualCall}, Player stack: {playerChips}, EffectivePot: {GetEffectivePot()}");
			}

			sfxPlayer.PlayRandomChip();
		}

		isPlayerTurn = false;
		UpdateHud();
		UpdateOpponentVisuals(); // Ensure visuals are current
		RefreshBetSlider();

		// Check for end of betting round
		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;

		// 1. Standard Round End (Equal bets) OR Both All-In
		if ((betsAreEqual && bothPlayersActed) || bothAllIn)
		{
			GD.Print($"Betting round complete: Standard/Equal. betsEqual={betsAreEqual}");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		// 2. Player All-In AND Bets Equal (Standard Call All-In)
		else if (playerIsAllIn && betsAreEqual)
		{
			GD.Print($"Betting round complete: Player All-In (Matched).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		// 3. Player All-In (Short) - Calling for LESS than opponent bet
		else if (playerIsAllIn && playerBet < opponentBet)
		{
			GD.Print($"Betting round complete: Player All-In (Short/Partial Call).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		// 4. Opponent All-In (Short) - Calling for LESS than player bet
		else if (opponentIsAllIn && opponentBet < playerBet)
		{
			GD.Print($"Betting round complete: Opponent All-In (Short).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else
		{
			GetTree().CreateTimer(1.2).Timeout += CheckAndProcessAITurn;
		}
	}

	/// <summary>
	/// Handles the Next Hand button press between hands
	/// </summary>
	private void OnNextHandPressed()
	{
		if (!waitingForNextGame || IsGameOver()) return;
		StartNewHand();
	}

	private void OnBetRaisePressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		bool isRaise = currentBet > 0;

		playerHasActedThisStreet = true;

		// Ensure bet amount is valid
		var (minBet, maxBet) = GetLegalBetRange();
		betAmount = Math.Clamp(betAmount, minBet, maxBet);

		int totalBet = betAmount;         // raise-to total
		int toAdd = totalBet - playerBet; // how much player must add

		int actualBet = Math.Min(toAdd, playerChips);

		playerChips -= actualBet;
		playerBet += actualBet;

		// Step 1: commit to current street pot only
		CommitToStreetPot(true, actualBet);

		previousBet = currentBet;
		currentBet = playerBet;

		playerBetOnStreet[currentStreet] = true;
		playerBetSizeOnStreet[currentStreet] = actualBet;
		playerTotalBetsThisHand++;

		if (playerChips == 0)
		{
			playerIsAllIn = true;
			ShowMessage($"You raise to ${playerBet} (ALL-IN!)");
			GD.Print($"Player raises to {playerBet} (ALL-IN)");
		}
		else if (isRaise)
		{
			ShowMessage($"You raise to ${playerBet}");
			GD.Print($"Player raises to {playerBet}");
		}
		else
		{
			ShowMessage($"You bet ${actualBet}");
			GD.Print($"Player bets {actualBet}");
		}

		sfxPlayer.PlayRandomChip();

		isPlayerTurn = false;
		UpdateHud();
		UpdateOpponentVisuals();
		RefreshBetSlider();

		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;

		// 1. Standard Round End (Equal bets) OR Both All-In
		if ((betsAreEqual && bothPlayersActed) || bothAllIn)
		{
			GD.Print($"Betting round complete: Standard/Equal. betsEqual={betsAreEqual}");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		// 2. Player All-In AND Bets Equal (Standard Call All-In)
		else if (playerIsAllIn && betsAreEqual)
		{
			GD.Print($"Betting round complete: Player All-In (Matched).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		// 3. Player All-In (Short) - Calling for LESS than opponent bet
		else if (playerIsAllIn && playerBet < opponentBet)
		{
			GD.Print($"Betting round complete: Player All-In (Short/Partial Call).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		// 4. Opponent All-In (Short) - Calling for LESS than player bet
		else if (opponentIsAllIn && opponentBet < playerBet)
		{
			GD.Print($"Betting round complete: Opponent All-In (Short).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else
		{
			GetTree().CreateTimer(1.2).Timeout += CheckAndProcessAITurn;
		}
	}

	// --- POT-SIZED BET BUTTONS ---

	/// <summary>
	/// Handles pot-sized bet buttons (1/3, 1/2, 2/3, 1x pot)
	/// </summary>
	private void OnPotSizeButtonPressed(float potMultiplier)
	{
		if (!handInProgress || !isPlayerTurn) return;

		var (minBet, maxBet) = GetLegalBetRange();
		if (maxBet <= 0) return;

		int targetBet = CalculatePotSizeBet(potMultiplier);

		betAmount = targetBet;
		betSlider.Value = targetBet;

		UpdateButtonLabels();

		GD.Print($"Set bet to {potMultiplier:P0} pot: {targetBet}");
	}

	/// <summary>
	/// Handles the All-In button
	/// </summary>
	private void OnAllInButtonPressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		var (minBet, maxBet) = GetLegalBetRange();
		if (maxBet <= 0) return;

		// Set to maximum (all-in amount)
		betAmount = maxBet;
		betSlider.Value = maxBet;

		UpdateButtonLabels();

		GD.Print($"Set bet to ALL-IN: {maxBet}");
	}

	private void OnCashOutPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/CharacterSelect.tscn");
	}
}
