using Godot;
using System.Collections.Generic;

public static class PokerUtilities
{
	public static HandRank ConvertPhevalRankToHandRank(int phevalRank)
	{
		if (phevalRank == 1) return HandRank.RoyalFlush;
		if (phevalRank <= 10) return HandRank.StraightFlush;
		if (phevalRank <= 166) return HandRank.FourOfAKind;
		if (phevalRank <= 322) return HandRank.FullHouse;
		if (phevalRank <= 1599) return HandRank.Flush;
		if (phevalRank <= 1609) return HandRank.Straight;
		if (phevalRank <= 2467) return HandRank.ThreeOfAKind;
		if (phevalRank <= 3325) return HandRank.TwoPair;
		if (phevalRank <= 6185) return HandRank.OnePair;
		return HandRank.HighCard;
	}
}

public static class PokerAIConfig
{
	public const float FLOP_BASE_THRESHOLD = 0.38f;
	public const float TURN_BASE_THRESHOLD = 0.48f;
	public const float RIVER_BASE_THRESHOLD = 0.54f;
	public const float PREFLOP_BASE_THRESHOLD = 0.42f;
	
	public const float FLOP_VALUE_THRESHOLD = 0.55f;
	public const float TURN_VALUE_THRESHOLD = 0.58f;
	public const float RIVER_VALUE_THRESHOLD = 0.62f;
	
	public const float FLOP_BLUFF_CEILING = 0.48f;
	public const float TURN_BLUFF_CEILING = 0.45f;
	public const float RIVER_BLUFF_CEILING = 0.42f;
	
	public const float FLOP_SIZE_BUMP = 0.025f;
	public const float LATER_STREET_SIZE_BUMP = 0.045f;

	public const float POT_ODDS_MULTIPLIER = 1.25f;
	public const float POT_ODDS_OVERRIDE_THRESHOLD = 0.40f;
	
	public const float OOP_VALUE_TIGHTEN = 0.03f;
	public const float OOP_BLUFF_REDUCE = 0.03f;
	
	public const float PREFLOP_BET_MULTIPLIER = 1.0f;
	public const float FLOP_BET_MULTIPLIER = 1.12f;
	public const float TURN_BET_MULTIPLIER = 1.10f;
	public const float RIVER_BET_MULTIPLIER = 1.15f;
	
	public const float SIZE_FACTOR_VALUE_ADJUST = 0.03f;
	public const float SIZE_FACTOR_BLUFF_ADJUST = 0.04f;
	
	public const float BLUFF_BASE_PROB = 0.55f;
	public const float BLUFF_AGGRESSION_WEIGHT = 0.35f;
	
	public const float TRAP_PROBABILITY = 0.08f;
	
	public const float VALUE_BET_BASE_FREQ = 0.70f;
	public const float VALUE_BET_AGGRESSION_WEIGHT = 0.20f;
}

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
	
	private Dictionary<AIPokerPlayer, float> playerBets = new Dictionary<AIPokerPlayer, float>();
	
	public float GetPlayerCurrentBet(AIPokerPlayer player)
	{
		return playerBets.ContainsKey(player) ? playerBets[player] : 0f;
	}
	
	public void SetPlayerBet(AIPokerPlayer player, float amount)
	{
		playerBets[player] = amount;
	}
	
	public void ResetBetsForNewStreet()
	{
		PreviousBet = CurrentBet;
		playerBets.Clear();
	}
}
