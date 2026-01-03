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
	private HSlider betSlider;
	
	private Button foldButton;
	private Button checkCallButton;
	private Button betRaiseButton;

	private Label playerStackLabel;
	private Label opponentStackLabel;
	private Label potLabel;
	private Label gameStateLabel;
	private Label playerHandType;
	private Label opponentHandType;
	private Label betSliderLabel;
	
	private Label checkCallLabel;
	private Label betRaiseLabel;
	
	private Texture2D foldBtnImg;
	private Texture2D checkBtnImg;
	private Texture2D callBtnImg;
	private Texture2D betBtnImg;
	private Texture2D raiseBtnImg;
	
	// Game flow
	private Street currentStreet = Street.Preflop;
	private int playerChips = 100;
	private int opponentChips = 100;

	// AI
	private OpponentProfile currentOpponent;
	private int selectedOpponentIndex = 1;
	
	// Audio
	private AudioStreamPlayer deckDealAudioPlayer;
	private AudioStreamPlayer chipsAudioPlayer;
	private AudioStreamPlayer musicPlayer;

	public override void _Ready()
	{
		GD.Print("=== Poker Game Started ===");
		Control hudControl = GetNode<Control>("CanvasLayer/Control");
		cardVisualScene = GD.Load<PackedScene>("res://Scenes/CardVisual.tscn");

		// card areas
		Node2D opponentArea = GetNode<Node2D>("OpponentArea");
		Node2D communityCardsArea = GetNode<Node2D>("CommunityCardsArea");
		Node2D playerArea = GetNode<Node2D>("PlayerArea");

		// pocket cards
		playerCard1 = playerArea.GetNode<CardVisual>("PlayerCard1");
		playerCard2 = playerArea.GetNode<CardVisual>("PlayerCard2");
		opponentCard1 = opponentArea.GetNode<CardVisual>("OpponentCard1");
		opponentCard2 = opponentArea.GetNode<CardVisual>("OpponentCard2");

		// community cards
		flop1 = communityCardsArea.GetNode<CardVisual>("Flop1");
		flop2 = communityCardsArea.GetNode<CardVisual>("Flop2");
		flop3 = communityCardsArea.GetNode<CardVisual>("Flop3");
		turnCard = communityCardsArea.GetNode<CardVisual>("Turn");
		riverCard = communityCardsArea.GetNode<CardVisual>("River");

		// action buttons
		Control actionButtons = hudControl.GetNode<Control>("ActionButtons");
		foldButton = actionButtons.GetNode<Button>("FoldButton");
		checkCallButton = actionButtons.GetNode<Button>("CheckCallButton");
		betRaiseButton = actionButtons.GetNode<Button>("BetRaiseButton");

		// labels
		playerStackLabel = hudControl.GetNode<Label>("PlayerStackLabel");
		opponentStackLabel = hudControl.GetNode<Label>("OpponentStackLabel");
		potLabel = hudControl.GetNode<Label>("PotLabel");
		gameStateLabel = hudControl.GetNode<Label>("GameStateLabel");
		playerHandType = hudControl.GetNode<Label>("PlayerHandType");
		opponentHandType = hudControl.GetNode<Label>("OpponentHandType");
		betSliderLabel = hudControl.GetNode<Label>("BetSliderLabel");
		
		// slider
		betSlider = hudControl.GetNode<HSlider>("BetSlider");
		
		// audio players
		deckDealAudioPlayer = GetNode<AudioStreamPlayer>("DeckDealAudioPlayer");
		chipsAudioPlayer = GetNode<AudioStreamPlayer>("ChipsAudioPlayer");  
		musicPlayer = GetNode<AudioStreamPlayer>("MusicPlayer");  
		
		// set on-press handlers
		foldButton.Pressed += OnFoldPressed;
		checkCallButton.Pressed += OnCheckCallPressed;
		betRaiseButton.Pressed += OnBetRaisePressed;
		betSlider.ValueChanged += OnBetSliderValueChanged;

		// choose the opponent we will face
		OpponentProfile[] circuitAOpponents = OpponentProfiles.CircuitAOpponents();
		currentOpponent = circuitAOpponents[selectedOpponentIndex];
		
		// chip amount
		playerChips = currentOpponent.BuyIn;
		opponentChips = currentOpponent.BuyIn;
		
		// --- NEW BLIND CALCULATION ---
		// Target: We want the starting stack to be 50 Big Blinds (faster game) or 100 BB (deep game).
		// Example: BuyIn 500 -> BB 10. BuyIn 50 -> BB 1.
		
		bigBlind = Math.Max(2, currentOpponent.BuyIn / 50); 
		
		// Make sure BB is always even so SB can be half of it integer-wise
		if (bigBlind % 2 != 0) bigBlind++; 

		smallBlind = bigBlind / 2;

		// Update Bet Slider default
		betAmount = bigBlind; 

		GD.Print($"=== Opponent: {currentOpponent.Name} ===");
		GD.Print($"Buy-In: {currentOpponent.BuyIn} | Blinds: {smallBlind}/{bigBlind}");
		GD.Print($"Aggression: {currentOpponent.Aggression:F2}");
		GD.Print($"Looseness: {currentOpponent.Looseness:F2}");
		GD.Print($"Bluffiness: {currentOpponent.Bluffiness:F2}");

		musicPlayer.Play();
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
	
	private void StartNewHand()
	{
		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}

		GD.Print("\n=== New Hand ===");
		ShowMessage("New hand starting...");

		betSlider.Visible = true;
		foldButton.Visible = true;
		betRaiseButton.Visible = true;
		betSliderLabel.Visible = true;
		potLabel.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";

		deck = new Deck();
		deck.Shuffle();
		deckDealAudioPlayer.Play();

		pot = 0;
		aiBluffedThisHand = false;
		playerTotalBetsThisHand = 0;
		raisesThisStreet = 0;
		playerIsAllIn = false;
		opponentIsAllIn = false;
		isProcessingAIAction = false; // Reset flag

		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;

		playerBetOnStreet.Clear();
		playerBetSizeOnStreet.Clear();
		playerBetOnStreet[Street.Preflop] = false;
		playerBetOnStreet[Street.Flop] = false;
		playerBetOnStreet[Street.Turn] = false;
		playerBetOnStreet[Street.River] = false;

		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

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
		waitingForNextGame = false;

		playerHasButton = !playerHasButton;

		if (playerHasButton)
		{
			// 1. Handle Player (Small Blind)
			int sbAmount = Math.Min(smallBlind, playerChips); // Clamp to stack
			playerChips -= sbAmount;
			pot += sbAmount;
			playerBet = sbAmount;
			if (playerChips == 0) playerIsAllIn = true;

			// 2. Handle Opponent (Big Blind)
			int bbAmount = Math.Min(bigBlind, opponentChips); // Clamp to stack
			opponentChips -= bbAmount;
			pot += bbAmount;
			opponentBet = bbAmount;
			currentBet = opponentBet; // The high bet is the BB
			if (opponentChips == 0) opponentIsAllIn = true;

			isPlayerTurn = true;
			ShowMessage($"Blinds: You {sbAmount}, Opponent {bbAmount}");
			GD.Print($"Player SB. Posted: {sbAmount} vs {bbAmount}. Pot: {pot}");
		}
		else
		{
			// 1. Handle Player (Big Blind)
			int bbAmount = Math.Min(bigBlind, playerChips); // Clamp to stack 
			playerChips -= bbAmount;
			pot += bbAmount;
			playerBet = bbAmount;
			if (playerChips == 0) playerIsAllIn = true;

			// 2. Handle Opponent (Small Blind)
			int sbAmount = Math.Min(smallBlind, opponentChips); // Clamp to stack
			opponentChips -= sbAmount;
			pot += sbAmount;
			opponentBet = sbAmount;
			
			// Important: Current bet is the higher of the two blinds
			// Usually the BB, but if BB was all-in for 2 chips, and SB posted 5, the high bet is 5.
			currentBet = Math.Max(playerBet, opponentBet);

			if (opponentChips == 0) opponentIsAllIn = true;

			isPlayerTurn = false;
			ShowMessage($"Blinds: You {bbAmount}, Opponent {sbAmount}");
			GD.Print($"Opponent SB. Posted: {sbAmount} vs {bbAmount}. Pot: {pot}");
		}


		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
		
		// Single timer for AI turn
		if (!isPlayerTurn)
		{
			GetTree().CreateTimer(1.15).Timeout += () => CheckAndProcessAITurn();
		}
	}
	
	private void EndHand()
	{
		pot = 0;
		handInProgress = false;
		waitingForNextGame = true;

		if (IsGameOver())
		{
			HandleGameOver();
		}
		else
		{
			UpdateHud(); 
			RefreshBetSlider();
		}
	}

}
