// PokerGame.PlayerActions.cs
using Godot;
using System;

public partial class PokerGame
{
	private void OnFoldPressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		playerHasActedThisStreet = true;
		TrackPlayerAction("Fold", 0, false);

		ShowMessage("You fold");
		GD.Print("Player folds");
		opponentChips += pot;
		pot = 0;
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
   		EndHand();
	}

	private void OnCheckCallPressed()
	{
		if (isMatchComplete)
		{
			GameManager.Instance.LastFacedOpponent = GameManager.Instance.SelectedOpponent;
			GetTree().ChangeSceneToFile("res://Scenes/CharacterSelect.tscn");
			return;
		}
		
		if (!handInProgress)
		{
			checkCallButton.Disabled = true; 
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
			TrackPlayerAction("Check", 0, false);
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
				TrackPlayerAction("Call", actualCall, true);
				GD.Print($"Player calls {actualCall} (ALL-IN)");
			}
			else
			{
				ShowMessage($"You call {actualCall} chips");
				TrackPlayerAction("Call", actualCall, false);
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
		// 5. CONTINUE BETTING (AI MUST ACT)
		// Includes: Player Raise All-In (playerBet > opponentBet)
		else
		{
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
			TrackPlayerAction("Raise", playerBet, true);
			GD.Print($"Player raises to {playerBet} (ALL-IN)");
		}
		else if (isRaise)
		{
			ShowMessage($"You raise to {playerBet} chips");
			TrackPlayerAction("Raise", playerBet, false);
			GD.Print($"Player raises to {playerBet}");
		}
		else
		{
			ShowMessage($"You bet {actualBet} chips");
			TrackPlayerAction("Bet", playerBet, false);
			GD.Print($"Player bets {actualBet}");
		}
		chipsAudioPlayer.Play();

		isPlayerTurn = false;
		UpdateHud();
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
		// 5. CONTINUE BETTING (AI MUST ACT)
		// Includes: Player Raise All-In (playerBet > opponentBet)
		else
		{
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
