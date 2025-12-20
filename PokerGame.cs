using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame : Node2D
{
	
	private enum Street
	{
		Preflop,
		Flop,
		Turn,
		River
	}
	
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
	
	private Button foldButton;
	private Button checkCallButton;
	private Button betRaiseButton;
	
	private int playerChips = 50;
	private int opponentChips = 50;
	private int pot = 0;
	private int betAmount = 5;
	
	private Label playerStackLabel;
	private Label opponentStackLabel;
	private Label potLabel;

	
	private Street currentStreet = Street.Preflop;
	private bool handInProgress = false;

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
		
		foldButton.Pressed += OnFoldPressed;
		checkCallButton.Pressed += OnCheckCallPressed;
		betRaiseButton.Pressed += OnBetRaisePressed;
		
		UpdateHud();
  		StartNewHand();
	}
	
	private void StartNewHand()
	{
		GD.Print("\n=== New Hand ===");

		deck = new Deck();
		deck.Shuffle();

		pot = 0;

		// Clear hands and community
		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

		// Reset card visuals to backs
		playerCard1.ShowBack();
		playerCard2.ShowBack();
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
		flop1.ShowBack();
		flop2.ShowBack();
		flop3.ShowBack();
		turnCard.ShowBack();
		riverCard.ShowBack();

		DealInitialHands();
		currentStreet = Street.Preflop;
		handInProgress = true;

		UpdateHud();
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
	
	private void OnFoldPressed()
	{
		if (!handInProgress) return;
		GD.Print("Player folds");

		opponentChips += pot;
		pot = 0;
		UpdateHud();

		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");

		handInProgress = false;
		StartNewHand();
	}

	private void OnCheckCallPressed()
	{
		if (!handInProgress) return;
		 GD.Print($"Check/Call on {currentStreet}");
		
		int toBet = Math.Min(betAmount, playerChips);
		playerChips -= toBet;
		pot += toBet;
		UpdateHud();
		
		GD.Print($"Player bets {toBet}, Player stack: {playerChips}, Pot: {pot}");
		OpponentAutoCall();
	
		AdvanceStreet();
	}

	private void OnBetRaisePressed()
	{
		if (!handInProgress) return;
		GD.Print($"Bet/Raise on {currentStreet}");
		
		int toBet = Math.Min(betAmount * 2, playerChips);
		playerChips -= toBet;
		pot += toBet;
		UpdateHud();
		
		GD.Print($"Player raises {toBet}, Player stack: {playerChips}, Pot: {pot}");
		OpponentAutoCall();
	
		AdvanceStreet();
	}
	
	private void UpdateHud()
	{
		playerStackLabel.Text = $"You: {playerChips}";
		opponentStackLabel.Text = $"Opp: {opponentChips}";
		potLabel.Text = $"Pot: {pot}";
	}

	private void OpponentAutoCall()
	{
		int toCall = Math.Min(betAmount, opponentChips);
		opponentChips -= toCall;
		pot += toCall;
		UpdateHud();

		GD.Print($"Opponent calls {toCall}, Opponent stack: {opponentChips}, Pot: {pot}");
	}

	private void AdvanceStreet()
	{
		switch (currentStreet)
		{
			case Street.Preflop:
				DealCommunityCards(Street.Flop);
				currentStreet = Street.Flop;
				break;
			case Street.Flop:
				RevealCommunityCards(Street.Flop);
				DealCommunityCards(Street.Turn);
				currentStreet = Street.Turn;
				break;
			case Street.Turn:
				RevealCommunityCards(Street.Turn);
				DealCommunityCards(Street.River);
				currentStreet = Street.River;
				break;
			case Street.River:
				RevealCommunityCards(Street.River);
				ShowDown();
				break;
		}
	}
	
	private void ShowDown()
	{
		GD.Print("\n=== Showdown ===");

		opponentCard1.ShowCard(opponentHand[0]);
		opponentCard2.ShowCard(opponentHand[1]);

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);

		if (result > 0)
		{
			GD.Print("\n🎉 PLAYER WINS! 🎉");
			playerChips += pot;
		}
		else if (result < 0)
		{
			GD.Print("\n😞 OPPONENT WINS! 😞");
			opponentChips += pot;
		}
		else
		{
			GD.Print("\n🤝 TIE! SPLIT POT! 🤝");
			int split = pot / 2;
			playerChips += split;
			opponentChips += pot - split;
		}

		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		pot = 0;
		UpdateHud();

		handInProgress = false;
		StartNewHand();
	}

}
