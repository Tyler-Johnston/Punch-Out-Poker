using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokerGame : Node2D
{
	// core game data
	private PackedScene cardVisualScene;
	private Deck deck;
	private List<Card> playerHand = new List<Card>();
	private List<Card> opponentHand = new List<Card>();
	private List<Card> communityCards = new List<Card>();

	// card visuals
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
	private TextureRect tableColor;
	
	private AtlasTexture _opponentAtlas;
	
	// game flow
	private Street currentStreet = Street.Preflop;
	private int playerChips = 100;
	private int opponentChips = 100;
	
	// Track AI strength when they commit to all-in (for bad beat detection)
	// This must be set in OnOpponentAllIn / OnOpponentCall logic!
	public float aiStrengthAtAllIn = 0f; 

	// side pot and contribution tracking
	private int playerContributed = 0;
	private int opponentContributed = 0;
	
	// game state flags
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

	// ais
	private AIPokerPlayer aiOpponent;
	private PokerDecisionMaker decisionMaker;
	private DialogueManager dialogueManager; // New dialogue manager
	private string currentOpponentName;
	private int buyInAmount;
	
	// Audio
	private SFXPlayer sfxPlayer;
	private AudioStreamPlayer musicPlayer;

	public override void _Ready()
	{
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
		tableColor = hudControl.GetNode<TextureRect>("ColorRect");
		
		// slider
		betSlider = hudControl.GetNode<HSlider>("BetSlider");
		
		// audio players
		sfxPlayer = GetNode<SFXPlayer>("SFXPlayer");
		musicPlayer = GetNode<AudioStreamPlayer>("MusicPlayer");  
		
		// set on-press handlers
		foldButton.Pressed += OnFoldPressed;
		checkCallButton.Pressed += OnCheckCallPressed;
		betRaiseButton.Pressed += OnBetRaisePressed;
		betSlider.ValueChanged += OnBetSliderValueChanged;

		// initialize opponent information
		currentOpponentName = GameManager.Instance.CurrentOpponentName;
		buyInAmount = GameManager.Instance.CurrentBuyIn;
		
		if (string.IsNullOrEmpty(currentOpponentName))
		{
			currentOpponentName = "Steve";
			buyInAmount = 50;
			GD.PushWarning("No opponent selected, defaulting to Steve");
		}
		
		GD.Print($"---------- Player VS {currentOpponentName} ----------");
		
		// ai initialization
		aiOpponent = new AIPokerPlayer();
		aiOpponent.Personality = LoadOpponentPersonality(currentOpponentName);
		aiOpponent.ChipStack = buyInAmount;
		aiOpponent.PlayerName = currentOpponentName;

		decisionMaker = new PokerDecisionMaker();
		AddChild(aiOpponent); 
		aiOpponent.SetDecisionMaker(decisionMaker);
		
		// Initialize Dialogue Manager
		dialogueManager = new DialogueManager();
		AddChild(dialogueManager);
		dialogueManager.Initialize(aiOpponent.Personality);
		
		// log personality stats
		var personality = aiOpponent.Personality;
		GD.Print($"Personality Stats:");
		GD.Print($"  Aggression: {personality.BaseAggression:F2}");
		GD.Print($"  Bluff Freq: {personality.BaseBluffFrequency:F2}");
		GD.Print($"  Call Tendency: {personality.CallTendency:F2}");
		GD.Print($"  Tilt Sensitivity: {personality.TiltSensitivity:F2}");
		
		// chip amount
		playerChips = buyInAmount;
		opponentChips = buyInAmount;
		
		// blind calculation
		bigBlind = Math.Max(2, buyInAmount / 50); 
		if (bigBlind % 2 != 0) bigBlind++; 
		smallBlind = bigBlind / 2;
		betAmount = bigBlind; 
		playerHasButton = false; 
		GD.Print($"Blinds: {smallBlind}/{bigBlind}");
		
		// get table color depending on the circuit we are in
		Color middleColor = new Color("#52a67f"); 
		switch (GameManager.Instance.GetCircuitType())
		{
			case 0:
				middleColor = new Color("#52a67f"); 
				break;
			case 1:
				middleColor = new Color("a80054ff"); 
				break;
			case 2:
				middleColor = new Color("#127AE3"); 
				break;
				
		}
		
		// set the table color
		var gradientTexture = tableColor.Texture as GradientTexture2D;
		if (gradientTexture != null)
		{
			gradientTexture.Gradient = (Gradient)gradientTexture.Gradient.Duplicate();
			Color cornerColor = middleColor.Darkened(0.3f);

			gradientTexture.Gradient.SetColor(0, middleColor);
			gradientTexture.Gradient.SetColor(1, cornerColor);
		}
		ShaderMaterial retroMat = new ShaderMaterial();
		retroMat.Shader = GD.Load<Shader>("res://Assets/Shaders/Pixelate.gdshader");
		tableColor.Material = retroMat;

		//musicPlayer.Play();
		UpdateHud();
		StartNewHand();
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
	
	// NEW: Visual updates for Tilt State
	private void UpdateOpponentVisuals()
	{
		TiltState state = aiOpponent.CurrentTiltState;
		
		switch (state)
		{
			case TiltState.Zen:
				opponentStackLabel.Modulate = Colors.White;
				break;
			case TiltState.Annoyed:
				opponentStackLabel.Modulate = Colors.Yellow;
				break;
			case TiltState.Steaming:
				opponentStackLabel.Modulate = Colors.Orange;
				ApplyShake(opponentPortrait, 2.0f);
				break;
			case TiltState.Monkey:
				opponentStackLabel.Modulate = Colors.Red;
				ApplyShake(opponentPortrait, 5.0f);
				break;
		}

		// Trigger Random Tilt Dialogue
		if (handInProgress && !waitingForNextGame)
		{
			string tiltLine = dialogueManager.GetTiltDialogue(state);
			if (!string.IsNullOrEmpty(tiltLine))
			{
				opponentDialogueLabel.Text = tiltLine;
			}
		}
	}
	
	private void ApplyShake(Control node, float intensity)
	{
		var tween = CreateTween();
		Vector2 originalPos = node.Position;
		tween.TweenProperty(node, "position", originalPos + new Vector2(intensity, 0), 0.05f);
		tween.TweenProperty(node, "position", originalPos - new Vector2(intensity, 0), 0.05f);
		tween.TweenProperty(node, "position", originalPos, 0.05f);
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
			return true;
		}
		else if (opponentContributed > playerContributed)
		{
			int refund = opponentContributed - playerContributed;
			opponentChips += refund;
			pot -= refund;
			opponentContributed -= refund;
			
			GD.Print($"Side Pot: Returned {refund} uncalled chips to Opponent.");
			return true;
		}
		return false;
	}

	private async Task DealInitialHands()
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
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
		
		// Animate Player Card 1
		sfxPlayer.PlaySound("card_flip");
		await playerCard1.RevealCard(playerHand[0]);
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		// Animate Player Card 2
		sfxPlayer.PlaySound("card_flip");
		await playerCard2.RevealCard(playerHand[1]);
		await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);

		// Opponent cards stay face down
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
	}

	public async Task DealCommunityCards(Street street)
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
				
				sfxPlayer.PlaySound("card_flip");
				await flop1.RevealCard(communityCards[0]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				sfxPlayer.PlaySound("card_flip");
				await flop2.RevealCard(communityCards[1]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				sfxPlayer.PlaySound("card_flip");
				await flop3.RevealCard(communityCards[2]);
				break;
				
			case Street.Turn:
				communityCards.Add(deck.Deal());
				GD.Print($"Turn: {communityCards[3]}");
				ShowMessage("Turn card");
				
				sfxPlayer.PlaySound("card_flip");
				await turnCard.RevealCard(communityCards[3]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				break;
				
			case Street.River:
				communityCards.Add(deck.Deal());
				GD.Print($"River: {communityCards[4]}");
				ShowMessage("River card");
				
				sfxPlayer.PlaySound("card_flip");
				await riverCard.RevealCard(communityCards[4]);
				await ToSignal(GetTree().CreateTimer(0.25f), SceneTreeTimer.SignalName.Timeout);
				break;
		}
	}
	
	private async void StartNewHand()
	{
		if (!waitingForNextGame && handInProgress) return; 
		waitingForNextGame = false;
		opponentDialogueLabel.Text = "";

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}
		
		// reset AI opponent for new hand
		aiOpponent.ResetForNewHand();
		aiOpponent.ChipStack = opponentChips;

		// re-enable input button after state is safely locked
		checkCallButton.Disabled = false;

		GD.Print("\n=== New Hand ===");
		ShowMessage("");

		betSlider.Visible = true;
		foldButton.Visible = true;
		betRaiseButton.Visible = true;
		betSliderLabel.Visible = true;
		potLabel.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";
		aiStrengthAtAllIn = 0f; // Reset snapshot

		deck = new Deck();
		deck.Shuffle();
		// deckDealAudioPlayer.Play(); // Handled in deal method

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

		await DealInitialHands();
		currentStreet = Street.Preflop;
		handInProgress = true;
		
		// Update visuals at start of hand
		UpdateOpponentVisuals();

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
	
	private async void ShowDown()
	{
		GD.Print("\n=== Showdown ===");
		
		// 1. Process Refunds First
		bool refundOccurred = ReturnUncalledChips();

		if (refundOccurred)
		{
			await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
		}

		// reveal opponent hand
		sfxPlayer.PlaySound("card_flip");
		await opponentCard1.RevealCard(opponentHand[0]);
		await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
		sfxPlayer.PlaySound("card_flip");
		await opponentCard2.RevealCard(opponentHand[1]);
		await ToSignal(GetTree().CreateTimer(1.0f), SceneTreeTimer.SignalName.Timeout);

		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandDescription(playerHand, communityCards);
		string opponentHandName = HandEvaluator.GetHandDescription(opponentHand, communityCards);

		playerHandType.Text = playerHandName;
		opponentHandType.Text = opponentHandName;

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);
		string message;
		HandResult aiHandResult;
		
		int finalPot = pot;

		if (result > 0)
		{
			// Player wins
			GD.Print("\nPLAYER WINS!");
			message = $"You win ${finalPot} with {playerHandName}!";
			
			// Definition: AI was Strong (>0.70) when chips went in, but lost.
			bool isBadBeat = (aiStrengthAtAllIn > 0.70f); 
			// Definition: AI had a monster hand (Four of a kind, etc) but lost (Cooler)
			bool isCooler = (opponentRank <= 1609); 
			
			if (isBadBeat)
			{
				GD.Print($"{currentOpponentName} suffered a BAD BEAT! (Strength was {aiStrengthAtAllIn:F2})");
				aiHandResult = HandResult.BadBeat;
			}
			else if (isCooler)
			{
				GD.Print($"{currentOpponentName} suffered a COOLER!");
				aiHandResult = HandResult.BadBeat;
			}
			else if (aiBluffedThisHand && opponentRank > 6185)
			{
				GD.Print($"{currentOpponentName} was bluffing!");
				aiHandResult = HandResult.BluffCaught;
			}
			else
			{
				aiHandResult = HandResult.Loss; 
			}
			
			playerChips += pot;
			aiOpponent.ProcessHandResult(aiHandResult);
		}
		else if (result < 0)
		{
			// Opponent wins
			GD.Print("\nOPPONENT WINS!");
			message = $"{currentOpponentName} wins ${finalPot} with {opponentHandName}";
			
			if (aiBluffedThisHand)
			{
				GD.Print($"{currentOpponentName} won with a bluff!");
			}
			else if (opponentRank < 1609)
			{
				GD.Print($"{currentOpponentName} had a strong hand!");
			}
			
			opponentChips += pot;
			aiOpponent.ChipStack = opponentChips;
			aiOpponent.ProcessHandResult(HandResult.Win);
			
			if (playerRank <= 2467)
			{
				GD.Print("You suffered a bad beat!");
			}
		}
		else
		{
			// Split pot
			GD.Print("\nSPLIT POT!");
			int split = pot / 2;
			message = $"Split pot - ${split} each!";
			playerChips += split;
			opponentChips += pot - split;
			aiOpponent.ChipStack = opponentChips;
			
			aiOpponent.ProcessHandResult(HandResult.Neutral);
		}

		ShowMessage(message);
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		GD.Print($"AI Tilt Level: {aiOpponent.Personality.TiltMeter:F1}");
		
		EndHand();
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
	
	private PokerPersonality LoadOpponentPersonality(string opponentName)
	{
		return opponentName switch
		{
			"Steve"   => PersonalityPresets.CreateSteve(),
			"Aryll" => PersonalityPresets.CreateAryll(),
			"Boy Wizard"   => PersonalityPresets.CreateBoyWizard(),
			"Apprentice"   => PersonalityPresets.CreateApprentice(),
			"Hippie"   => PersonalityPresets.CreateHippie(),
			"Cowboy"   => PersonalityPresets.CreateCowboy(),
			"King"   => PersonalityPresets.CreateKing(),
			"Old Wizard"   => PersonalityPresets.CreateOldWizard(),
			"Akalite"   => PersonalityPresets.CreateAkalite(),
			_       => throw new ArgumentException($"Unknown opponent: {opponentName}")
		};
	}
}
