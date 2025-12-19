using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame : Node2D
{
	private Deck deck;
	private List<Card> playerHand = new List<Card>();
	private List<Card> opponentHand = new List<Card>();
	private List<Card> communityCards = new List<Card>();

	public override void _Ready()
	{
		GD.Print("=== Poker Game Started ===");
		
		// Create and shuffle deck
		deck = new Deck();
		deck.Shuffle();
		
		// Test dealing cards
		TestDealCards();
	}
	
	private void TestDealCards()
	{
		GD.Print("\n=== Dealing Cards ===");
		
		// Deal 2 cards to player
		playerHand.Add(deck.Deal());
		playerHand.Add(deck.Deal());
		
		GD.Print($"Player hand: {playerHand[0]}, {playerHand[1]}");
		
		// Deal 2 cards to opponent
		opponentHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());
		
		GD.Print($"Opponent hand: {opponentHand[0]}, {opponentHand[1]}");
		
		// Deal 5 community cards (flop, turn, river)
		GD.Print("\n=== Community Cards ===");
		
		// Flop (3 cards)
		communityCards.Add(deck.Deal());
		communityCards.Add(deck.Deal());
		communityCards.Add(deck.Deal());
		GD.Print($"Flop: {communityCards[0]}, {communityCards[1]}, {communityCards[2]}");
		
		// Turn (1 card)
		communityCards.Add(deck.Deal());
		GD.Print($"Turn: {communityCards[3]}");
		
		// River (1 card)
		communityCards.Add(deck.Deal());
		GD.Print($"River: {communityCards[4]}");
		
		GD.Print($"\nCards remaining in deck: {deck.CardsRemaining()}");
	}
}
