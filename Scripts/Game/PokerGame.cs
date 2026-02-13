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
	private Label handTypeLabel;
	
	private PanelContainer opponentFrame;
	private PanelContainer gameStatePanel;
	private PanelContainer dashboardTopPanel;
	private PanelContainer dashboardBottomPanel;
	
	private Texture2D foldBtnImg;
	private Texture2D checkBtnImg;
	private Texture2D callBtnImg;
	private Texture2D betBtnImg;
	private Texture2D raiseBtnImg;
	
	private TextureRect mainTableRect;
	private TextureRect miniTableRect;
	
	private Node2D opponentView;
	private Node2D potArea;
	private Node2D playerArea;
	private Node2D opponentArea;
	private Node2D communityCardsArea;
	private Node2D betweenHandsUI;
	private Node2D activePlayUI;
	
	private Control actionButtonsContainer;
	private Control actionButtons;
	private Control sliderUI;
	
	// chip display containers
	private GridContainer playerChipGridBox;
	private GridContainer opponentChipGridBox;
	private GridContainer chipContainer;
	
	private ColorRect titleBar;
	private Sprite2D faceSprite;
	private SpeechBubble speechBubble;

	// game flow
	private Street currentStreet = Street.Preflop;
	private int playerChips = 100;
	private int opponentChips = 100;
	private int playerChipsInPot = 0;
	private int opponentChipsInPot = 0;
	private int pot = 0;
	private int displayPot = 0; 
	private int lastDisplayedPot = -1;
	private int lastPotLabel = -1;
	private int lastDisplayedPlayerChips = -1;
	private int lastDisplayedOpponentChips = -1;
	private int previousBet = 0;
	private int betAmount = 20;
	private int currentBet = 0;
	private int playerBet = 0;
	private int opponentBet = 0;
	private int smallBlind = 5;
	private int bigBlind = 10;
	private int playerContributed = 0;
	private int opponentContributed = 0;
	private int playerTotalBetsThisHand = 0;
	private int lastRaiseAmount = 0;
	private float aiStrengthAtAllIn = 0f; 
	
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
	private bool playerCanReopenBetting = true;
	private bool opponentCanReopenBetting = true;

	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();

	// ais
	private AIPokerPlayer aiOpponent;
	private PokerDecisionMaker decisionMaker;
	private DialogueManager dialogueManager; 
	private string currentOpponentName;
	private int buyInAmount;
	private string lastHandDescription = "";
	
	// audio
	private SFXPlayer sfxPlayer;
	private MusicPlayer musicPlayer;

	// timer for idle tells
	private Timer tellTimer;

	public override void _Ready()
	{
		// Load resources
		cardVisualScene = GD.Load<PackedScene>("res://Scenes/CardVisual.tscn");
		
		// === GAME WORLD ===
		miniTableRect = GetNode<TextureRect>("%MiniTable");
		mainTableRect = GetNode<TextureRect>("%MainTable");
		
		// === AREAS ===
		opponentArea = GetNode<Node2D>("%OpponentArea");
		communityCardsArea = GetNode<Node2D>("%CommunityCardsArea");
		playerArea = GetNode<Node2D>("%PlayerArea");
		opponentView = GetNode<Control>("CanvasLayer/Control").GetNode<Node2D>("OpponentView");
		potArea = GetNode<Control>("CanvasLayer/Control").GetNode<Node2D>("PotArea");
		betweenHandsUI = GetNode<Node2D>("%BetweenHandsUI");
		activePlayUI = GetNode<Node2D>("%ActivePlayUI");
		
		// === CHIP CONTAINERS ===
		playerChipGridBox = playerArea.GetNode<GridContainer>("PlayerChipGridBox");
		opponentChipGridBox = GetNode<GridContainer>("%OpponentChipGridBox");
		chipContainer = potArea.GetNode<GridContainer>("%PotGridBox");
		
		// === PANELS ===
		gameStatePanel = GetNode<PanelContainer>("%GameStatePanel");
		dashboardTopPanel = GetNode<PanelContainer>("%TopPanel");
		dashboardBottomPanel = GetNode<PanelContainer>("%BottomPanel");
		
		// === CARDS ===
		SetupCards();
		
		// === UI ELEMENTS ===
		SetupButtons();
		SetupLabels();
		
		// === AUDIO ===
		SetupAudio();
		
		// === BEHAVIOR ===
		SetupSpeechBubble();
		SetupSlider();
		SetupOpponent();
		SetupEventHandlers();
		
		// === GAME INIT ===
		SetupGameState();
		InitializeAI();
		InitializeUI();
		StartNewHand();
	}

	private void SetupCards()
	{
		playerCard1 = playerArea.GetNode<CardVisual>("PlayerCard1");
		playerCard2 = playerArea.GetNode<CardVisual>("PlayerCard2");
		opponentCard1 = opponentArea.GetNode<CardVisual>("OpponentCard1");
		opponentCard2 = opponentArea.GetNode<CardVisual>("OpponentCard2");
		
		flop1 = GetNode<CardVisual>("%Flop1");
		flop2 = GetNode<CardVisual>("%Flop2");
		flop3 = GetNode<CardVisual>("%Flop3");
		turnCard = GetNode<CardVisual>("%Turn");
		riverCard = GetNode<CardVisual>("%River");
	}

	private void SetupButtons()
	{
		actionButtons = GetNode<Control>("%ActionButtons");
		foldButton = actionButtons.GetNode<Button>("FoldButton");
		checkCallButton = actionButtons.GetNode<Button>("CheckCallButton");
		betRaiseButton = actionButtons.GetNode<Button>("BetRaiseButton");
		cashOutButton = GetNode<Button>("%CashOutButton");
		nextHandButton = GetNode<Button>("%NextHandButton");
	}

	private void SetupLabels()
	{
		playerStackLabel = GetNode<Label>("%PlayerStackLabel");
		playerStackLabel2 = GetNode<Label>("%PlayerStackLabel2");
		playerEarningsLabel = GetNode<Label>("%PlayerEarningsLabel");
		opponentStackLabel = GetNode<Label>("%OpponentStackLabel");
		handTypeLabel = GetNode<Label>("%HandTypeLabel");
		potLabel = GetNode<Label>("%PotLabel");
		gameStateLabel = GetNode<Label>("%GameStateLabel");
	}

	private void SetupAudio()
	{
		sfxPlayer = GetNode<SFXPlayer>("SFXPlayer");
		musicPlayer = GetNode<MusicPlayer>("MusicPlayer");
	}

	private void SetupSpeechBubble()
	{
		speechBubble = opponentView.GetNode<SpeechBubble>("SpeechBubble");
		speechBubble.AudioPlayer = sfxPlayer;
	}

	private void SetupSlider()
	{
		sliderUI = GetNode<Control>("%SliderUI");
		betSlider = GetNode<HSlider>("%BetSlider");
		thirdPot = GetNode<Button>("%ThirdPot");
		halfPot = GetNode<Button>("%HalfPot");
		standardPot = GetNode<Button>("%StandardPot");
		twoThirdsPot = GetNode<Button>("%TwoThirdsPot");
		allInPot = GetNode<Button>("%AllInPot");
	}

	private void SetupOpponent()
	{
		opponentFrame = GetNode<PanelContainer>("%OpponentFrame");
		faceSprite = GetNode<Sprite2D>("%FaceSprite");
		titleBar = GetNode<ColorRect>("%TitleBar");
	}

	private void SetupEventHandlers()
	{
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
		
		// Tell Timer
		tellTimer = new Timer();
		tellTimer.WaitTime = 4.0f;
		tellTimer.OneShot = false;
		tellTimer.Timeout += OnTellTimerTimeout;
		AddChild(tellTimer);
	}

	private void SetupGameState()
	{
		// Opponent info
		currentOpponentName = GameManager.Instance.CurrentOpponentName;
		buyInAmount = GameManager.Instance.CurrentBuyIn;
		
		if (string.IsNullOrEmpty(currentOpponentName))
		{
			currentOpponentName = "Steve";
			buyInAmount = 50;
			GD.PushWarning("No opponent selected, defaulting to Steve");
		}
		
		GD.Print($"---------- Player VS {currentOpponentName} ----------");
		
		playerChips = buyInAmount;
		opponentChips = buyInAmount;
		
		// Blinds
		bigBlind = Math.Max(2, buyInAmount / 50);
		if (bigBlind % 2 != 0) bigBlind++;
		smallBlind = bigBlind / 2;
		betAmount = bigBlind;
		playerHasButton = false;
		
		GD.Print($"Blinds: {smallBlind}/{bigBlind}");
	}

	private void InitializeAI()
	{
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
		
		// Music
		musicPlayer.PlayTrack($"{currentOpponentName.ToLower()}_bg_music");
		
		// Log personality
		var personality = aiOpponent.Personality;
		GD.Print($"Personality Stats:");
		GD.Print($"  Aggression: {personality.BaseAggression:F2}");
		GD.Print($"  Bluff Freq: {personality.BaseBluffFrequency:F2}");
		GD.Print($"  Call Tendency: {personality.CallTendency:F2}");
		GD.Print($"  Tilt Sensitivity: {personality.TiltSensitivity:F2}");
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
