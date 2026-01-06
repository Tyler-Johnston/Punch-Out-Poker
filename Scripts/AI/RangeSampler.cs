// RangeSampler.cs
using Godot;
using System.Collections.Generic;

/// <summary>
/// Utilities for sampling poker hands from predefined ranges.
/// </summary>
public static class RangeSampler
{
	/// <summary>
	/// Samples a random hand from a given range.
	/// </summary>
	/// <param name="availableDeck">Cards available to sample from</param>
	/// <param name="range">Range of hand notations to sample from</param>
	/// <returns>2 cards matching a hand from the range, or null if not possible</returns>
	public static List<Card> SampleHandFromRange(List<Card> availableDeck, List<string> range)
	{
		if (range.Count == 0) return null;

		// Try up to 50 times to find a valid hand
		for (int attempt = 0; attempt < 50; attempt++)
		{
			string handStr = range[(int)(GD.Randf() * range.Count)];
			List<Card> hand = HandNotation.ConvertHandStringToCards(handStr, availableDeck);
			
			if (hand != null && hand.Count == 2)
				return hand;
		}

		// Fallback: return any two random cards
		if (availableDeck.Count >= 2)
		{
			return new List<Card> { availableDeck[0], availableDeck[1] };
		}

		return null;
	}
}
