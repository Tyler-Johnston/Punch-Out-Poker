using Godot;
using System.Collections.Generic;

public partial class GameState : RefCounted
{
	public List<Card> CommunityCards { get; set; } = new List<Card>();
	public float PotSize { get; set; }
	public float CurrentBet { get; set; }
	public float PreviousBet { get; set; }
	public Street Street { get; set; }
	public float BigBlind { get; set; }
	public int OpponentChipStack { get; set; }
	public bool IsAIInPosition { get; set; }
	
	// NEW: Tracks the human player's long-term tendencies
	public PlayerStats CurrentPlayerStats { get; set; }
	
	// Controlled via setters for Engine, read-only for AI decisions
	public bool CanAIReopenBetting { get; private set; }
	public int LastFullRaiseIncrement { get; private set; }
	
	private Dictionary<AIPokerPlayer, float> playerBets = new Dictionary<AIPokerPlayer, float>();
	
	public float GetPlayerCurrentBet(AIPokerPlayer player)
	{
		return playerBets.ContainsKey(player) ? playerBets[player] : 0f;
	}
	
	public void SetPlayerBet(AIPokerPlayer player, float amount)
	{
		playerBets[player] = amount;
	}
	
	public void SetCanAIReopenBetting(bool canReopen)
	{
		CanAIReopenBetting = canReopen;
	}
	
	public void SetLastFullRaiseIncrement(int increment)
	{
		LastFullRaiseIncrement = increment;
	}
	
	public void ResetBetsForNewStreet()
	{
		// When street changes, bets reset (usually dealt with in PokerGame, 
		// but state tracking helps AI know "PreviousBet" context if needed)
		// Note: Logic in PokerGame often handles setting CurrentBet to 0.
		PreviousBet = 0; 
		playerBets.Clear();
	}

	// ==========================================
	// UNIT TEST HELPERS
	// ==========================================
	
	/// <summary>
	/// HELPER FOR UNIT TESTS ONLY.
	/// Allows manual injection of game scenarios to test AI logic without running the full engine.
	/// </summary>
	public void SetupTestScenario(
		int currentBet, 
		int previousBet, 
		int potSize, 
		Street street, 
		int lastFullRaiseIncrement,
		int bigBlind,
		bool canAIReopen,
		PlayerStats mockStats = null) // Added optional mock stats parameter
	{
		this.CurrentBet = currentBet;
		this.PreviousBet = previousBet;
		this.PotSize = potSize;
		this.Street = street;
		this.LastFullRaiseIncrement = lastFullRaiseIncrement;
		this.BigBlind = bigBlind;
		this.CanAIReopenBetting = canAIReopen;
		
		// If tests don't provide stats, create an empty (neutral) baseline
		this.CurrentPlayerStats = mockStats ?? new PlayerStats(); 
	}
}
