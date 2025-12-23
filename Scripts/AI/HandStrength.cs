// PokerGame.HandStrength.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	private float EvaluateAIHandStrength()
	{
		if (communityCards.Count == 0)
		{
			// Preflop hand strength
			return EvaluatePreflopStrength(opponentHand);
		}

		// Postflop - use your HandEvaluator
		int aiRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);
		float baseStrength = GetStrengthFromRank(aiRank);

		// Adjust for board texture
		float boardModifier = AnalyzeBoardTexture(aiRank);

		return Math.Clamp(baseStrength * boardModifier, 0.05f, 1.0f);
	}

	// Convert hand rank to 0-1 strength (using your HandEvaluator thresholds)
	private float GetStrengthFromRank(int rank)
	{
		if (rank <= 10) return 1.0f;     // Straight flush
		if (rank <= 166) return 0.95f;   // Four of a kind
		if (rank <= 322) return 0.9f;    // Full house
		if (rank <= 1599) return 0.8f;   // Flush
		if (rank <= 1609) return 0.75f;  // Straight
		if (rank <= 2467) return 0.65f;  // Three of a kind
		if (rank <= 3325) return 0.5f;   // Two pair
		if (rank <= 4000) return 0.35f;  // Strong one pair
		if (rank <= 6185) return 0.2f;   // Weak one pair
		return 0.1f;                     // High card
	}

	// Board texture analysis for better hand evaluation
	private float AnalyzeBoardTexture(int handRank)
	{
		float modifier = 1.0f;

		// Check if board is paired (increases value of trips/boats, decreases value of pairs)
		bool boardPaired = IsBoardPaired();
		if (boardPaired)
		{
			if (handRank <= 322) modifier *= 1.15f;      // Full house or better - stronger
			else if (handRank <= 2467) modifier *= 1.05f; // Trips - slightly stronger
			else if (handRank <= 6185) modifier *= 0.85f; // Pairs - weaker (opponent could have trips)
		}

		// Check if board has flush possibility
		int maxSuitCount = GetMaxSuitCount(communityCards);
		if (maxSuitCount >= 3)
		{
			if (handRank <= 1599) modifier *= 1.1f;  // We have flush - stronger
			else modifier *= 0.9f;                   // We don't have flush - weaker
		}

		// Check if board is connected (straight possibilities)
		bool boardConnected = IsBoardConnected();
		if (boardConnected)
		{
			if (handRank <= 1609) modifier *= 1.1f;  // We have straight - stronger
			else modifier *= 0.92f;                  // We don't - weaker
		}

		return modifier;
	}

	// Helper methods for board texture analysis
	private bool IsBoardPaired()
	{
		if (communityCards.Count < 2) return false;

		for (int i = 0; i < communityCards.Count; i++)
		{
			for (int j = i + 1; j < communityCards.Count; j++)
			{
				if (communityCards[i].Rank == communityCards[j].Rank)
					return true;
			}
		}
		return false;
	}

	private int GetMaxSuitCount(List<Card> cards)
	{
		int[] suitCounts = new int[4];
		foreach (var card in cards)
		{
			suitCounts[(int)card.Suit]++;
		}
		return Math.Max(Math.Max(suitCounts[0], suitCounts[1]),
						Math.Max(suitCounts[2], suitCounts[3]));
	}

	private bool IsBoardConnected()
	{
		if (communityCards.Count < 3) return false;

		List<int> ranks = new List<int>();
		foreach (var card in communityCards)
		{
			ranks.Add((int)card.Rank);
		}
		ranks.Sort();

		// Check for 3+ cards in sequence
		int consecutive = 1;
		for (int i = 1; i < ranks.Count; i++)
		{
			if (ranks[i] == ranks[i - 1] + 1)
			{
				consecutive++;
				if (consecutive >= 3) return true;
			}
			else if (ranks[i] != ranks[i - 1]) // Not a pair
			{
				consecutive = 1;
			}
		}

		return false;
	}

	// Improved preflop evaluation with equity-based values
	private float EvaluatePreflopStrength(List<Card> hand)
	{
		if (hand.Count != 2) return 0.5f;

		int rank1 = (int)hand[0].Rank;
		int rank2 = (int)hand[1].Rank;
		bool suited = hand[0].Suit == hand[1].Suit;
		bool paired = rank1 == rank2;
		int highRank = Math.Max(rank1, rank2);
		int lowRank = Math.Min(rank1, rank2);
		int gap = highRank - lowRank;

		float strength = 0.0f;

		// Base value from high card
		strength += (highRank / 12.0f) * 0.3f;  // Ace=13 gives ~0.325
		strength += (lowRank / 12.0f) * 0.15f;  // Kicker value

		// Pocket pair bonus (equity-based)
		if (paired)
		{
			if (highRank >= 12) strength = 0.95f;      // AA
			else if (highRank >= 11) strength = 0.92f; // KK
			else if (highRank >= 10) strength = 0.88f; // QQ
			else if (highRank >= 9) strength = 0.84f;  // JJ
			else if (highRank >= 8) strength = 0.78f;  // TT
			else if (highRank >= 6) strength = 0.70f;  // 99-77
			else if (highRank >= 4) strength = 0.62f;  // 66-55
			else strength = 0.55f;                     // 44-22
			return strength;
		}

		// Suited bonus
		if (suited) strength += 0.08f;

		// Connectedness bonus (potential for straights)
		if (gap == 0) strength += 0.08f;      // Connectors (e.g., 9-8)
		else if (gap == 1) strength += 0.05f; // One-gapper (e.g., 9-7)
		else if (gap == 2) strength += 0.02f; // Two-gapper (e.g., 9-6)

		// Premium hand adjustments
		if (highRank >= 12 && lowRank >= 11)       // AK, AQ, KQ
		{
			strength = suited ? 0.85f : 0.80f;
		}
		else if (highRank >= 12 && lowRank >= 10)  // AJ, KJ, QJ
		{
			strength = suited ? 0.75f : 0.68f;
		}

		return Math.Clamp(strength, 0.15f, 0.95f);
	}
}
