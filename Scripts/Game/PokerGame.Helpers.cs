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
	
	private void ResetBettingRound()
	{
		// Move current round chips to display pot
		displayPot += playerChipsInPot + opponentChipsInPot;
		
		// Reset current round displays
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		
		// Reset betting amounts for new street
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		
		// Reset action flags
		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;
		UpdateHud();
		GD.Print($"[ResetBettingRound] New street - Total Pot: {pot}, Display Pot: {displayPot}");
	}

	private (int minBet, int maxBet) GetLegalBetRange()
	{
		int amountToCall = currentBet - playerBet;
		
		int maxBet = playerChips + playerBet;
		
		if (maxBet <= 0) return (0, 0);
		
		bool opening = currentBet == 0;
		int minBet;
		
		if (opening)
		{
			minBet = Math.Min(bigBlind, maxBet);
		}
		else
		{
			// Minimum raise increment (at least one big blind, or match last raise size)
			int minRaiseIncrement = Math.Max(amountToCall, bigBlind);
			
			// Minimum total bet = current bet + minimum raise increment
			minBet = currentBet + minRaiseIncrement;
			
			if (minBet > maxBet)
				minBet = maxBet;
		}
		
		if (minBet > maxBet) minBet = maxBet;
		return (minBet, maxBet);
	}

	private int CalculatePotSizeBet(float potMultiplier)
	{
		var (minBet, maxBet) = GetLegalBetRange();
		
		if (maxBet <= 0) return 0;
		
		int targetRaiseAmount = (int)Math.Round(pot * potMultiplier);
		return Math.Clamp(targetRaiseAmount, minBet, maxBet);
	}
	
	// --- POT HELPERS ---
	
	public void AddToPot(bool isPlayer, int amount)
	{
		pot += amount;
		if (isPlayer)
			playerContributed += amount;
		else
			opponentContributed += amount;
	}
	
	private bool ReturnUncalledChips()
	{
		if (playerContributed > opponentContributed)
		{
			int refund = playerContributed - opponentContributed;
			playerChips += refund;
			pot -= refund;
			playerContributed -= refund;
			
			GD.Print($"Side Pot: Returned {refund} uncalled chips to Player.");
			return true;
		}
		else if (opponentContributed > playerContributed)
		{
			int refund = opponentContributed - playerContributed;
			opponentChips += refund;
			pot -= refund;
			opponentContributed -= refund;
			
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
			PotSize = pot,
			CurrentBet = currentBet,
			Street = currentStreet,
			BigBlind = bigBlind,
			IsAIInPosition = DetermineAIPosition()
		};
		state.SetPlayerBet(aiOpponent, opponentBet);
		return state;
	}

}
