// PokerGame.UI.cs
using Godot;
using System;

public partial class PokerGame
{
	private void UpdateButtonLabels()
	{
		int toCall = currentBet - playerBet;
		var (minBet, maxBet) = GetLegalBetRange();

		bool allInOnly = (minBet == maxBet && maxBet == playerChips);
		bool sliderAllIn = (maxBet == playerChips && betAmount == maxBet);

		if (toCall == 0)
		{
			checkCallButton.TextureNormal = checkBtnImg;
			betRaiseButton.TextureNormal = betBtnImg;
			checkCallLabel.Text = "";

			if (allInOnly || sliderAllIn)
			{
				betRaiseLabel.Text = $"ALL IN: {maxBet}";
			}
			else
			{
				betRaiseLabel.Text = $"Bet: {betAmount}";
			}
		}
		else
		{
			checkCallLabel.Text = $"Call: {Math.Max(0, Math.Min(toCall, playerChips))}";
		  	checkCallButton.TextureNormal = callBtnImg;
			betRaiseButton.TextureNormal = raiseBtnImg;

			int raiseTotal = currentBet + betAmount;
			int toAddForRaise = raiseTotal - playerBet;

			if (allInOnly || sliderAllIn)
			{
				betRaiseLabel.Text = $"ALL IN ({maxBet})";
			}
			else
			{
				betRaiseLabel.Text = $"Raise: {toAddForRaise}";
			}
		}

		if (raisesThisStreet >= MAX_RAISES_PER_STREET && !waitingForNextGame)
		{
			betRaiseButton.Disabled = true;
		}
	}

	private void UpdateHud()
	{
		if (waitingForNextGame)
		{
			if (IsGameOver())
			{
				checkCallButton.Disabled = true;
			}
			else
			{
				checkCallButton.Disabled = false;
			}

			foldButton.Visible = false;
			betRaiseButton.Visible = false;
			betSlider.Visible = false;
			checkCallLabel.Visible = false;
			betRaiseLabel.Visible = false;
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
