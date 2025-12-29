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
			chipsAudioPlayer.Play();
		}

		isPlayerTurn = false;
		UpdateHud();
		RefreshBetSlider();

		// FIXED: Complete all-in handling
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
			// Betting continues - AI needs to act
			GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
		}
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

		// FIXED: Complete all-in handling
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
			GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
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

	private void ProcessAIAction()
	{
		if (!handInProgress) return;

		AIAction action = DecideAIAction();
		ExecuteAIAction(action);

		opponentHasActedThisStreet = true;

		// Check if hand ended (AI folded or won)
		if (!handInProgress) return;

		// FIXED: Complete all-in handling
		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;
		
		if ((betsAreEqual && bothPlayersActed) || (playerIsAllIn && betsAreEqual) || bothAllIn)
		{
			GD.Print($"Betting round complete: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}, bothAllIn={bothAllIn}");
			GetTree().CreateTimer(1.0).Timeout += AdvanceStreet;
		}
		else if (playerIsAllIn && !betsAreEqual)
		{
			// Player is all-in, AI raised over it - player cannot act
			GD.Print($"Betting round complete: Player all-in, cannot match AI raise");
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
}
