// PokerGame.PlayerActions.cs
// Step 1 (Settled-pot semantics):
// - Do NOT use AddToPot anywhere.
// - Use CommitToStreetPot(...) when chips move from stack to the middle during the street.
// - Use GetEffectivePot() for award amounts when a hand ends mid-street.

using Godot;
using System;

public partial class PokerGame
{
	private void OnFoldPressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		playerHasActedThisStreet = true;

		ShowMessage("You fold");
		GD.Print("Player folds");

		// Opponent wins the effective pot (settled + current street commits)
		int winAmount = GetEffectivePot();

		AddOpponentChips(winAmount);
		RefreshAllInFlagsFromStacks();

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

		// IMPORTANT: Decide check vs call based on current state BEFORE applying.
		int toCall = currentBet - playerBet;
		if (toCall == 0)
		{
			ApplyAction(isPlayer: true, action: PlayerAction.Check);

			ShowMessage("You check");
			sfxPlayer.PlaySound("check");
			GD.Print($"Player checks on {currentStreet}");
		}
		else
		{
			var result = ApplyAction(isPlayer: true, action: PlayerAction.Call);

			// Refund path (over-invested correction)
			if (result.AmountMoved < 0)
			{
				int refundAmount = -result.AmountMoved;
				ShowMessage($"You take back {refundAmount} excess chips (Match All-In)");
				GD.Print($"Player matched All-In. Refunded {refundAmount} chips.");
				sfxPlayer.PlayRandomChip();
			}
			// Normal call (including short all-in calls)
			else if (result.AmountMoved > 0)
			{
				if (result.BecameAllIn || playerChips == 0)
				{
					ShowMessage($"You call ${result.AmountMoved} (ALL-IN!)");
					GD.Print($"Player calls {result.AmountMoved} (ALL-IN)");
				}
				else
				{
					ShowMessage($"You call ${result.AmountMoved}");
					GD.Print($"Player calls {result.AmountMoved}, Player stack: {playerChips}, EffectivePot: {GetEffectivePot()}");
				}
				sfxPlayer.PlayRandomChip();
			}
			else
			{
				// Defensive: nothing moved, treat as check
				ShowMessage("You check");
				sfxPlayer.PlaySound("check");
				GD.Print($"Player checks on {currentStreet} (call produced 0 move)");
			}
		}

		isPlayerTurn = false;
		UpdateHud();
		UpdateOpponentVisuals();
		RefreshBetSlider();

		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;

		if ((betsAreEqual && bothPlayersActed) || bothAllIn)
		{
			GD.Print($"Betting round complete: Standard/Equal. betsEqual={betsAreEqual}");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (playerIsAllIn && betsAreEqual)
		{
			GD.Print($"Betting round complete: Player All-In (Matched).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (playerIsAllIn && playerBet < opponentBet)
		{
			GD.Print($"Betting round complete: Player All-In (Short/Partial Call).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
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

		playerHasActedThisStreet = true;

		var (minBet, maxBet) = GetLegalBetRange();
		betAmount = Math.Clamp(betAmount, minBet, maxBet);

		int totalBet = betAmount; // raise-to total

		var result = ApplyAction(isPlayer: true, action: PlayerAction.Raise, raiseToTotal: totalBet);

		// result.AmountMoved is how many chips actually left the player's stack into the street pot
		int actualBet = Math.Max(0, result.AmountMoved);

		playerBetOnStreet[currentStreet] = true;
		playerBetSizeOnStreet[currentStreet] = actualBet;
		playerTotalBetsThisHand++;

		if (result.BecameAllIn || playerChips == 0)
		{
			ShowMessage($"You raise to ${playerBet} (ALL-IN!)");
			GD.Print($"Player raises to {playerBet} (ALL-IN)");
		}
		else if (!result.IsBet)
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

		if ((betsAreEqual && bothPlayersActed) || bothAllIn)
		{
			GD.Print($"Betting round complete: Standard/Equal. betsEqual={betsAreEqual}");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (playerIsAllIn && betsAreEqual)
		{
			GD.Print($"Betting round complete: Player All-In (Matched).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (playerIsAllIn && playerBet < opponentBet)
		{
			GD.Print($"Betting round complete: Player All-In (Short/Partial Call).");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
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
