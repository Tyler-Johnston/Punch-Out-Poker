using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame : Node2D
{
	// Load the CardVisual scene
	private PackedScene cardVisualScene;
	
	private Deck deck;
	private List<Card> playerHand = new List<Card>();
	private List<Card> opponentHand = new List<Card>();
	private List<Card> communityCards = new List<Card>();
	
	// References to card position areas
	private Node2D opponentArea;
	private Node2D communityCardsArea;
	private Node2D playerArea;

	public override void _Ready()
	{
		GD.Print("=== Poker Game Started ===");
		
		// Load the CardVisual scene
		cardVisualScene = GD.Load<PackedScene>("res://CardVisual.tscn");
		
		// Get references to position areas
		opponentArea = GetNode<Node2D>("OpponentArea");
		communityCardsArea = GetNode<Node2D>("CommunityCardsArea");
		playerArea = GetNode<Node2D>("PlayerArea");
		
		
		// Create and shuffle deck
		deck = new Deck();
		deck.Shuffle();
		
		// Deal cards
		TestDealCards();
		
		// Evaluate and determine winner
		EvaluateWinner();
	}
	
	private void TestDealCards()
	{
		GD.Print("\n=== Dealing Cards ===");
		
		// Deal 2 cards to player
		playerHand.Add(deck.Deal());
		playerHand.Add(deck.Deal());
		
		GD.Print($"Player hand: {playerHand[0]}, {playerHand[1]}");
		
		var playerCard1 = playerArea.GetNode<CardVisual>("PlayerCard1");
		var playerCard2 = playerArea.GetNode<CardVisual>("PlayerCard2");
		playerCard1.SetCard(playerHand[0]);
		playerCard2.SetCard(playerHand[1]);
		
		
		// Deal 2 cards to opponent
		opponentHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());
		
		GD.Print($"Opponent hand: {opponentHand[0]}, {opponentHand[1]}");
		
		var opponentCard1 = opponentArea.GetNode<CardVisual>("OpponentCard1");
		var opponentCard2 = opponentArea.GetNode<CardVisual>("OpponentCard2");
		opponentCard1.SetCard(opponentHand[0]);
		opponentCard2.SetCard(opponentHand[1]);
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
		
		// Display community cards
		var flop1 = communityCardsArea.GetNode<CardVisual>("Flop1");
		var flop2 = communityCardsArea.GetNode<CardVisual>("Flop2");
		var flop3 = communityCardsArea.GetNode<CardVisual>("Flop3");
		var turn = communityCardsArea.GetNode<CardVisual>("Turn");
		var river = communityCardsArea.GetNode<CardVisual>("River");

		flop1.SetCard(communityCards[0]);
		flop2.SetCard(communityCards[1]);
		flop3.SetCard(communityCards[2]);
		turn.SetCard(communityCards[3]);
		river.SetCard(communityCards[4]);

		
		GD.Print($"\nCards remaining in deck: {deck.CardsRemaining()}");
	}
	
	private void EvaluateWinner()
	{
		GD.Print("\n=== Showdown ===");
		
		// Evaluate both hands using the HandEvaluator
		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);
		
		// Get readable hand names
		string playerHandName = HandEvaluator.GetHandName(playerRank);
		string opponentHandName = HandEvaluator.GetHandName(opponentRank);
		
		GD.Print($"Player: {playerHandName} (rank: {playerRank})");
		GD.Print($"Opponent: {opponentHandName} (rank: {opponentRank})");
		
		// Determine winner (lower rank = better)
		int result = HandEvaluator.CompareHands(playerRank, opponentRank);
		
		if (result > 0)
		{
			GD.Print("\n🎉 PLAYER WINS! 🎉");
		}
		else if (result < 0)
		{
			GD.Print("\n😞 OPPONENT WINS! 😞");
		}
		else
		{
			GD.Print("\n🤝 TIE! SPLIT POT! 🤝");
		}
	}
}
