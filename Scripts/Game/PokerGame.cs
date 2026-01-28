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
	private Sprite2D faceSprite;
	private PanelContainer speechBubble;
	
	// game flow
	private Street currentStreet = Street.Preflop;
	private int playerChips = 100;
	private int opponentChips = 100;
	private int pot = 0;
	private int betAmount = 20;
	private int currentBet = 0;
	private int playerBet = 0;
	private int opponentBet = 0;
	private int smallBlind = 5;
	private int bigBlind = 10;
	private int raisesThisStreet = 0;
	private const int MAX_RAISES_PER_STREET = 4;

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

	// Timer for idle tells
	private Timer tellTimer;

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
		Control buttonUI = hudControl.GetNode<Control>("ButtonUI");
		Control actionButtons = buttonUI.GetNode<Control>("ActionButtons");
		foldButton = actionButtons.GetNode<Button>("FoldButton");
		checkCallButton = actionButtons.GetNode<Button>("CheckCallButton");
		betRaiseButton = actionButtons.GetNode<Button>("BetRaiseButton");
		cashOutButton = buttonUI.GetNode<Button>("CashOutButton");

		// labels
		playerStackLabel = hudControl.GetNode<Label>("PlayerStackLabel");
		opponentStackLabel = hudControl.GetNode<Label>("OpponentStackLabel");
		potLabel = hudControl.GetNode<Label>("PotLabel");
		gameStateLabel = hudControl.GetNode<Label>("GameStateLabel");
		playerHandType = hudControl.GetNode<Label>("PlayerHandType");
		opponentHandType = hudControl.GetNode<Label>("OpponentHandType");
		betSliderLabel = hudControl.GetNode<Label>("BetSliderLabel");
		opponentDialogueLabel = hudControl.GetNode<Label>("OpponentDialogue");
		
		// opponent picture
		opponentPortrait = hudControl.GetNode<TextureRect>("OpponentPortrait");
		
		// speech bubble
		speechBubble = hudControl.GetNode<PanelContainer>("SpeechBubble");
		
		// table color
		tableColor = hudControl.GetNode<TextureRect>("ColorRect");
		
		// slider
		betSlider = hudControl.GetNode<HSlider>("BetSlider");
		
		// faceSprite
		faceSprite = hudControl.GetNode<Sprite2D>("FaceSprite");
		
		// audio players
		sfxPlayer = GetNode<SFXPlayer>("SFXPlayer");
		musicPlayer = GetNode<AudioStreamPlayer>("MusicPlayer");  
		
		// set on-press handlers
		foldButton.Pressed += OnFoldPressed;
		checkCallButton.Pressed += OnCheckCallPressed;
		betRaiseButton.Pressed += OnBetRaisePressed;
		betSlider.ValueChanged += OnBetSliderValueChanged;

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

		//musicPlayer.Play();
		SetTableColor();
		UpdateHud();
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
	
	private (int minBet, int maxBet) GetLegalBetRange()
	{
		int amountToCall = currentBet - playerBet;
		int maxBet = playerChips - amountToCall; 
		
		if (maxBet <= 0) return (0, 0);

		bool opening = currentBet == 0;
		int minBet;

		if (opening)
		{
			minBet = Math.Min(bigBlind, maxBet);
		}
		else
		{
			int minRaiseIncrement = (amountToCall == 0) ? bigBlind : amountToCall;
			minRaiseIncrement = Math.Max(minRaiseIncrement, bigBlind);

			minBet = minRaiseIncrement;
			
			// cap at calculated maxBet
			if (minBet > maxBet)
			{
				minBet = maxBet; 
			}
		}

		if (minBet > maxBet) minBet = maxBet;
		return (minBet, maxBet);
	}

	private void ResetBettingRound()
	{
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		raisesThisStreet = 0;
		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;
	}

	public void AddToPot(bool isPlayer, int amount)
	{
		pot += amount;
		if (isPlayer)
			playerContributed += amount;
		else
			opponentContributed += amount;
	}

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

	private void OnTellTimerTimeout()
	{
		if (!handInProgress || isShowdownInProgress || aiOpponent.IsFolded) return;
		if (isProcessingAIAction) return; 
		if (aiOpponent.IsAllIn || playerIsAllIn) return;
		if (currentStreet == Street.Preflop) return;

		if (isPlayerTurn)
		{
			ShowTell(false);
		}
	}

	private async void StartNewHand()
	{
		if (!waitingForNextGame && handInProgress) return; 
		waitingForNextGame = false;
		opponentDialogueLabel.Text = "";
		SetExpression(Expression.Neutral);

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}
		
		// reset AI opponent for new hand
		aiOpponent.ResetForNewHand();
		aiOpponent.ChipStack = opponentChips;

		GD.Print("\\\\n=== New Hand ===");
		ShowMessage("");

		cashOutButton.Disabled = true;
		cashOutButton.Visible = false;
		speechBubble.Visible = false;
		betSlider.Visible = true;
		betSliderLabel.Visible = true;
		foldButton.Visible = true;
		foldButton.Disabled = true;
		betRaiseButton.Visible = true;
		betRaiseButton.Disabled = true;
		potLabel.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";
		aiStrengthAtAllIn = 0f;

		deck = new Deck();
		deck.Shuffle();
		
		pot = 0;
		playerContributed = 0;
		opponentContributed = 0;
		playerTotalBetsThisHand = 0;
		
		aiBluffedThisHand = false;
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
		
		tellTimer.Start();
		
		currentStreet = Street.Preflop;
		handInProgress = true;
		
		UpdateOpponentVisuals();

		playerHasButton = !playerHasButton;
		if (playerHasButton)
		{
			// human player is Small Blind
			int sbAmount = Math.Min(smallBlind, playerChips); 
			playerChips -= sbAmount;
			AddToPot(true, sbAmount);
			playerBet = sbAmount;
			if (playerChips == 0) playerIsAllIn = true;

			// opponent is Big Blind
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
			// human player is Big Blind
			int bbAmount = Math.Min(bigBlind, playerChips); 
			playerChips -= bbAmount;
			AddToPot(true, bbAmount);
			playerBet = bbAmount;
			if (playerChips == 0) playerIsAllIn = true;

			// opponent is Small Blind
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
		
		// if both are all-in (or player is forced all-in), skip betting logic
		if (playerIsAllIn || opponentIsAllIn)
		{
			GD.Print("[START HAND] Blind forced All-In! Skipping to next street.");
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
		}
		else if (!isPlayerTurn)
		{
			// Standard AI turn
			GetTree().CreateTimer(1.15).Timeout += () => CheckAndProcessAITurn();
		}
	}

	private async void EndHand()
	{
		tellTimer.Stop();
		
		if (isShowdownInProgress) return;
		
		pot = 0;
		handInProgress = false;
		waitingForNextGame = true;

		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}
		
		// check for rage quit or surrender
		OpponentExitType exitType = aiOpponent.CheckForEarlyExit();
		if (exitType != OpponentExitType.None)
		{
			await ToSignal(GetTree().CreateTimer(1.5f), SceneTreeTimer.SignalName.Timeout);
			if (exitType == OpponentExitType.RageQuit)
			{
				ShowMessage($"{aiOpponent.PlayerName} RAGE QUITS!");
				GD.Print($"[GAME OVER] Opponent Rage Quit! Tilt: {aiOpponent.Personality.TiltMeter}");
			}
			else if (exitType == OpponentExitType.Surrender)
			{
				ShowMessage($"{aiOpponent.PlayerName} SURRENDERS!");
				GD.Print($"[GAME OVER] Opponent Surrendered. Chips: {aiOpponent.ChipStack}");
			}
			await ToSignal(GetTree().CreateTimer(3.0f), SceneTreeTimer.SignalName.Timeout);
			HandleGameOver(opponentSurrendered: true);
			return;
		}

		UpdateHud();
		RefreshBetSlider();
	}
	
	private bool IsGameOver()
	{
		if (isShowdownInProgress) return false;
		return playerChips <= 0 || opponentChips <= 0;
	}
	
	// Helper to show emotion momentarily then reset to neutral
	private async void ShowMomentaryExpression(Expression expr, float duration)
	{
		SetExpression(expr);
		
		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
		
		if (handInProgress && !isShowdownInProgress && !isProcessingAIAction && !aiOpponent.IsFolded)
		{
			SetExpression(Expression.Neutral);
		}
	}

	private void ShowTell(bool forceTell = false)
	{
		GameState gameState = CreateGameState();
		HandStrength strength = aiOpponent.DetermineHandStrengthCategory(gameState);

		if (currentStreet == Street.Preflop && forceTell)
		{
			float duration = 3.0f; 

			bool isTilted = aiOpponent.CurrentTiltState != TiltState.Zen;
			bool isSteaming = aiOpponent.CurrentTiltState == TiltState.Steaming || 
							  aiOpponent.CurrentTiltState == TiltState.Monkey;

			// --- Scenario A: Weak Hand ---
			if (strength == HandStrength.Weak)
			{
				bool hasHighCard = opponentHand.Exists(c => c.Rank >= Rank.King);
				bool isSuited = (opponentHand.Count == 2) && (opponentHand[0].Suit == opponentHand[1].Suit);
				
				if (hasHighCard || isSuited)
				{
					SetExpression(Expression.Neutral);
					string reason = hasHighCard ? "high card" : "suited";
					GD.Print($"[TELL-PREFLOP] {currentOpponentName} is NEUTRAL (weak but {reason})");
					return;
				}

				// Tilted reaction to genuine trash
				if (isSteaming || (isTilted && GD.Randf() < 0.7f))
				{
					ShowMomentaryExpression(Expression.Angry, duration);
					GD.Print($"[TELL-PREFLOP] {currentOpponentName} is ANGRY (bad cards + tilt)");
					return;
				}

				// Normal disappointment for trash hands (increased to 60% for readability)
				if (GD.Randf() < 0.6f)
				{
					ShowMomentaryExpression(Expression.Sad, duration);
					GD.Print($"[TELL-PREFLOP] {currentOpponentName} is SAD (bad cards)");
					return;
				}
				
				// Remaining 40% maintains poker face (Neutral)
				SetExpression(Expression.Neutral);
				GD.Print($"[TELL-PREFLOP] {currentOpponentName} maintained poker face (trash hand hidden)");
				return;
			}
			
			// --- Scenario B: Medium Hand ---
			if (strength == HandStrength.Medium)
			{
				float bluffExpressionChance = aiOpponent.Personality.BaseBluffFrequency;

				// Path 1: ACTING (Bluffing / Deception)
				if (GD.Randf() < bluffExpressionChance)
				{
					if (GD.Randf() > 0.5f) {
						ShowMomentaryExpression(Expression.Happy, duration);
						GD.Print($"[TELL-PREFLOP] {currentOpponentName} is ACTING HAPPY (Medium Hand)");
					} else {
						ShowMomentaryExpression(Expression.Sad, duration);
						GD.Print($"[TELL-PREFLOP] {currentOpponentName} is ACTING SAD (Medium Hand)");
					}
					return;
				}
				
				if (GD.Randf() < 0.35f)
				{
					ShowMomentaryExpression(Expression.Worried, duration); 
					GD.Print($"[TELL-PREFLOP] {currentOpponentName} is WORRIED (Medium Hand - genuine uncertainty)");
				}
				else
				{
					SetExpression(Expression.Neutral);
					GD.Print($"[TELL-PREFLOP] {currentOpponentName} is NEUTRAL (Medium Hand - stoic)");
				}
				return;
			}

			// --- Scenario C: Strong Hand (Premium) ---
			if (strength == HandStrength.Strong)
			{
				bool attemptsPokerFace = GD.Randf() > aiOpponent.Personality.BaseBluffFrequency;
				
				if (!attemptsPokerFace) 
				{
					 ShowMomentaryExpression(Expression.Happy, duration);
					 GD.Print($"[TELL-PREFLOP] {currentOpponentName} is HAPPY (premium hand leaked)");
					 return;
				}
				else
				{
					GD.Print($"[TELL-PREFLOP] {currentOpponentName} maintained poker face (premium hand hidden)");
					SetExpression(Expression.Neutral);
					return;
				}
			}
			
			// Default: No strong reaction
			SetExpression(Expression.Neutral);
			return;
		}

		// ---------------------------------------------------------
		// IDLE TELLS (Post-Flop / Timer)
		// ---------------------------------------------------------
		float idleDuration = 1.5f; 
		float baseTellChance = 0.20f;
		float tiltModifier = (float)aiOpponent.CurrentTiltState * 0.15f; 
		
		if (!forceTell && GD.Randf() > (baseTellChance + tiltModifier))
		{
			return;
		}

		string tellCategory = "weak_hand";
		if (strength == HandStrength.Strong) 
		{
			tellCategory = "strong_hand";
		}
		else if (strength == HandStrength.Bluffing) 
		{
			tellCategory = "bluffing";
		}
		else if (strength == HandStrength.Medium) 
		{
			// Randomize "uncertainty" -> 40% look confident, 60% look worried.
			tellCategory = (GD.Randf() > 0.6f) ? "strong_hand" : "weak_hand";
		}

		bool canAct = aiOpponent.CurrentTiltState < TiltState.Steaming;
		float actingChance = 1.0f - aiOpponent.Personality.TellReliability;
	
		if (strength == HandStrength.Bluffing) actingChance += 0.10f;
		
		bool isActing = canAct && (GD.Randf() < actingChance);

		if (isActing)
		{
			// Acting logic refinement
			if (tellCategory == "strong_hand") 
				tellCategory = "weak_hand"; // Trapping
			else if (tellCategory == "weak_hand") 
				tellCategory = "strong_hand"; // Bluffing
			else if (tellCategory == "bluffing")
				tellCategory = "strong_hand"; // Selling the bluff
		}
		
		// Retrieve expression from personality map
		if (aiOpponent.Personality.Tells.ContainsKey(tellCategory))
		{
			var possibleTells = aiOpponent.Personality.Tells[tellCategory];
			if (possibleTells.Count > 0)
			{
				string tellString = possibleTells[GD.RandRange(0, possibleTells.Count - 1)];
				if (Enum.TryParse(tellString, true, out Expression expr))
				{
					ShowMomentaryExpression(expr, idleDuration);
					GD.Print($"[TELL] {currentOpponentName} shows {tellString} ({tellCategory}) Acting={isActing}");
				}
			}
		}
	}
	
	/// <summary>
	/// Creates current game state snapshot
	/// </summary>
	private GameState CreateGameState()
	{
		var state = new GameState
		{
			CommunityCards = new List<Card>(communityCards),
			PotSize = pot,
			CurrentBet = currentBet,
			Street = currentStreet,
			BigBlind = bigBlind,
			IsAIInPosition = DetermineAIPosition()
		};
		state.SetPlayerBet(aiOpponent, opponentBet);
		return state;
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
