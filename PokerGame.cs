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
		
		// Display cards on screen
		DisplayCards();
		
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
	
	private void DisplayCards()
	{
		GD.Print("\n=== Displaying Cards ===");
		
		// Display player cards (face up)
		SpawnCard(playerHand[0], playerArea.GetNode<Node2D>("PlayerCard1").Position, playerArea);
		SpawnCard(playerHand[1], playerArea.GetNode<Node2D>("PlayerCard2").Position, playerArea);
		
		// Display opponent cards (face down)
		SpawnCardFaceDown(opponentArea.GetNode<Node2D>("OpponentCard1").Position, opponentArea);
		SpawnCardFaceDown(opponentArea.GetNode<Node2D>("OpponentCard2").Position, opponentArea);
		
		// Display community cards
		SpawnCard(communityCards[0], communityCardsArea.GetNode<Node2D>("Flop1").Position, communityCardsArea);
		SpawnCard(communityCards[1], communityCardsArea.GetNode<Node2D>("Flop2").Position, communityCardsArea);
		SpawnCard(communityCards[2], communityCardsArea.GetNode<Node2D>("Flop3").Position, communityCardsArea);
		SpawnCard(communityCards[3], communityCardsArea.GetNode<Node2D>("Turn").Position, communityCardsArea);
		SpawnCard(communityCards[4], communityCardsArea.GetNode<Node2D>("River").Position, communityCardsArea);
		
		GD.Print("Cards displayed!");
	}
	
	private void SpawnCard(Card card, Vector2 position, Node2D parent)
	{
		// Instantiate a CardVisual
		var cardVisual = cardVisualScene.Instantiate<CardVisual>();
		
		// Set its position
		cardVisual.Position = position;
		
		// Set the card data
		cardVisual.SetCard(card);
		
		// Add to the scene
		parent.AddChild(cardVisual);
	}
	
	private void SpawnCardFaceDown(Vector2 position, Node2D parent)
	{
		// Instantiate a CardVisual
		var cardVisual = cardVisualScene.Instantiate<CardVisual>();
		
		// Set its position
		cardVisual.Position = position;
		
		// Set it face down
		cardVisual.SetFaceDown();
		
		// Add to the scene
		parent.AddChild(cardVisual);
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
