// HandNotation.cs
using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Utilities for converting between Card objects and poker notation (e.g., "AKs", "77", "Q9o")
/// </summary>
public static class HandNotation
{
	/// <summary>
	/// Converts a 2-card hand to poker notation.
	/// </summary>
	/// <param name="hand">List of exactly 2 cards</param>
	/// <returns>Hand string like "AKs", "77", "Q9o", or "XX" if invalid</returns>
	public static string GetHandString(List<Card> hand)
	{
		if (hand == null || hand.Count != 2) return "XX";

		char rank1 = RankToChar(hand[0].Rank);
		char rank2 = RankToChar(hand[1].Rank);

		// Ensure higher rank comes first
		if ((int)hand[1].Rank > (int)hand[0].Rank)
		{
			(rank1, rank2) = (rank2, rank1);
		}

		if (hand[0].Rank == hand[1].Rank)
			return $"{rank1}{rank2}"; // Pair: "AA", "KK"
		else if (hand[0].Suit == hand[1].Suit)
			return $"{rank1}{rank2}s"; // Suited: "AKs"
		else
			return $"{rank1}{rank2}o"; // Offsuit: "AKo"
	}

	/// <summary>
	/// Converts a hand notation string to actual Card objects from available deck.
	/// </summary>
	/// <param name="handStr">Hand notation like "AKs", "77", "Q9o"</param>
	/// <param name="availableDeck">Deck to sample cards from</param>
	/// <returns>List of 2 cards, or null if not possible</returns>
	public static List<Card> ConvertHandStringToCards(string handStr, List<Card> availableDeck)
	{
		if (handStr.Length < 2) return null;

		char rank1Char = handStr[0];
		char rank2Char = handStr[1];
		bool isSuited = handStr.EndsWith("s");
		bool isPair = rank1Char == rank2Char;

		Rank rank1 = CharToRank(rank1Char);
		Rank rank2 = CharToRank(rank2Char);

		var card1Options = availableDeck.Where(c => c.Rank == rank1).ToList();
		var card2Options = availableDeck.Where(c => c.Rank == rank2 && c != card1Options.FirstOrDefault()).ToList();

		if (card1Options.Count == 0 || card2Options.Count == 0) return null;

		Card card1;
		Card card2;

		if (isPair)
		{
			if (card1Options.Count < 2) return null;
			card1 = card1Options[(int)(GD.Randf() * card1Options.Count)];
			card2Options = card1Options.Where(c => c != card1).ToList();
			if (card2Options.Count == 0) return null;
			card2 = card2Options[(int)(GD.Randf() * card2Options.Count)];
		}
		else if (isSuited)
		{
			foreach (var c1 in card1Options)
			{
				var matching = card2Options.Where(c2 => c2.Suit == c1.Suit).ToList();
				if (matching.Count > 0)
				{
					card1 = c1;
					card2 = matching[(int)(GD.Randf() * matching.Count)];
					return new List<Card> { card1, card2 };
				}
			}
			return null;
		}
		else // Offsuit
		{
			foreach (var c1 in card1Options)
			{
				var matching = card2Options.Where(c2 => c2.Suit != c1.Suit).ToList();
				if (matching.Count > 0)
				{
					card1 = c1;
					card2 = matching[(int)(GD.Randf() * matching.Count)];
					return new List<Card> { card1, card2 };
				}
			}
			return null;
		}

		return new List<Card> { card1, card2 };
	}

	/// <summary>
	/// Converts a character to a Rank enum.
	/// </summary>
	public static Rank CharToRank(char c)
	{
		switch (c)
		{
			case 'A': return Rank.Ace;
			case 'K': return Rank.King;
			case 'Q': return Rank.Queen;
			case 'J': return Rank.Jack;
			case 'T': return Rank.Ten;
			case '9': return Rank.Nine;
			case '8': return Rank.Eight;
			case '7': return Rank.Seven;
			case '6': return Rank.Six;
			case '5': return Rank.Five;
			case '4': return Rank.Four;
			case '3': return Rank.Three;
			case '2': return Rank.Two;
			default: return Rank.Two;
		}
	}

	/// <summary>
	/// Converts a Rank enum to a character.
	/// </summary>
	public static char RankToChar(Rank rank)
	{
		switch (rank)
		{
			case Rank.Ace: return 'A';
			case Rank.King: return 'K';
			case Rank.Queen: return 'Q';
			case Rank.Jack: return 'J';
			case Rank.Ten: return 'T';
			case Rank.Nine: return '9';
			case Rank.Eight: return '8';
			case Rank.Seven: return '7';
			case Rank.Six: return '6';
			case Rank.Five: return '5';
			case Rank.Four: return '4';
			case Rank.Three: return '3';
			case Rank.Two: return '2';
			default: return '?';
		}
	}
}
