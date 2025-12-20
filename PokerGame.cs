using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame : Node2D
{
	
	private PackedScene cardVisualScene;
	
	private Deck deck;
	private List<Card> playerHand = new List<Card>();
	private List<Card> opponentHand = new List<Card>();
	private List<Card> communityCards = new List<Card>();

	private CardVisual playerCard1;
	private CardVisual playerCard2;
	private CardVisual opponentCard1;
	private CardVisual opponentCard2;
	private CardVisual flop1;
	private CardVisual flop2;
	private CardVisual flop3;
	private CardVisual turnCard;
	private CardVisual riverCard;
	
	private enum Street
	{
		Flop,
		Turn,
		River
	}

	public override void _Ready()
	{
		GD.Print("=== Poker Game Started ===");
	
		cardVisualScene = GD.Load<PackedScene>("res://CardVisual.tscn");
		
		Node2D opponentArea = GetNode<Node2D>("OpponentArea");
		Node2D communityCardsArea = GetNode<Node2D>("CommunityCardsArea");
		Node2D playerArea = GetNode<Node2D>("PlayerArea");
		
		playerCard1 = playerArea.GetNode<CardVisual>("PlayerCard1");
		playerCard2 = playerArea.GetNode<CardVisual>("PlayerCard2");
		opponentCard1 = opponentArea.GetNode<CardVisual>("OpponentCard1");
		opponentCard2 = opponentArea.GetNode<CardVisual>("OpponentCard2");

		flop1 = communityCardsArea.GetNode<CardVisual>("Flop1");
		flop2 = communityCardsArea.GetNode<CardVisual>("Flop2");
		flop3 = communityCardsArea.GetNode<CardVisual>("Flop3");
		turnCard = communityCardsArea.GetNode<CardVisual>("Turn");
		riverCard = communityCardsArea.GetNode<CardVisual>("River");
		
		deck = new Deck();
		deck.Shuffle();
		
		DealInitialHands();
		DealCommunityCards(Street.Flop);
		//RevealCommunityCards(Street.Flop);
		DealCommunityCards(Street.Turn);
		//RevealCommunityCards(Street.Turn);
		DealCommunityCards(Street.River);
		//RevealCommunityCards(Street.River);
		EvaluateWinner();
	}
	
	private void DealInitialHands()
	{
		GD.Print("\n=== Dealing Initial Hands ===");

		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

		playerHand.Add(deck.Deal());
		playerHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());

		GD.Print($"Player hand: {playerHand[0]}, {playerHand[1]}");
		GD.Print($"Opponent hand: {opponentHand[0]}, {opponentHand[1]}");
		
		playerCard1.ShowCard(playerHand[0]);
		playerCard2.ShowCard(playerHand[1]);
		
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
	}

	private void DealCommunityCards(Street street)
	{
		GD.Print($"\n=== Community Cards: {street} ===");

		switch (street)
		{
			case Street.Flop:
				communityCards.Add(deck.Deal());
				communityCards.Add(deck.Deal());
				communityCards.Add(deck.Deal());

				GD.Print($"Flop: {communityCards[0]}, {communityCards[1]}, {communityCards[2]}");

				flop1.ShowBack();
				flop2.ShowBack();
				flop3.ShowBack();
				break;

			case Street.Turn:
				communityCards.Add(deck.Deal());
				GD.Print($"Turn: {communityCards[3]}");

				turnCard.ShowBack();
				break;

			case Street.River:
				communityCards.Add(deck.Deal());
				GD.Print($"River: {communityCards[4]}");

				riverCard.ShowBack();
				break;
		}
	}
	
	private void RevealCommunityCards(Street street)
	{
		GD.Print($"\n=== Reveal Community Cards: {street} ===");
		switch (street)
		{
			case Street.Flop:
				flop1.ShowCard(communityCards[0]);
				flop2.ShowCard(communityCards[1]);
				flop3.ShowCard(communityCards[2]);
				break;

			case Street.Turn:
				turnCard.ShowCard(communityCards[3]);
				break;

			case Street.River:
				riverCard.ShowCard(communityCards[4]);
				break;
		}
	}
	
	private void EvaluateWinner()
	{
		GD.Print("\n=== Showdown ===");
		
		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);
		
		string playerHandName = HandEvaluator.GetHandName(playerRank);
		string opponentHandName = HandEvaluator.GetHandName(opponentRank);
		
		GD.Print($"Player: {playerHandName} (rank: {playerRank})");
		GD.Print($"Opponent: {opponentHandName} (rank: {opponentRank})");
	
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
