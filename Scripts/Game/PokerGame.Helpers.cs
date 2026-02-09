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
		
		gameStatePanel.Visible = !string.IsNullOrEmpty(text);
		gameStateLabel.Text = text;
	}

	
	private void UpdatePlayerStackLabels()
	{
		string text = $"Money In-Hand: ${playerChips}";
		playerStackLabel.Text = text;
		playerStackLabel2.Text = text;
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
	
	private int GetEffectivePot()
	{
		return pot + playerChipsInPot + opponentChipsInPot;
	}

	private void ResetBettingRound()
	{
		SettleStreetIntoPot();
		
		// Reset betting amounts for new street
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		previousBet = 0; 
		displayPot = pot;
	 	lastRaiseAmount = 0; 
		
		// Reset action flags
		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;
		playerCanReopenBetting = true;  // NEW: reset reopening flags
		opponentCanReopenBetting = true;
		UpdateHud();
		GD.Print($"[ResetBettingRound] New street - Total Pot: {pot}, Display Pot: {displayPot}");
	}

	private (int minBet, int maxBet) GetLegalBetRange()
	{
		int maxBet = playerChips + playerBet;
		
		if (maxBet <= 0) return (0, 0);
		
		bool opening = currentBet == 0;
		int minBet;
		
		if (opening)
		{
			// Opening bet - minimum is big blind
			minBet = Math.Min(bigBlind, maxBet);
		}
		else
		{
			// Calculate the LAST RAISE INCREMENT (not the full bet)
			int lastRaiseAmount = currentBet - previousBet;
			
			// Minimum raise must match the last raise size (min one BB)
			int minRaiseIncrement = Math.Max(lastRaiseAmount, bigBlind);
			
			// Minimum total bet = current bet + raise increment
			minBet = currentBet + minRaiseIncrement;
			
			// Allow all-in for less
			if (minBet > maxBet)
				minBet = maxBet;
		}
		
		return (minBet, maxBet);
	}

	private int CalculatePotSizeBet(float potMultiplier)
	{
		var (minBet, maxBet) = GetLegalBetRange();
		
		if (maxBet <= 0) return 0;
		
		int targetRaiseAmount = (int)Math.Round(GetEffectivePot() * potMultiplier);
		return Math.Clamp(targetRaiseAmount, minBet, maxBet);
	}
	
	// --- POT HELPERS ---

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
		if (amount > opponentChips) amount = opponentChips; // safety
		SetOpponentChips(opponentChips - amount);
	}

	private void AssertOpponentChipsSynced(string context)
	{
		if (aiOpponent == null) return;
		if (aiOpponent.ChipStack != opponentChips)
			GD.PrintErr($"[SYNC] Opponent chips desync in {context}: opponentChips={opponentChips}, aiOpponent.ChipStack={aiOpponent.ChipStack}");
	}

	public void CommitToStreetPot(bool isPlayer, int amount)
	{
		if (amount <= 0) return;

		if (isPlayer)
		{
			playerChipsInPot += amount;
			playerContributed += amount;
		}
		else
		{
			opponentChipsInPot += amount;
			opponentContributed += amount;
		}
	}
	
	private void SettleStreetIntoPot()
	{
		pot += playerChipsInPot + opponentChipsInPot;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
	}
	
	private void UncommitFromStreetPot(bool isPlayer, int amount)
	{
		if (amount <= 0) return;

		if (isPlayer)
		{
			playerChipsInPot = Math.Max(0, playerChipsInPot - amount);
			playerContributed = Math.Max(0, playerContributed - amount);
		}
		else
		{
			opponentChipsInPot = Math.Max(0, opponentChipsInPot - amount);
			opponentContributed = Math.Max(0, opponentContributed - amount);
		}
	}
	
	private bool ReturnUncalledChips()
	{
		if (playerContributed > opponentContributed)
		{
			int refund = playerContributed - opponentContributed;

			AddPlayerChips(refund);
			pot -= refund;
			playerContributed -= refund;

			RefreshAllInFlagsFromStacks();
			GD.Print($"Side Pot: Returned {refund} uncalled chips to Player.");
			return true;
		}
		else if (opponentContributed > playerContributed)
		{
			int refund = opponentContributed - playerContributed;

			AddOpponentChips(refund);
			pot -= refund;
			opponentContributed -= refund;

			RefreshAllInFlagsFromStacks();
			GD.Print($"Side Pot: Returned {refund} uncalled chips to Opponent.");
			return true;
		}

		return false;
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
			CommunityCards = new List<Card>(communityCards),
			PotSize = GetEffectivePot(),
			CurrentBet = currentBet,
			PreviousBet = previousBet,
			Street = currentStreet,
			BigBlind = bigBlind,
			IsAIInPosition = DetermineAIPosition(),
			OpponentChipStack = opponentChips
		};
		
		state.SetPlayerBet(aiOpponent, opponentBet);
		state.SetCanAIReopenBetting(opponentCanReopenBetting);
		
		return state;
	}

	
	// -- AI HELPERS --
	
	private PlayerAction DecideAIAction(GameState gameState)
	{
		return aiOpponent.MakeDecision(gameState);
	}
	
	private bool DetermineAIPosition()
	{
		if (currentStreet == Street.Preflop)
		{
			return playerHasButton;
		}
		else
		{
			return !playerHasButton;
		}
	}

}
