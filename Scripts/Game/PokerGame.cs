// PokerGame.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame : Node2D
{
	// Core game data
	private PackedScene cardVisualScene;
	private Deck deck;
	private List<Card> playerHand = new List<Card>();
	private List<Card> opponentHand = new List<Card>();
	private List<Card> communityCards = new List<Card>();

	// Card visuals
	private CardVisual playerCard1;
	private CardVisual playerCard2;
	private CardVisual opponentCard1;
	private CardVisual opponentCard2;
	private CardVisual flop1;
	private CardVisual flop2;
	private CardVisual flop3;
	private CardVisual turnCard;
	private CardVisual riverCard;

	// UI
	private Button foldButton;
	private Button checkCallButton;
	private Button betRaiseButton;

	private Label playerStackLabel;
	private Label opponentStackLabel;
	private Label potLabel;
	private Label gameStateLabel;
	private Label playerHandType;
	private Label opponentHandType;

	// Game flow
	private Street currentStreet = Street.Preflop;

	// AI
	private OpponentProfile currentOpponent;
	private int selectedOpponentIndex = 0;

	public override void _Ready()
	{
		GD.Print("=== Poker Game Started ===");
		Control hudControl = GetNode<Control>("CanvasLayer/Control");
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

		Control actionButtons = hudControl.GetNode<Control>("ActionButtons");
		foldButton = actionButtons.GetNode<Button>("FoldButton");
		checkCallButton = actionButtons.GetNode<Button>("CheckCallButton");
		betRaiseButton = actionButtons.GetNode<Button>("BetRaiseButton");

		playerStackLabel = hudControl.GetNode<Label>("PlayerStackLabel");
		opponentStackLabel = hudControl.GetNode<Label>("OpponentStackLabel");
		potLabel = hudControl.GetNode<Label>("PotLabel");
		gameStateLabel = hudControl.GetNode<Label>("GameStateLabel");
		playerHandType = hudControl.GetNode<Label>("PlayerHandType");
		opponentHandType = hudControl.GetNode<Label>("OpponentHandType");

		foldButton.Pressed += OnFoldPressed;
		checkCallButton.Pressed += OnCheckCallPressed;
		betRaiseButton.Pressed += OnBetRaisePressed;

		OpponentProfile[] circuitAOpponents = OpponentProfiles.CircuitAOpponents();
		currentOpponent = circuitAOpponents[selectedOpponentIndex];
		GD.Print($"=== Opponent: {currentOpponent.Name} ===");
		GD.Print($"Aggression: {currentOpponent.Aggression:F2}");
		GD.Print($"Looseness: {currentOpponent.Looseness:F2}");
		GD.Print($"Bluffiness: {currentOpponent.Bluffiness:F2}");

		UpdateHud();
		StartNewHand();
	}

	private void ShowMessage(string text)
	{
		gameStateLabel.Text = text;
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
				ShowMessage("Flop dealt");
				break;
			case Street.Turn:
				communityCards.Add(deck.Deal());
				GD.Print($"Turn: {communityCards[3]}");
				ShowMessage("Turn card");
				break;
			case Street.River:
				communityCards.Add(deck.Deal());
				GD.Print($"River: {communityCards[4]}");
				ShowMessage("River card");
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
}
