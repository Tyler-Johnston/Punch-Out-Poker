using Godot;

public static class PokerUtilities
{
	/// <summary>
	/// Convert pheval integer rank to HandRank enum
	/// Lower numbers = better hands in pheval
	/// </summary>
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
