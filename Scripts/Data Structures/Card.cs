using Godot;
using System;

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
			Rank.Nine => "9",
			Rank.Eight => "8",
			Rank.Seven => "7",
			Rank.Six => "6",
			Rank.Five => "5",
			Rank.Four => "4",
			Rank.Three => "3",
			Rank.Two => "2",
			_ => "?"
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
	
	public string GetRankSymbol()
	{
		return Rank switch
		{
			Rank.Ace => "A",
			Rank.King => "K",
			Rank.Queen => "Q",
			Rank.Jack => "J",
			Rank.Ten => "10",
			Rank.Nine => "9",
			Rank.Eight => "8",
			Rank.Seven => "7",
			Rank.Six => "6",
			Rank.Five => "5",
			Rank.Four => "4",
			Rank.Three => "3",
			Rank.Two => "2",
			_ => "?"
		};
	}
	
	public string GetSuitSymbol()
	{
		return Suit switch
		{
			Suit.Spades => "♠",
			Suit.Hearts => "♥",
			Suit.Diamonds => "♦",
			Suit.Clubs => "♣",
			_ => ""
		};
	}
}
