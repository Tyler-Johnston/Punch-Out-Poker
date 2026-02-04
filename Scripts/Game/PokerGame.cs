using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokerGame : Node2D
{
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
	private Button cashOutButton;
	private Button nextHandButton;
	private Button thirdPot;
	private Button halfPot;
	private Button standardPot;
	private Button twoThirdsPot;
	private Button allInPot;

	private Label playerStackLabel;
	private Label playerStackLabel2;
	private Label playerEarningsLabel;
	private Label opponentStackLabel;
	private Label potLabel;
	private Label gameStateLabel;
	private Label checkCallLabel;
	private Label betRaiseLabel;
	
	private PanelContainer gameStatePanel;
	
	private Texture2D foldBtnImg;
	private Texture2D checkBtnImg;
	private Texture2D callBtnImg;
	private Texture2D betBtnImg;
	private Texture2D raiseBtnImg;
	
	[Export] public PanelContainer OpponentFrame;
	[Export] public TextureRect MainTableRect;
	[Export] public TextureRect MiniTableRect;
	
	private Node2D opponentView;
	private Node2D actionView;
	private Node2D potArea;
	private Node2D playerArea;
	private Node2D betweenHandsUI;
	
	private Control actionButtons;
	private Control sliderUI;
	
	// Chip display containers
	private GridContainer PlayerChipGridBox;      // Player's chips in current betting round
	private GridContainer OpponentChipGridBox;    // Opponent's chips in current betting round
	private GridContainer chipContainer;          // Main pot display (center of table)
	
	private Sprite2D faceSprite;
	private SpeechBubble speechBubble;

	// game flow
	private Street currentStreet = Street.Preflop;
	private int playerChips = 100;
	private int opponentChips = 100;
	private int playerChipsInPot = 0;      // Amount player has in current betting round
	private int opponentChipsInPot = 0;    // Amount opponent has in current betting round
	private int pot = 0;                   // Total pot (all streets combined)
	private int displayPot = 0; 
	private int _lastDisplayedPot = -1;
	private int _lastPotLabel = -1;
	private int _lastDisplayedPlayerChips = -1;
	private int _lastDisplayedOpponentChips = -1;
	private int previousBet = 0;
	private int betAmount = 20;
	private int currentBet = 0;
	private int playerBet = 0;
	private int opponentBet = 0;
	private int smallBlind = 5;
	private int bigBlind = 10;

	// tracking
	public float aiStrengthAtAllIn = 0f; 

	// side pot and contribution tracking
	private int playerContributed = 0;
	private int opponentContributed = 0;
	private int playerTotalBetsThisHand = 0;
	
	// game state flags
	private bool isMatchComplete = false;
	private bool isPlayerTurn = true;
	private bool playerHasButton = false;
	private bool handInProgress = false;
	private bool waitingForNextGame = false;
	private bool playerIsAllIn = false;
	private bool opponentIsAllIn = false;
	private bool isProcessingAIAction = false;
	private bool aiBluffedThisHand = false;
	private bool isShowdownInProgress = false;
	private bool playerHasActedThisStreet = false;
	private bool opponentHasActedThisStreet = false;
	
	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();

	// ais
	private AIPokerPlayer aiOpponent;
	private PokerDecisionMaker decisionMaker;
	private DialogueManager dialogueManager; 
	private string currentOpponentName;
	private int buyInAmount;
	
	// audio
	private SFXPlayer sfxPlayer;
	private AudioStreamPlayer musicPlayer;

	// timer for idle tells
	private Timer tellTimer;

	public override void _Ready()
	{
		Control hudControl = GetNode<Control>("CanvasLayer/Control");
		cardVisualScene = GD.Load<PackedScene>("res://Scenes/CardVisual.tscn");
		
		// ui areas
		Node2D opponentArea = GetNode<Node2D>("%OpponentArea");
		Node2D communityCardsArea = hudControl.GetNode<Node2D>("CommunityCardsArea");
		playerArea = GetNode<Node2D>("%PlayerArea");
		opponentView = hudControl.GetNode<Node2D>("OpponentView");
		potArea = hudControl.GetNode<Node2D>("PotArea");
		betweenHandsUI = GetNode<Node2D>("%BetweenHandsUI");
		
		// Get chip display containers
		PlayerChipGridBox = playerArea.GetNode<GridContainer>("PlayerChipGridBox");
		OpponentChipGridBox = GetNode<GridContainer>("%OpponentChipGridBox");
		chipContainer = potArea.GetNode<GridContainer>("%PotGridBox");
		gameStatePanel = GetNode<PanelContainer>("%GameStatePanel");

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
		actionButtons = GetNode<Control>("%ActionButtons");
		actionView = GetNode<Node2D>("%ActionView");
		foldButton = actionButtons.GetNode<Button>("FoldButton");
		checkCallButton = actionButtons.GetNode<Button>("CheckCallButton");
		betRaiseButton = actionButtons.GetNode<Button>("BetRaiseButton");
		cashOutButton = GetNode<Button>("%CashOutButton");
		nextHandButton = GetNode<Button>("%NextHandButton");
		
		// labels
		playerStackLabel = GetNode<Label>("%PlayerStackLabel");
		playerStackLabel2 = GetNode<Label>("%PlayerStackLabel2");
		playerEarningsLabel = GetNode<Label>("%PlayerEarningsLabel");
		opponentStackLabel = opponentView.GetNode<Label>("OpponentStackLabel");
		potLabel = GetNode<Label>("%PotLabel");
		gameStateLabel = GetNode<Label>("%GameStateLabel");
		
		// audio players
		sfxPlayer = GetNode<SFXPlayer>("SFXPlayer");
		musicPlayer = GetNode<AudioStreamPlayer>("MusicPlayer");  
		
		// speech bubble
		speechBubble = opponentView.GetNode<SpeechBubble>("SpeechBubble");
		speechBubble.AudioPlayer = sfxPlayer; 
		
		// slider
		sliderUI = GetNode<Control>("%SliderUI");
		betSlider = GetNode<HSlider>("%BetSlider");
		thirdPot = GetNode<Button>("%ThirdPot");
		halfPot = GetNode<Button>("%HalfPot");
		standardPot = GetNode<Button>("%StandardPot");
		twoThirdsPot = GetNode<Button>("%TwoThirdsPot");
		allInPot = GetNode<Button>("%AllInPot");
		
		// faceSprite
		faceSprite = GetNode<Sprite2D>("%FaceSprite"); 
		
		// set on-press handlers
		foldButton.Pressed += OnFoldPressed;
		checkCallButton.Pressed += OnCheckCallPressed;
		betRaiseButton.Pressed += OnBetRaisePressed;
		cashOutButton.Pressed += OnCashOutPressed;
		nextHandButton.Pressed += OnNextHandPressed;
		betSlider.ValueChanged += OnBetSliderValueChanged;
		thirdPot.Pressed += () => OnPotSizeButtonPressed(0.33f);
		halfPot.Pressed += () => OnPotSizeButtonPressed(0.5f);
		standardPot.Pressed += () => OnPotSizeButtonPressed(1.0f);
		twoThirdsPot.Pressed += () => OnPotSizeButtonPressed(0.67f);
		allInPot.Pressed += OnAllInButtonPressed;

		// Initialize Tell Timer
		tellTimer = new Timer();
		tellTimer.WaitTime = 4.0f;
		tellTimer.OneShot = false;
		tellTimer.Timeout += OnTellTimerTimeout;
		AddChild(tellTimer);

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
		aiOpponent.InitializeForMatch(buyInAmount);
		speechBubble.VoicePitch = aiOpponent.Personality.VoicePitch;

		decisionMaker = new PokerDecisionMaker();
		AddChild(aiOpponent); 
		aiOpponent.SetDecisionMaker(decisionMaker);
		
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

		InitializeUI();
		StartNewHand();
	}

	public override void _ExitTree()
	{
		if (tellTimer != null)
		{
			tellTimer.Stop();
			tellTimer.QueueFree();
		}
	}
}
