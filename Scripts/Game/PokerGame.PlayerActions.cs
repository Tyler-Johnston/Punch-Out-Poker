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
		opponentChips += pot;
		pot = 0;
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
   		EndHand();
	}

	private void OnCheckCallPressed()
	{
		// FIX: Handle "Next Hand" click safely to prevent button rotation bug
		if (!handInProgress)
		{
			// Prevent double-clicking
			checkCallButton.Disabled = true; 
			checkCallButton.Text = "Check"; // Reset label visually
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
		else if (toCall < 0)
		{
			// === Refund Scenario (Uncalled Bet Return) ===
			// This happens when we bet X, but Opponent is All-In for Y (where Y < X).
			// The game state treats this as us "calling" the all-in, but since we bet MORE,
			// we technically get the difference back immediately.
			
			int refundAmount = Math.Abs(toCall);
			
			// Refund the chips to stack
			playerChips += refundAmount;
			playerBet -= refundAmount; // Reduce our active bet to match opponent's all-in
			
			// Remove from pot/contribution tracking (passing negative value)
			AddToPot(true, -refundAmount);

			playerIsAllIn = false; // We clearly have chips back now
			
			ShowMessage($"You take back {refundAmount} excess chips (Match All-In)");
			GD.Print($"Player matched All-In. Refunded {refundAmount} chips.");
			chipsAudioPlayer.Play();
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
			chipsAudioPlayer.Play();
		}

		isPlayerTurn = false;
		UpdateHud();
		RefreshBetSlider();

		// Check for end of betting round
		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;
		
		// If opponent is All-In, we don't need 'bothPlayersActed' to be true if we just matched them
		if ((betsAreEqual && bothPlayersActed) || (opponentIsAllIn && betsAreEqual) || bothAllIn)
		{
			GD.Print($"Betting round complete after player action: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}, bothAllIn={bothAllIn}");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (opponentIsAllIn && !betsAreEqual)
		{
			// Edge Case: Opponent All-In but we somehow still have a discrepancy?
			// This block usually shouldn't be reached if the Refund logic above works,
			// but good to keep as a failsafe for the engine.
			GD.Print($"Betting round complete: Opponent all-in, cannot match player bet");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else
		{
			// Standard play: we checked/called, now it's AI's turn
			GetTree().CreateTimer(1.2).Timeout += CheckAndProcessAITurn;
		}
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

		// Calculate the chips to put in
		// If UI slider is "Raise To" (Total Bet):
		// int totalBet = betAmount;
		// int toAdd = totalBet - playerBet;
		
		// If UI slider is "Raise By" (Increment):
		// This assumes your slider returns the INCREMENT amount.
		int raiseAmount = betAmount;
		int totalBet = currentBet + raiseAmount;
		int toAdd = totalBet - playerBet;
		
		int actualBet = Math.Min(toAdd, playerChips);

		playerChips -= actualBet;
		playerBet += actualBet;
		
		// Use helper to track contributions
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
		chipsAudioPlayer.Play();

		isPlayerTurn = false;
		UpdateHud();
		RefreshBetSlider();

		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;

		if ((betsAreEqual && bothPlayersActed) || (opponentIsAllIn && betsAreEqual) || bothAllIn)
		{
			GD.Print($"Betting round complete after player action: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}, bothAllIn={bothAllIn}");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else if (opponentIsAllIn && !betsAreEqual)
		{
			// Opponent is all-in, cannot match player's larger bet
			GD.Print($"Betting round complete: Opponent all-in, cannot match player bet");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else
		{
			// Normal case: player bet/raised, AI needs to respond
			GetTree().CreateTimer(1.2).Timeout += CheckAndProcessAITurn;
		}
	}

	private void OnBetSliderValueChanged(double value)
	{
		int sliderValue = (int)Math.Round(value);

		var (minBet, maxBet) = GetLegalBetRange();
		sliderValue = Math.Clamp(sliderValue, minBet, maxBet);

		betAmount = sliderValue;
		betSlider.Value = betAmount;
		UpdateButtonLabels();
	}
}
