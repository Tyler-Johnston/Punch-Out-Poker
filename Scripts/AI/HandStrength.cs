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

	// Convert hand rank to 0-1 strength (UPDATED with better values)
	private float GetStrengthFromRank(int rank)
	{
		if (rank <= 10) return 1.0f;     // Straight flush
		if (rank <= 166) return 0.95f;   // Four of a kind
		if (rank <= 322) return 0.90f;   // Full house
		if (rank <= 1599) return 0.82f;  // Flush (increased from 0.8)
		if (rank <= 1609) return 0.77f;  // Straight (increased from 0.75)
		if (rank <= 2467) return 0.70f;  // Three of a kind (increased from 0.65)
		if (rank <= 3325) return 0.65f;  // Two pair (INCREASED from 0.5 - KEY FIX)
		if (rank <= 4000) return 0.42f;  // Strong one pair (increased from 0.35)
		if (rank <= 6185) return 0.28f;  // Weak one pair (increased from 0.2)
		return 0.12f;                    // High card (increased from 0.1)
	}

	// Board texture analysis with less harsh penalties
	private float AnalyzeBoardTexture(int handRank)
	{
		float modifier = 1.0f;

		// Check if board is paired
		bool boardPaired = IsBoardPaired();
		if (boardPaired)
		{
			if (handRank <= 322) modifier *= 1.12f;      // Full house or better
			else if (handRank <= 2467) modifier *= 1.08f; // Trips
			else if (handRank > 6185) modifier *= 0.82f;  // NEW: High card on paired board (very weak)
			else if (handRank <= 6185) modifier *= 0.90f; // Pairs
		}


		// Check if board has flush possibility
		int maxSuitCount = GetMaxSuitCount(communityCards);
		if (maxSuitCount >= 3)
		{
			if (handRank <= 1599) modifier *= 1.08f;  // We have flush - stronger
			else modifier *= 0.95f;                   // We don't have flush (less harsh, was 0.9)
		}

		// Check if board is connected (straight possibilities)
		bool boardConnected = IsBoardConnected();
		if (boardConnected)
		{
			if (handRank <= 1609) modifier *= 1.08f;      // We have straight - stronger
			else if (handRank <= 3325) modifier *= 0.96f; // Two pair on connected board (NEW)
			else modifier *= 0.94f;                       // Weaker hands (less harsh, was 0.92)
		}

		// Prevent extreme adjustments
		return Math.Clamp(modifier, 0.80f, 1.20f);
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

	// Enhanced connectivity detection including gappy boards
	private bool IsBoardConnected()
	{
		if (communityCards.Count < 3) return false;

		List<int> ranks = new List<int>();
		foreach (var card in communityCards)
		{
			int rank = (int)card.Rank;
			if (!ranks.Contains(rank)) ranks.Add(rank); // Remove duplicates
		}
		ranks.Sort();

		// Check for 3+ cards in exact sequence
		int consecutive = 1;
		for (int i = 1; i < ranks.Count; i++)
		{
			if (ranks[i] == ranks[i - 1] + 1)
			{
				consecutive++;
				if (consecutive >= 3) return true;
			}
			else
			{
				consecutive = 1;
			}
		}
		
		// Check for "gappy" dangerous boards (e.g., Q-9-8 or K-T-9)
		// 3 cards within 5 rank span = straight possible
		if (ranks.Count >= 3)
		{
			for (int i = 0; i < ranks.Count - 2; i++)
			{
				int span = ranks[i + 2] - ranks[i];
				if (span <= 4) return true;
			}
		}

		return false;
	}

	// Improved preflop evaluation with better gap penalties
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

		// Base value from cards (ADJUSTED - less high card emphasis)
		strength += (highRank / 12.0f) * 0.25f;  // Reduced from 0.3
		strength += (lowRank / 12.0f) * 0.18f;   // Increased from 0.15 (kicker matters more)

		// Suited bonus
		if (suited) strength += 0.08f;

		// Connectedness bonus (potential for straights)
		if (gap == 0) strength += 0.08f;      // Connectors (e.g., 9-8)
		else if (gap == 1) strength += 0.05f; // One-gapper (e.g., 9-7)
		else if (gap == 2) strength += 0.02f; // Two-gapper (e.g., 9-6)
		else if (gap >= 5) strength -= 0.08f; // NEW: Large gap penalty (e.g., Q-3, K-2)

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
