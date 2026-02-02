// PokerGame.PlayerActions.cs
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

		int winAmount = pot; 
		opponentChips += winAmount;
		aiOpponent.ChipStack = opponentChips;
		pot = 0;
		
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
			// === Uncalled Bet Return ===
			int refundAmount = Math.Abs(toCall);
			
			playerChips += refundAmount;
			playerBet -= refundAmount;
			
			// Pass negative value to remove from pot/contribution tracking
			AddToPot(true, -refundAmount);

			playerIsAllIn = false; // because we have the chips back now
			
			ShowMessage($"You take back {refundAmount} excess chips (Match All-In)");
			GD.Print($"Player matched All-In. Refunded {refundAmount} chips.");
			sfxPlayer.PlayRandomChip();
		}
		else
		{
			// Normal Call Logic
			int actualCall = Math.Min(toCall, playerChips);
			playerChips -= actualCall;
			playerBet += actualCall;
			
			AddToPot(true, actualCall);

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
		
		nextHandButton.Disabled = true;
		nextHandButton.Text = "Next Hand";
		StartNewHand();
	}

	private void OnBetRaisePressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		bool isRaise = currentBet > 0;
		if (isRaise && raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			ShowMessage("Maximum raises reached - can only call or fold");
			GD.Print("Max raises reached this street");
			return;
		}

		playerHasActedThisStreet = true;
		
		// Ensure bet amount is valid
		var (minBet, maxBet) = GetLegalBetRange();
		betAmount = Math.Clamp(betAmount, minBet, maxBet);

		int raiseAmount = betAmount;
		int totalBet = currentBet + raiseAmount;
		int toAdd = totalBet - playerBet;
		
		int actualBet = Math.Min(toAdd, playerChips);

		playerChips -= actualBet;
		playerBet += actualBet;
		
		AddToPot(true, actualBet);
		
		currentBet = playerBet;

		playerBetOnStreet[currentStreet] = true;
		playerBetSizeOnStreet[currentStreet] = actualBet;
		playerTotalBetsThisHand++;

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
		
		// Use the helper method to calculate raise amount
		int targetBet = CalculatePotSizeBet(potMultiplier);
		
		// Update slider and betAmount
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
		// Handle cashing out / returning to character select
		GetTree().ChangeSceneToFile("res://Scenes/CharacterSelect.tscn");
	}

}
