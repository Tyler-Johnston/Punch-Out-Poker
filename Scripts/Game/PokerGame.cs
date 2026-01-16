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
	private Label opponentDialogueLabel;
	
	private Label checkCallLabel;
	private Label betRaiseLabel;
	
	private Texture2D foldBtnImg;
	private Texture2D checkBtnImg;
	private Texture2D callBtnImg;
	private Texture2D betBtnImg;
	private Texture2D raiseBtnImg;
	
	private TextureRect opponentPortrait;
	
	// Game flow
	private Street currentStreet = Street.Preflop;
	private int playerChips = 100;
	private int opponentChips = 100;

	// Side Pot / Contribution Tracking
	private int playerContributed = 0;
	private int opponentContributed = 0;
	
	// Game state flags
	private bool isPlayerTurn = true;
	private bool playerHasButton = false;
	private bool handInProgress = false;
	private bool waitingForNextGame = false;
	private bool playerIsAllIn = false;
	private bool opponentIsAllIn = false;
	private bool isProcessingAIAction = false;
	private bool aiBluffedThisHand = false;
	
	private int playerTotalBetsThisHand = 0;
	
	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();

	private AIPokerPlayer aiOpponent;
	private PokerDecisionMaker decisionMaker;
	private string currentOpponentName;
	private int buyInAmount;
	
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
		opponentDialogueLabel = hudControl.GetNode<Label>("OpponentDialogue");
		
		opponentPortrait = hudControl.GetNode<TextureRect>("OpponentPortrait");
		
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

		currentOpponentName = GameManager.Instance.CurrentOpponentName;
		buyInAmount = GameManager.Instance.CurrentBuyIn;
		
		if (string.IsNullOrEmpty(currentOpponentName))
		{
			// Default for testing
			currentOpponentName = "Steve";
			buyInAmount = 100;
			GD.PushWarning("No opponent selected, defaulting to Steve");
		}
		
		GD.Print($"=== VS {currentOpponentName} ===");
		GD.Print($"Buy-In: ${buyInAmount}");
		
		aiOpponent = new AIPokerPlayer();
		aiOpponent.Personality = LoadOpponentPersonality(currentOpponentName);
		aiOpponent.ChipStack = buyInAmount;
		aiOpponent.PlayerName = currentOpponentName;

		decisionMaker = new PokerDecisionMaker();
		aiOpponent.AddChild(decisionMaker);
		AddChild(aiOpponent);
		// Explicitly set the decision maker reference (in case _Ready() didn't find it)
		aiOpponent.SetDecisionMaker(decisionMaker);
		
		// Log personality stats
		var personality = aiOpponent.Personality;
		GD.Print($"Personality Stats:");
		GD.Print($"  Aggression: {personality.BaseAggression:F2}");
		GD.Print($"  Bluff Freq: {personality.BaseBluffFrequency:F2}");
		GD.Print($"  Call Tendency: {personality.CallTendency:F2}");
		GD.Print($"  Tilt Sensitivity: {personality.TiltSensitivity:F2}");
		
		// chip amount
		playerChips = buyInAmount;
		opponentChips = buyInAmount;
		
		// --- BLIND CALCULATION ---
		bigBlind = Math.Max(2, buyInAmount / 50); 
		if (bigBlind % 2 != 0) bigBlind++; 
		smallBlind = bigBlind / 2;

		// Update Bet Slider default
		betAmount = bigBlind; 
		
		playerHasButton = false; 

		GD.Print($"Blinds: {smallBlind}/{bigBlind}");

		//musicPlayer.Play();
		LoadOpponentPortrait();
		UpdateHud();
		StartNewHand();
	}
	
	/// <summary>
	/// Load personality for the current opponent
	/// </summary>
	private PokerPersonality LoadOpponentPersonality(string opponentName)
	{
		// Try to load from .tres file first
		string resourcePath = $"res://Resources/Personalities/{opponentName.ToLower().Replace(" ", "_")}_personality.tres";
		
		if (ResourceLoader.Exists(resourcePath))
		{
			GD.Print($"Loading personality from: {resourcePath}");
			return GD.Load<PokerPersonality>(resourcePath);
		}
		
		// Fall back to preset factory
		GD.Print($"Loading personality from preset: {opponentName}");
		return opponentName switch
		{
			"Steve" => PersonalityPresets.CreateSteve(),
			"Aryll" => PersonalityPresets.CreateAryll(),
			"Boy Wizard" => PersonalityPresets.CreateBoyWizard(),
			"Cowboy" => PersonalityPresets.CreateCowboy(),
			"Hippie" => PersonalityPresets.CreateHippie(),
			"Rumi" => PersonalityPresets.CreateRumi(),
			"King" => PersonalityPresets.CreateKing(),
			"Old Wizard" => PersonalityPresets.CreateOldWizard(),
			"Spade" => PersonalityPresets.CreateSpade(),
			_ => PersonalityPresets.CreateSteve() // Default fallback
		};
	}

	private void ShowMessage(string text)
	{
		gameStateLabel.Text = text;
	}

	// Helper to track contributions accurately
	public void AddToPot(bool isPlayer, int amount)
	{
		pot += amount;
		if (isPlayer)
			playerContributed += amount;
		else
			opponentContributed += amount;
	}

	// Side Pot logic for Heads Up
	private bool ReturnUncalledChips()
	{
		if (playerContributed > opponentContributed)
		{
			int refund = playerContributed - opponentContributed;
			playerChips += refund;
			pot -= refund;
			playerContributed -= refund;
			
			GD.Print($"Side Pot: Returned {refund} uncalled chips to Player.");
			ShowMessage($"Returned {refund} uncalled chips");
			return true;
		}
		else if (opponentContributed > playerContributed)
		{
			int refund = opponentContributed - playerContributed;
			opponentChips += refund;
			pot -= refund;
			opponentContributed -= refund;
			
			GD.Print($"Side Pot: Returned {refund} uncalled chips to Opponent.");
			ShowMessage($"Returned {refund} uncalled chips");
			return true;
		}
		return false;
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

		// Deal cards to AI opponent
		aiOpponent.Hand.Clear();
		foreach (var card in opponentHand)
		{
			aiOpponent.DealCard(card);
		}

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
		if (!waitingForNextGame && handInProgress) return; 
		waitingForNextGame = false;
		opponentDialogueLabel.Text = "";

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}
		
		// Reset AI opponent for new hand
		aiOpponent.ResetForNewHand();
		aiOpponent.ChipStack = opponentChips;

		// Re-enable input button after state is safely locked
		checkCallButton.Disabled = false;

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
		playerContributed = 0;
		opponentContributed = 0;
		
		aiBluffedThisHand = false;
		playerTotalBetsThisHand = 0;
		raisesThisStreet = 0;
		playerIsAllIn = false;
		opponentIsAllIn = false;
		isProcessingAIAction = false; 

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

		// Button Rotation
		playerHasButton = !playerHasButton;

		if (playerHasButton)
		{
			// Player is Small Blind
			int sbAmount = Math.Min(smallBlind, playerChips); 
			playerChips -= sbAmount;
			AddToPot(true, sbAmount);
			playerBet = sbAmount;
			if (playerChips == 0) playerIsAllIn = true;

			// Opponent is Big Blind
			int bbAmount = Math.Min(bigBlind, opponentChips); 
			opponentChips -= bbAmount;
			aiOpponent.ChipStack = opponentChips;
			AddToPot(false, bbAmount);
			opponentBet = bbAmount;
			currentBet = opponentBet; 
			if (opponentChips == 0) opponentIsAllIn = true;

			isPlayerTurn = true;
			ShowMessage($"Blinds: You {sbAmount}, {currentOpponentName} {bbAmount}");
			GD.Print($"Player SB. Posted: {sbAmount} vs {bbAmount}. Pot: {pot}");
		}
		else
		{
			// Player is Big Blind
			int bbAmount = Math.Min(bigBlind, playerChips); 
			playerChips -= bbAmount;
			AddToPot(true, bbAmount);
			playerBet = bbAmount;
			if (playerChips == 0) playerIsAllIn = true;

			// Opponent is Small Blind
			int sbAmount = Math.Min(smallBlind, opponentChips); 
			opponentChips -= sbAmount;
			aiOpponent.ChipStack = opponentChips;
			AddToPot(false, sbAmount);
			opponentBet = sbAmount;
			
			currentBet = Math.Max(playerBet, opponentBet);

			if (opponentChips == 0) opponentIsAllIn = true;

			isPlayerTurn = false;
			ShowMessage($"Blinds: You {bbAmount}, {currentOpponentName} {sbAmount}");
			GD.Print($"Opponent SB. Posted: {sbAmount} vs {bbAmount}. Pot: {pot}");
		}

		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
		
		if (playerIsAllIn && opponentIsAllIn)
		{
			GetTree().CreateTimer(1.0).Timeout += AdvanceStreet;
		}
		else if (!isPlayerTurn && !opponentIsAllIn)
		{
			GetTree().CreateTimer(1.15).Timeout += () => CheckAndProcessAITurn();
		}
		else if (!isPlayerTurn && opponentIsAllIn)
		{
			isPlayerTurn = true;
			ShowMessage($"{currentOpponentName} is All-In");
			UpdateHud();
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
	
	private void LoadOpponentPortrait()
	{
		string portraitPath = $"res://Assets/Textures/Portraits/{currentOpponentName} Small.png";
		
		if (ResourceLoader.Exists(portraitPath))
		{
			opponentPortrait.Texture = GD.Load<Texture2D>(portraitPath);
			GD.Print($"Loaded portrait: {portraitPath}");
		}
		else
		{
			GD.PrintErr($"Portrait not found: {portraitPath}");
		}
	}
//
	//public void UpdateOpponentDialogue(HandStrength handStrength, float tiltLevel = 0)
	//{
		//// Get hand evaluation
		//string handEval = HandEvaluator.GetHandDescription(opponentHand, communityCards);
		//
		//// Show personality info
		//var personality = aiOpponent.Personality;
		//opponentDialogueLabel.Text = $"{handEval}";
		//
		//if (tiltLevel > 20)
		//{
			//opponentDialogueLabel.Text += $" [TILTED: {tiltLevel:F0}]";
		//}
		//
		//// Show current stats (affected by tilt)
		//GD.Print($"[AI STATE] Agg: {personality.CurrentAggression:F2}, " +
				 //$"Bluff: {personality.CurrentBluffFrequency:F2}, " +
				 //$"Tilt: {tiltLevel:F1}");
	//}
	
	/// <summary>
	/// AI decision making using personality + dialogue system
	/// </summary>
	private PlayerAction DecideAIAction()
	{
		// Create game state for AI decision maker
		GameState gameState = new GameState
		{
			CommunityCards = new List<Card>(communityCards),
			PotSize = pot,
			CurrentBet = currentBet,
			Street = currentStreet,
			BigBlind = bigBlind,
			IsAIInPosition = DetermineAIPosition()
		};
		gameState.SetPlayerBet(aiOpponent, opponentBet);
		
		// 1) Get AI decision (Fold/Check/Call/Raise/AllIn)
		PlayerAction action = aiOpponent.MakeDecision(gameState);
		
		// 2) Evaluate hand strength category for tells / dialogue
		HandStrength strength = aiOpponent.DetermineHandStrengthCategory(gameState);
		
		// 3) Get a *behavior* tell key (for logs/animations)
		string tellKey = aiOpponent.GetTellForHandStrength(strength);
		if (!string.IsNullOrEmpty(tellKey))
		{
			GD.Print($"[TELL] {currentOpponentName}: {tellKey}");
			// If you later add animations, you can map tellKey -> animation here
		}
		
		// 4) Get a spoken dialogue line (what appears in the label)
		string dialogueLine = aiOpponent.GetDialogueForAction(
			action,
			strength,
			aiBluffedThisHand // set this from your bluff-tracking logic if you want
		);
		
		// 5) Apply chattiness: chance that they say nothing
		float chatRoll = GD.Randf();
		if (chatRoll <= aiOpponent.Personality.Chattiness && !string.IsNullOrEmpty(dialogueLine))
		{
			opponentDialogueLabel.Text = dialogueLine;
		}
		else
		{
			// Silent this turn
			opponentDialogueLabel.Text = "";
		}
		
		return action;
	}

	
	/// <summary>
	/// Check if game is over
	/// </summary>
	private bool IsGameOver()
	{
		return playerChips <= 0 || opponentChips <= 0;
	}
	
	/// <summary>
	/// Handle game over state
	/// </summary>
	private void HandleGameOver()
	{
		bool playerWon = opponentChips <= 0;
		
		if (playerWon)
		{
			int winnings = buyInAmount * 2; // Winner takes all
			GameManager.Instance.OnMatchWon(currentOpponentName, winnings);
			ShowMessage($"You defeated {currentOpponentName}!");
			GD.Print($"=== VICTORY vs {currentOpponentName} ===");
			
			// Process AI tilt for losing
			aiOpponent.ProcessHandResult(HandResult.Loss);
		}
		else
		{
			GameManager.Instance.OnMatchLost(currentOpponentName);
			ShowMessage($"{currentOpponentName} wins!");
			GD.Print($"=== DEFEAT vs {currentOpponentName} ===");
		}
		
		// Return to menu after delay
		GetTree().CreateTimer(3.0).Timeout += () => 
		{
			GetTree().ChangeSceneToFile("res://Scenes/CharacterSelect.tscn");
		};
	}
	
	/// <summary>
	/// Determine if AI is in position (acts last postflop)
	/// In heads-up: button posts SB and acts last postflop = in position
	/// </summary>
	private bool DetermineAIPosition()
	{
		// Preflop: button acts first, so AI is OOP if they have button
		// Postflop: button acts last, so AI is IP if they have button
		
		if (currentStreet == Street.Preflop)
		{
			// Preflop in HU: button = OOP (acts first)
			return playerHasButton; // If player has button, AI is IP (acts last preflop)
		}
		else
		{
			// Postflop: button = IP (acts last)
			return !playerHasButton; // If player doesn't have button, AI has button = IP
		}
	}
}
