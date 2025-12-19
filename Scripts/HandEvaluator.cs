using Godot;
using System.Collections.Generic;
using System.Linq;
using pheval;

public static class HandEvaluator
{
    // Convert your Card to pheval Card format
    private static pheval.Card ToPhevalCard(Card card)
    {
        // pheval uses: rank (0-12) * 4 + suit (0-3)
        byte id = (byte)card.ToEvaluatorFormat();
        return new pheval.Card(id);
    }
    
    // Evaluate a 7-card hand (2 hole + 5 community)
    public static int EvaluateHand(List<Card> holeCards, List<Card> communityCards)
    {
        var allCards = holeCards.Concat(communityCards).ToList();
        
        if (allCards.Count != 7)
        {
            GD.PrintErr($"Invalid hand size: {allCards.Count}. Need exactly 7 cards.");
            return int.MaxValue;
        }
        
        // Convert to pheval cards
        pheval.Card[] phevalCards = allCards.Select(c => ToPhevalCard(c)).ToArray();
        
        // Use Eval7Cards to evaluate the 7-card hand
        return Eval.Eval7Cards(phevalCards);
    }
    
	// Get hand name from rank (using our own logic)
	public static string GetHandName(int rank)
	{
		if (rank > 6185) return "High Card";        // 1277 high card
		if (rank > 3325) return "One Pair";         // 2860 one pair
		if (rank > 2467) return "Two Pair";         // 858 two pair
		if (rank > 1609) return "Three of a Kind";  // 858 three-kind
		if (rank > 1599) return "Straight";         // 10 straights
		if (rank > 322) return "Flush";             // 1277 flushes
		if (rank > 166) return "Full House";        // 156 full house
		if (rank > 10) return "Four of a Kind";     // 156 four-kind
		return "Straight Flush";                    // 10 straight-flushes
	}

    // Compare hands (lower rank = better in this library)
    public static int CompareHands(int playerRank, int opponentRank)
    {
        if (playerRank < opponentRank) return 1;  // Player wins
        if (playerRank > opponentRank) return -1; // Opponent wins
        return 0; // Tie
    }
}
