using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class Deck
{
	private List<Card> cards = new List<Card>();
	private Random random = new Random();
	
	public Deck()
	{
		Reset();
	}
	
	// Create a fresh 52-card deck
	public void Reset()
	{
		cards.Clear();
		
		foreach (Suit suit in Enum.GetValues(typeof(Suit)))
		{
			foreach (Rank rank in Enum.GetValues(typeof(Rank)))
			{
				cards.Add(new Card(rank, suit));
			}
		}
		
		GameManager.LogVerbose($"Deck created with {cards.Count} cards");
	}
	
	// Shuffle the deck
	public void Shuffle()
	{
		// Fisher-Yates shuffle
		for (int i = cards.Count - 1; i > 0; i--)
		{
			int j = random.Next(i + 1);
			(cards[i], cards[j]) = (cards[j], cards[i]);
		}
		
		GameManager.LogVerbose("Deck shuffled!");
	}
	
	// Deal one card from the top
	public Card Deal()
	{
		if (cards.Count == 0)
		{
			GD.PrintErr("Deck is empty! Cannot deal.");
			return null;
		}
		
		Card card = cards[0];
		cards.RemoveAt(0);
		return card;
	}
	
	// How many cards left?
	public int CardsRemaining() => cards.Count;
	
	public static List<Card> CreateCardList()
	{
		List<Card> cards = new List<Card>();
		
		foreach (Suit suit in Enum.GetValues(typeof(Suit)))
		{
			foreach (Rank rank in Enum.GetValues(typeof(Rank)))
			{
				cards.Add(new Card(rank, suit));
			}
		}
		
		return cards;
	}
	
	public static void RemoveCardsFromDeck(List<Card> deck, List<Card> cardsToRemove)
	{
		if (cardsToRemove == null) return;
		
		foreach (var card in cardsToRemove)
		{
			deck.RemoveAll(c => c.Suit == card.Suit && c.Rank == card.Rank);
		}
	}

}
