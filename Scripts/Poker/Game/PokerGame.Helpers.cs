using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	private PokerPersonality LoadOpponentPersonality(string opponentName)
	{
		return opponentName switch
		{
			"Steve"        => PersonalityPresets.CreateSteve(),
			"Aryll"        => PersonalityPresets.CreateAryll(),
			"Boy Wizard"   => PersonalityPresets.CreateBoyWizard(),
			"Apprentice"   => PersonalityPresets.CreateApprentice(),
			"Malandro"     => PersonalityPresets.CreateMalandro(),
			"Cowboy"       => PersonalityPresets.CreateCowboy(),
			"King"         => PersonalityPresets.CreateKing(),
			"Old Wizard"   => PersonalityPresets.CreateOldWizard(),
			"Akalite"      => PersonalityPresets.CreateAkalite(),
			_              => throw new ArgumentException($"Unknown opponent: {opponentName}")
		};
	}

	// --- LABEL HELPERS ---

	private void ShowMessage(string text)
	{
		gameStateLabel.Visible = !string.IsNullOrEmpty(text);
		gameStateLabel.Text = text;
	}

	private void UpdatePlayerStackLabels()
	{
		playerStackLabel.Text = $"Your Stack: ${playerChips}";
	}

	private void UpdateSessionProfitLabel()
	{
		int buyIn = GameManager.Instance.CurrentBuyIn;
		int profit = playerChips - buyIn;

		if (profit > 0)
		{
			playerEarningsLabel.Text = $"Net: +${profit}";
			playerEarningsLabel.Modulate = new Color("#4ade80");
		}
		else if (profit < 0)
		{
			playerEarningsLabel.Text = $"Net: -${Math.Abs(profit)}";
			playerEarningsLabel.Modulate = new Color("#f87171");
		}
		else
		{
			playerEarningsLabel.Text = "Net: $0";
			playerEarningsLabel.Modulate = Colors.White;
		}
	}

	// --- BETTING HELPERS ---

	private void ResetBettingRound()
	{
		potManager.SettleStreetIntoPot();

		// potManager now owns: CurrentBet, PreviousBet, LastRaiseAmount,
		// PlayerStreetBet, OpponentStreetBet, TotalContributed

		displayPot = potManager.MainPot;
		betAmount = 0;

		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;
		playerCanReopenBetting = true;
		opponentCanReopenBetting = true;

		UpdateHud();
		GameManager.LogVerbose($"[ResetBettingRound] New street - Total Pot: {potManager.MainPot}, Display Pot: {displayPot}");
	}

	private (int minBet, int maxBet) GetLegalBetRange()
	{
		// Max is everything the player has plus what they've already put in this street
		int maxBet = playerChips + potManager.PlayerStreetBet;

		if (maxBet <= 0) return (0, 0);

		int minRaiseTotal = PokerRules.CalculateMinRaiseTotal(
			potManager.CurrentBet,
			potManager.PreviousBet,
			potManager.LastRaiseAmount,
			bigBlind
		);

		int minBet = Math.Min(minRaiseTotal, maxBet);

		return (minBet, maxBet);
	}

	private int CalculatePotSizeBet(float potMultiplier)
	{
		var (minBet, maxBet) = GetLegalBetRange();
		if (maxBet <= 0) return 0;

		int targetRaiseAmount = (int)Math.Round(potManager.GetEffectivePot() * potMultiplier);
		return Math.Clamp(targetRaiseAmount, minBet, maxBet);
	}

	// --- OPPONENT CHIP HELPERS ---

	private void SetOpponentChips(int newAmount)
	{
		newAmount = Math.Max(0, newAmount);
		opponentChips = newAmount;
		if (aiOpponent != null) aiOpponent.ChipStack = newAmount;
	}

	private void AddOpponentChips(int delta)
	{
		if (delta == 0) return;
		SetOpponentChips(opponentChips + delta);
	}

	private void SpendOpponentChips(int amount)
	{
		if (amount <= 0) return;
		if (amount > opponentChips) amount = opponentChips;
		SetOpponentChips(opponentChips - amount);
	}

	// --- ASSERTION / AUDIT HELPERS ---

	private void AssertOpponentChipsSynced(string context)
	{
		if (aiOpponent == null) return;
		if (aiOpponent.ChipStack != opponentChips)
			GD.PrintErr($"[SYNC] Opponent chips desync in {context}: opponentChips={opponentChips}, aiOpponent.ChipStack={aiOpponent.ChipStack}");
	}

	private void VerifyTotalChips(string context)
	{
		int currentTotal = playerChips + opponentChips
			+ potManager.MainPot
			+ potManager.PlayerStreetBet
			+ potManager.OpponentStreetBet;

		GameManager.LogVerbose($"[AUDIT] {context}: Total In Play = ${currentTotal}");

		int expectedTotal = buyInAmount * 2;
		if (currentTotal != expectedTotal)
			GD.PrintErr($"[CRITICAL] Money Leak in {context}! Expected={expectedTotal}, Got={currentTotal}");
	}

	private void Assert(bool condition, string message)
	{
		if (!condition)
		{
			GD.PrintErr($"!!! CRITICAL LOGIC ERROR: {message} !!!");
			GD.Print($"State dump: P_Stack:{playerChips} O_Stack:{opponentChips} " +
					 $"MainPot:{potManager.MainPot} " +
					 $"P_StreetBet:{potManager.PlayerStreetBet} O_StreetBet:{potManager.OpponentStreetBet}");
			GetTree().Paused = true;
			throw new Exception($"Poker Logic Assertion Failed: {message}");
		}
	}

	// --- STATE HELPERS ---

	private bool IsGameOver()
	{
		if (isShowdownInProgress) return false;
		return playerChips <= 0 || opponentChips <= 0;
	}

	private GameState CreateGameState()
	{
		var state = new GameState
		{
			CommunityCards      = new List<Card>(communityCards),
			PotSize             = potManager.GetEffectivePot(),
			CurrentBet          = potManager.CurrentBet,
			PreviousBet         = potManager.PreviousBet,
			Street              = currentStreet,
			BigBlind            = bigBlind,
			IsAIInPosition      = DetermineAIPosition(),
			OpponentChipStack   = opponentChips,
			CurrentPlayerStats  = this.playerStats
		};

		state.SetPlayerBet(aiOpponent, potManager.OpponentStreetBet);
		state.SetCanAIReopenBetting(opponentCanReopenBetting);
		state.SetLastFullRaiseIncrement(potManager.LastRaiseAmount);

		return state;
	}

	private bool TryGetRandom(Godot.Collections.Dictionary<string, Godot.Collections.Array<string>> dict, string key, out string line)
	{
		line = "";
		if (!dict.TryGetValue(key, out var lines) || lines.Count == 0)
			return false;

		int idx = (int)GD.RandRange(0, lines.Count - 1);
		line = lines[idx].ToString();
		return true;
	}

	// --- AI HELPERS ---

	private bool DetermineAIPosition()
	{
		return currentStreet == Street.Preflop ? playerHasButton : !playerHasButton;
	}

	private float GetPatienceMultiplier(Patience patience)
	{
		return patience switch
		{
			Patience.VeryLow   => 0.5f,
			Patience.Low       => 0.75f,
			Patience.Average   => 1.0f,
			Patience.High      => 1.5f,
			Patience.VeryHigh  => 2.0f,
			_                  => 1.0f
		};
	}
}
