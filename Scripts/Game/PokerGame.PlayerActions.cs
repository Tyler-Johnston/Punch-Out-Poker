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

		GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
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

		// Proper betting round completion logic
		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool someoneAllIn = playerIsAllIn || opponentIsAllIn;

		// Round is complete when bets match AND both have acted, OR someone is all-in
		if ((betsAreEqual && bothPlayersActed) || someoneAllIn)
		{
			GD.Print($"Betting round complete: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}, allIn={someoneAllIn}");
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
