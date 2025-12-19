using Godot;
using System;

public enum Suit { Clubs, Diamonds, Hearts, Spades }
public enum Rank { Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King, Ace }

public class Card
{
    public Suit Suit { get; }
    public Rank Rank { get; }
    
    public Card(Rank rank, Suit suit)
    {
        Rank = rank;
        Suit = suit;
    }
    
    // Convert to PokerHandEvaluator format (0-51)
    public int ToEvaluatorFormat()
    {
        return (int)Rank * 4 + (int)Suit;
    }
    
    // Display card as "A♠" or "7♥"
    public override string ToString()
    {
        string rankStr = Rank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            Rank.Ten => "10",
            _ => ((int)Rank + 2).ToString()
        };
        
        string suitStr = Suit switch
        {
            Suit.Spades => "♠",
            Suit.Hearts => "♥",
            Suit.Diamonds => "♦",
            Suit.Clubs => "♣",
            _ => ""
        };
        
        return $"{rankStr}{suitStr}";
    }
}
