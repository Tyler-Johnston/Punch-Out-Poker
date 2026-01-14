// PokerGame.UI.cs
using Godot;
using System;

public partial class PokerGame
{
	private void UpdateButtonLabels()
	{
		if (waitingForNextGame) return;
		
		int toCall = currentBet - playerBet;
		var (minBet, maxBet) = GetLegalBetRange();

		bool allInOnly = (minBet == maxBet && maxBet == playerChips);
		bool sliderAllIn = (maxBet == playerChips && betAmount == maxBet);

		if (toCall == 0)
		{
			checkCallButton.Text = "Check";

			if (allInOnly || sliderAllIn)
			{
				betRaiseButton.Text = $"ALL IN: {maxBet}";
			}
			else
			{
				// FIX: If currentBet > 0 (e.g. Big Blind option), it's technically a Raise
				if (currentBet > 0)
				{
					// Calculate total like the raise block
					int raiseTotal = currentBet + betAmount;
					betRaiseButton.Text = $"Raise to: {raiseTotal}";
				}
				else
				{
					// True opening bet (0 in pot)
					betRaiseButton.Text = $"Bet: {betAmount}";
				}
			}
		}
		else
		{
			// Check specifically for the negative call (Refund scenario)
			if (toCall < 0)
			{
				checkCallButton.Text = $"Call (Take back {Math.Abs(toCall)})";
			}
			else
			{
				checkCallButton.Text = $"Call: {Math.Min(toCall, playerChips)}";
			}

			int raiseTotal = currentBet + betAmount;
			int toAddForRaise = raiseTotal - playerBet;

			if (allInOnly || sliderAllIn)
			{
				betRaiseButton.Text = $"ALL IN ({maxBet})";
			}
			else
			{
				betRaiseButton.Text = $"Raise to: {raiseTotal}";
			}
		}

		if (raisesThisStreet >= MAX_RAISES_PER_STREET && !waitingForNextGame)
		{
			betRaiseButton.Disabled = true;
		}
	}

	private void UpdateHud()
	{
		if (isMatchComplete)
		{
			checkCallButton.Text = "Continue";
			checkCallButton.Disabled = false;
			foldButton.Visible = false;
			betRaiseButton.Visible = false;
			betSlider.Visible = false;
			betSliderLabel.Visible = false;
			potLabel.Visible = false;
			opponentDialogueLabel.Text = "";
			return;
		}
	
		if (waitingForNextGame)
		{
			if (IsGameOver())
			{
				checkCallButton.Disabled = true;
			}
			else
			{
				checkCallButton.Text = "Next Hand";
				checkCallButton.Disabled = false;
			}

			foldButton.Visible = false;
			betRaiseButton.Visible = false;
			betSlider.Visible = false;
			betSliderLabel.Visible = false;
			potLabel.Visible = false;
			opponentDialogueLabel.Text = "";
		}
		else
		{
			RefreshBetSlider();
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
		opponentStackLabel.Text = $"Opp: {opponentChips}";
		potLabel.Text = $"Pot: {pot}";
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

		betAmount = Math.Clamp(betAmount, minBet, maxBet);
		betSlider.Value = betAmount;
	}
}
