// NashEquilibrium.cs
using Godot;
using System.Collections.Generic;

/// <summary>
/// Nash Equilibrium push/fold calculator for short-stack scenarios.
/// Used when effective stack is 12BB or less in heads-up play.
/// </summary>
public static class NashEquilibrium
{
	/// <summary>
	/// Nash push/fold thresholds in big blinds.
	/// Key: Hand notation (e.g., "AKs", "77", "Q9o")
	/// Value: Maximum stack size in BB to profitably call an all-in
	/// </summary>
	private static readonly Dictionary<string, float> PushThresholds = new Dictionary<string, float>
	{
		// Premium pairs
		{"AA", 50f}, {"KK", 50f}, {"QQ", 50f}, {"JJ", 50f}, {"TT", 50f},
		{"99", 50f}, {"88", 50f}, {"77", 50f}, {"66", 11f}, {"55", 9f},
		{"44", 7f}, {"33", 6f}, {"22", 5f},
		
		// Suited aces
		{"AKs", 50f}, {"AQs", 50f}, {"AJs", 40f}, {"ATs", 50f}, {"A9s", 45f},
		{"A8s", 37f}, {"A7s", 32f}, {"A6s", 28f}, {"A5s", 37f}, {"A4s", 28f},
		{"A3s", 24f}, {"A2s", 24f},
		
		// Offsuit aces
		{"AKo", 50f}, {"AQo", 50f}, {"AJo", 35f}, {"ATo", 28f}, {"A9o", 22f},
		{"A8o", 18f}, {"A7o", 14f}, {"A6o", 11f}, {"A5o", 16f},
		
		// Suited kings
		{"KQs", 50f}, {"KJs", 45f}, {"KTs", 38f}, {"K9s", 30f}, {"K8s", 24f},
		{"K7s", 16f}, {"K6s", 14f}, {"K5s", 13f},
		
		// Offsuit kings
		{"KQo", 35f}, {"KJo", 28f}, {"KTo", 22f}, {"K9o", 16f},
		
		// Other broadway
		{"QJs", 45f}, {"QTs", 35f}, {"Q9s", 28f}, {"Q8s", 22f},
		{"JTs", 38f}, {"J9s", 28f}, {"J8s", 22f},
		{"T9s", 30f}, {"T8s", 24f},
		{"QJo", 30f}, {"QTo", 24f}, {"JTo", 24f},
		
		// Suited connectors
		{"98s", 24f}, {"87s", 20f}, {"76s", 18f}, {"65s", 16f}, {"54s", 14f}
	};

	/// <summary>
	/// Determines if a hand should call an all-in based on Nash equilibrium.
	/// </summary>
	/// <param name="handString">Hand notation (e.g., "AKs", "77", "Q9o")</param>
	/// <param name="effectiveStackBB">Effective stack size in big blinds</param>
	/// <returns>True if should call, false if should fold</returns>
	public static bool ShouldCallPush(string handString, float effectiveStackBB)
	{
		if (!PushThresholds.ContainsKey(handString))
		{
			GD.Print($"[NASH] Hand {handString} not in push table - FOLD by default");
			return false;
		}

		float threshold = PushThresholds[handString];
		bool shouldCall = effectiveStackBB <= threshold;

		if (shouldCall)
		{
			GD.Print($"[NASH] ✓ CALL {handString} at {effectiveStackBB:F1}BB (threshold: {threshold}BB)");
		}
		else
		{
			GD.Print($"[NASH] ✗ FOLD {handString} at {effectiveStackBB:F1}BB (needs ≤{threshold}BB)");
		}

		return shouldCall;
	}

	/// <summary>
	/// Gets the Nash push threshold for a specific hand.
	/// </summary>
	/// <param name="handString">Hand notation (e.g., "AKs", "77", "Q9o")</param>
	/// <returns>Maximum stack size in BB to profitably call, or -1 if hand not in table</returns>
	public static float GetPushThreshold(string handString)
	{
		return PushThresholds.ContainsKey(handString) ? PushThresholds[handString] : -1f;
	}

	/// <summary>
	/// Checks if Nash equilibrium applies to current situation.
	/// </summary>
	/// <param name="effectiveStackBB">Effective stack size in big blinds</param>
	/// <param name="isPreflop">Whether it's preflop</param>
	/// <param name="facingBet">Whether facing a bet</param>
	/// <returns>True if Nash equilibrium should be used</returns>
	public static bool ShouldUseNash(float effectiveStackBB, bool isPreflop, bool facingBet)
	{
		return effectiveStackBB <= 12f && isPreflop && facingBet;
	}

	/// <summary>
	/// Gets all hands that should call at a given stack depth.
	/// </summary>
	/// <param name="effectiveStackBB">Effective stack size in big blinds</param>
	/// <returns>List of hand notations that are profitable calls</returns>
	public static List<string> GetCallingRange(float effectiveStackBB)
	{
		List<string> callingRange = new List<string>();

		foreach (var kvp in PushThresholds)
		{
			if (effectiveStackBB <= kvp.Value)
			{
				callingRange.Add(kvp.Key);
			}
		}

		return callingRange;
	}

	/// <summary>
	/// Checks if a hand exists in the Nash push table.
	/// </summary>
	/// <param name="handString">Hand notation (e.g., "AKs", "77", "Q9o")</param>
	/// <returns>True if hand is in the push table</returns>
	public static bool HandExistsInTable(string handString)
	{
		return PushThresholds.ContainsKey(handString);
	}
}
