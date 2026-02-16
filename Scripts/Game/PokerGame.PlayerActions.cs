using Godot;
using System;

public partial class PokerGame
{
	//  MAIN ACTION BUTTONS 
	private void OnFoldPressed()
	{
		if (!CanPlayerAct()) return;

		PerformPlayerAction(() =>
		{
			ShowMessage("You fold");
			GD.Print("Player folds");

			int winAmount = GetEffectivePot();
			AddOpponentChips(winAmount);
			RefreshAllInFlagsFromStacks();

			string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
			lastHandDescription = $"You: {playerHandName} VS {currentOpponentName}: ???";

			ClearPotTracking();
			aiOpponent.ProcessHandResult(HandResult.Win, winAmount, bigBlind);
			
			GameManager.LogVerbose($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
			EndHand();
		}, advanceTurn: false);
	}

	private void OnCheckCallPressed()
	{
		if (!CanPlayerAct()) return;

		PerformPlayerAction(() =>
		{
			int toCall = currentBet - playerBet;

			if (toCall == 0)
			{
				ApplyAction(isPlayer: true, action: PlayerAction.Check);
				ShowMessage("You check");
				GD.Print("> Player checks");
				sfxPlayer.PlaySound("check");
			}
			else
			{
				HandleCallAction();
			}
		});
	}

	private void OnBetRaisePressed()
	{
		if (!CanPlayerAct()) return;

		PerformPlayerAction(() =>
		{
			var (minBet, maxBet) = GetLegalBetRange();
			betAmount = Math.Clamp(betAmount, minBet, maxBet);
			
			var result = ApplyAction(isPlayer: true, action: PlayerAction.Raise, raiseToTotal: betAmount);
			
			int chipsAdded = Math.Max(0, result.AmountMoved);
			playerBetOnStreet[currentStreet] = true;
			playerBetSizeOnStreet[currentStreet] = chipsAdded;
			playerTotalBetsThisHand++;

			LogBetRaiseMessage(result, chipsAdded);
			sfxPlayer.PlayRandomChip();
		});
	}

	//  BET SIZING HELPERS 
	private void OnPotSizeButtonPressed(float potMultiplier)
	{
		if (!CanPlayerAct()) return;

		var (minBet, maxBet) = GetLegalBetRange();
		if (maxBet <= 0) return;

		int targetBet = CalculatePotSizeBet(potMultiplier);
		UpdateBetSlider(targetBet);
		GameManager.LogVerbose($"Set bet to {potMultiplier:P0} pot: {targetBet}");
	}

	private void OnAllInButtonPressed()
	{
		if (!CanPlayerAct()) return;

		var (minBet, maxBet) = GetLegalBetRange();
		if (maxBet <= 0) return;

		UpdateBetSlider(maxBet);
		GameManager.LogVerbose($"Set bet to ALL-IN: {maxBet}");
	}

	private void OnCashOutPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/CharacterSelect.tscn");
	}

	private void OnNextHandPressed()
	{
		if (!waitingForNextGame || IsGameOver()) return;
		StartNewHand();
	}

	//  PRIVATE HELPER METHODS 
	private void PerformPlayerAction(Action actionLogic, bool advanceTurn = true)
	{
		playerHasActedThisStreet = true;
		actionLogic();

		if (!advanceTurn) return;

		isPlayerTurn = false;
		UpdateHud();
		CheckBettingRoundComplete();
		ResetPlayerWaitTime();
	}

	private void HandleCallAction()
	{
		var result = ApplyAction(isPlayer: true, action: PlayerAction.Call);

		if (result.AmountMoved < 0)
		{
			int refund = -result.AmountMoved;
			ShowMessage($"You take back {refund} chips (Match All-In)");
			GameManager.LogVerbose($"> Player matched All-In. Refunded ${refund}.");
			sfxPlayer.PlayRandomChip();
		}
		else if (result.AmountMoved > 0)
		{
			string allInTag = (result.BecameAllIn || playerChips == 0) ? " (ALL-IN!)" : "";
			ShowMessage($"You call ${result.AmountMoved}{allInTag}");
			GD.Print($"> Player calls ${result.AmountMoved}{allInTag}");
			sfxPlayer.PlayRandomChip();
		}
		else
		{
			ShowMessage("You check");
			sfxPlayer.PlaySound("check");
			GameManager.LogVerbose($"> Player checks (call moved 0 chips)");
		}
	}

	private void LogBetRaiseMessage(ActionApplyResult result, int chipsAdded)
	{
		if (result.BecameAllIn || playerChips == 0)
		{
			ShowMessage($"You raise to ${playerBet} (ALL-IN!)");
			GD.Print($"> Player raises to ${playerBet} (ALL-IN)");
		}
		else if (!result.IsBet)
		{
			ShowMessage($"You raise to ${playerBet}");
			GD.Print($"> Player raises to ${playerBet}");
		}
		else
		{
			ShowMessage($"You bet ${chipsAdded}");
			GD.Print($"> Player bets ${chipsAdded}");
		}
	}

	private void CheckBettingRoundComplete()
	{
		bool betsEqual = (playerBet == opponentBet);
		bool bothActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool bothAllIn = playerIsAllIn && opponentIsAllIn;
		
		bool playerCannotAct = playerIsAllIn || !playerCanReopenBetting;
		bool opponentCannotAct = opponentIsAllIn || !opponentCanReopenBetting;
		
		GameManager.LogVerbose($"[Round Check] betsEqual={betsEqual}, bothActed={bothActed}");

		bool isRoundComplete = false;

		if (bothAllIn) 
			isRoundComplete = true;
		else if (betsEqual && bothActed && playerCannotAct && opponentCannotAct)
			isRoundComplete = true;
		else if (playerIsAllIn && !playerCanReopenBetting && bothActed)
			isRoundComplete = true;
		else if (opponentIsAllIn && (betsEqual || !opponentCanReopenBetting))
			isRoundComplete = true;

		if (isRoundComplete)
		{
			GameManager.LogVerbose("Betting round complete.");
			GetTree().CreateTimer(0.8).Timeout += AdvanceStreet;
		}
		else
		{
			GetTree().CreateTimer(1.2).Timeout += CheckAndProcessAITurn;
		}
	}

	private bool CanPlayerAct()
	{
		return handInProgress && isPlayerTurn;
	}

	private void UpdateBetSlider(int amount)
	{
		betAmount = amount;
		betSlider.Value = amount;
		UpdateButtonLabels();
	}
}
