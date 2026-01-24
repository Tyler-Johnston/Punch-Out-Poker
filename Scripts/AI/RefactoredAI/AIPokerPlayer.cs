using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;

/// <summary>
/// Represents an AI-controlled poker player with personality-driven behavior
/// Handles chip management, hand tracking, tilt processing, and tell / dialogue display
/// </summary>
public partial class AIPokerPlayer : Node
{
	// Core properties
	[Export] public PokerPersonality Personality { get; set; }
	[Export] public string PlayerName { get; set; }
	
	// Random seeds for per-hand variation
	public float HandRandomnessSeed { get; private set; }
	public float BetSizeSeed { get; private set; }
	public float PreflopDecisionSeed { get; private set; }
	public float FlopDecisionSeed { get; private set; }
	public float TurnDecisionSeed { get; private set; }
	public float RiverDecisionSeed { get; private set; }
	public float TrapDecisionSeed { get; private set; }
	public float AllInCommitmentSeed { get; private set; }
	public int StartingChips { get; private set; }


	// Game state
	public int ChipStack { get; set; }
	public List<Card> Hand { get; private set; }
	public bool IsFolded { get; set; }
	public bool IsAllIn { get; set; }
	public int CurrentBetThisRound { get; set; }
	
	// Decision making
	private PokerDecisionMaker decisionMaker;
	
	// Signals for UI updates
	[Signal]
	public delegate void ChipsChangedEventHandler(int newAmount);
	
	[Signal]
	public delegate void ActionTakenEventHandler(PlayerAction action, int amount);
	
	[Signal]
	public delegate void TellDisplayedEventHandler(string tellName);
	
	[Signal]
	public delegate void TiltLevelChangedEventHandler(float tiltAmount);
	
	[Signal]
	public delegate void OpponentExitedEventHandler(int exitTypeInt);
	
	public override void _Ready()
	{
		Hand = new List<Card>();
		IsFolded = false;
		IsAllIn = false;
		CurrentBetThisRound = 0;
		
		if (HasNode("PokerDecisionMaker"))
		{
			decisionMaker = GetNode<PokerDecisionMaker>("PokerDecisionMaker");
		}
		
		if (Personality == null)
		{
			GD.PushWarning($"No personality assigned to {PlayerName}, using default");
			Personality = CreateDefaultPersonality();
		}
		
		if (string.IsNullOrEmpty(PlayerName))
		{
			PlayerName = Personality.CharacterName;
		}
	}
	
	public void InitializeForMatch(int buyIn) 
	{
		StartingChips = buyIn;
		ChipStack = buyIn;
	}

	public OpponentExitType CheckForEarlyExit()
	{
		// 1. RAGE QUIT CHECK
		if (Personality.TiltMeter >= Personality.RageQuitThreshold)
		{
			GD.Print($"[OPPONENT EXIT] {PlayerName} is RAGE QUITTING! Tilt: {Personality.TiltMeter}");
			return OpponentExitType.RageQuit;
		}

		// 2. SURRENDER CHECK 
		int surrenderThresholdAmount = (int)(StartingChips * Personality.SurrenderChipPercent);

		bool isCalm = Personality.TiltMeter < 25f; 
		bool isLowChips = ChipStack <= surrenderThresholdAmount;

		if (isLowChips && isCalm)
		{
			GD.Print($"[OPPONENT EXIT] {PlayerName} is Surrendering. Chips: {ChipStack} <= {surrenderThresholdAmount}, Tilt: {Personality.TiltMeter}");
			return OpponentExitType.Surrender;
		}

		return OpponentExitType.None;
	}

	public PlayerAction MakeDecision(GameState gameState)
	{
		// Debug logging
		GD.Print($"[{PlayerName}] MakeDecision called - IsFolded: {IsFolded}, IsAllIn: {IsAllIn}, ChipStack: {ChipStack}");
		
		// Safety check: If IsAllIn but we have chips, reset it
		if (IsAllIn && ChipStack > 0)
		{
			GD.PrintErr($"[{PlayerName}] ERROR: IsAllIn=true but ChipStack={ChipStack}! Resetting IsAllIn.");
			IsAllIn = false;
		}
		
		// Only return early if actually folded/all-in
		if (IsFolded)
		{
			GD.Print($"[{PlayerName}] Folded - returning Check");
			return PlayerAction.Check;
		}
		
		if (IsAllIn)
		{
			GD.Print($"[{PlayerName}] All-in with 0 chips - returning Check");
			return PlayerAction.Check;
		}
		
		// Safety: ensure decision maker exists
		if (decisionMaker == null)
		{
			GD.PrintErr($"[{PlayerName}] DecisionMaker is null! Folding as fallback.");
			return PlayerAction.Fold;
		}
		
		// Call the actual decision logic
		GD.Print($"[{PlayerName}] Calling DecisionMaker.DecideAction()");
		PlayerAction action = decisionMaker.DecideAction(this, gameState);
		
		// Validation: can't check when facing a bet
		float toCall = gameState.CurrentBet - gameState.GetPlayerCurrentBet(this);
		if (action == PlayerAction.Check && toCall > 0)
		{
			GD.PrintErr($"[{PlayerName}] ERROR: Tried to check when facing {toCall} bet! Converting to Fold.");
			action = PlayerAction.Fold;
		}
		
		// Handle specific action execution
		int betAmount = 0;
		switch (action)
		{
			case PlayerAction.Raise:
				float handStrength = EvaluateCurrentHandStrength(gameState);
				betAmount = decisionMaker.CalculateBetSize(this, gameState, handStrength);
				break;
			case PlayerAction.AllIn:
				betAmount = ChipStack;
				IsAllIn = true;
				break;
		}
		
		EmitSignal(SignalName.ActionTaken, (int)action, betAmount);
		return action;
	}

	public void ProcessHandResult(HandResult result, int potSize, int bigBlind)
	{
		float previousTilt = Personality.TiltMeter;
		
		switch (result)
		{
			case HandResult.BadBeat:
				AddTiltSafe(20f);
				Personality.ConsecutiveLosses++;
				GD.Print($"{PlayerName} suffered a bad beat! Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.BluffCaught:
				AddTiltSafe(12f);
				GD.Print($"{PlayerName}'s bluff was caught! Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.Loss:
				Personality.ConsecutiveLosses++;
				float lossAmount = 5f * Personality.ConsecutiveLosses; // 5, 10, 15, 20...
				AddTiltSafe(lossAmount);
				GD.Print($"{PlayerName} lost (streak: {Personality.ConsecutiveLosses}). Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.AllInLoss:
				AddTiltSafe(25f);
				Personality.ConsecutiveLosses++;
				GD.Print($"{PlayerName} lost all-in! Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.Win:
				Personality.ConsecutiveLosses = 0;
				
				// DYNAMIC RECOVERY LOGIC
				float bigBlindsWon = (float)potSize / bigBlind;
				float reliefAmount = 2.0f;
				
				if (bigBlindsWon > 20) 
				{
					reliefAmount = 15.0f; // Huge pot = Huge relief
					GD.Print($"{PlayerName} won a MONSTER POT! Major relief.");
				}
				else if (bigBlindsWon > 10)
				{
					reliefAmount = 8.0f; // Big pot = Good relief
				}
				
				Personality.ReduceTilt(reliefAmount);
				GD.Print($"{PlayerName} won ({bigBlindsWon:F1} BBs). Tilt reduced by {reliefAmount}");
				break;
				
			case HandResult.Neutral:
				Personality.ReduceTilt(1f);
				break;
		}
		
		// Emit signal if tilt changed
		if (Mathf.Abs(previousTilt - Personality.TiltMeter) > 0.5f)
		{
			EmitSignal(SignalName.TiltLevelChanged, Personality.TiltMeter);
		}
	}
	
	// Helper to prevent tilt from exploding to infinity
	private void AddTiltSafe(float amount)
	{
		Personality.AddTilt(amount);
		if (Personality.TiltMeter > 100f)
		{
			Personality.TiltMeter = 100f;
		}
	}
	
	public void ResetForNewHand()
	{
		Hand.Clear();
		IsFolded = false;
		CurrentBetThisRound = 0;
		
		// Generate all seeds at start of hand for consistency
		HandRandomnessSeed = GD.Randf() - 0.5f;
		BetSizeSeed = GD.Randf();
		
		// Decision seeds per street
		PreflopDecisionSeed = GD.Randf();
		FlopDecisionSeed = GD.Randf();
		TurnDecisionSeed = GD.Randf();
		RiverDecisionSeed = GD.Randf();
		
		// Other decision seeds
		TrapDecisionSeed = GD.Randf();
		AllInCommitmentSeed = GD.Randf();
		
		if (ChipStack > 0)
		{
			IsAllIn = false;
		}
	}

	/// <summary>
	/// Get an appropriate tell based on current hand strength (for animation / logs)
	/// Returns empty string if no tell should be shown (25% of the time)
	/// </summary>
	public string GetTellForHandStrength(HandStrength strength)
	{
		string tellCategory = strength switch
		{
			HandStrength.Strong    => "strong_hand",
			HandStrength.Weak      => "weak_hand",
			HandStrength.Bluffing  => "bluffing",
			_ => ""
		};
		
		if (string.IsNullOrEmpty(tellCategory))
			return "";
		
		if (Personality.Tells.ContainsKey(tellCategory) && 
			Personality.Tells[tellCategory].Count > 0)
		{
			var tells = Personality.Tells[tellCategory];
			
			// 75% chance to show tell (makes them useful but not guaranteed)
			if (GD.Randf() < 0.75f)
			{
				int randomIndex = GD.RandRange(0, tells.Count - 1);
				string tellName = tells[randomIndex].ToString();
				
				EmitSignal(SignalName.TellDisplayed, tellName);
				return tellName;
			}
		}
		
		return "";
	}

	/// <summary>
	/// Returns a spoken dialogue line based on action + hand strength + bluffing.
	/// Uses TellReliability to decide how honest Strong/Weak/Bluffing lines are,
	/// and falls back to OnFold/OnBet/etc. when not using a tell.
	/// </summary>
	public string GetDialogueForAction(PlayerAction action, HandStrength strength, bool isBluffing)
	{
		var p = Personality;
		var dialog = p.Dialogue;

		// 1) Sometimes use spoken tells (StrongHand / WeakHand / Bluffing)
		float roll = GD.Randf();
		bool useTell = roll < p.TellReliability;
		string spokenLine;

		if (useTell)
		{
			if (isBluffing && dialog.ContainsKey("Bluffing"))
			{
				spokenLine = GetRandom(dialog["Bluffing"]);
				GD.Print($"[AI SPOKEN TELL] {spokenLine}");
				return spokenLine;
			}
			
			if (strength == HandStrength.Strong && dialog.ContainsKey("StrongHand"))
			{
				spokenLine = GetRandom(dialog["StrongHand"]);
				GD.Print($"[AI SPOKEN TELL] {spokenLine}");
				return spokenLine;
			}
			
			if (strength == HandStrength.Weak && dialog.ContainsKey("WeakHand"))
			{
				spokenLine = GetRandom(dialog["WeakHand"]);
				GD.Print($"[AI SPOKEN TELL] {spokenLine}");
				return spokenLine;
			}
		}

		// 2) Action-specific lines
		string key = action switch
		{
			PlayerAction.Fold  => "OnFold",
			PlayerAction.Check => "OnCheck",
			PlayerAction.Call  => "OnCall",
			PlayerAction.Raise => "OnRaise",
			PlayerAction.AllIn => "OnAllIn",
			_                  => null
		};

		if (key != null && dialog.ContainsKey(key))
		{
			spokenLine = GetRandom(dialog[key]);
			GD.Print($"[AI SPOKEN ACTION] {spokenLine}");
			return spokenLine;
		}

		// 3) No line this turn
		return "";
	}


	private string GetRandom(Array<string> lines)
	{
		if (lines == null || lines.Count == 0)
			return "";
		int idx = (int)GD.RandRange(0, lines.Count - 1);
		return lines[idx];
	}
	
	/// <summary>
	/// Set the decision maker
	/// </summary>
	public void SetDecisionMaker(PokerDecisionMaker dm)
	{
		decisionMaker = dm;
	}

	/// <summary>
	/// Determine current hand strength category for tell system
	/// </summary>
	public HandStrength DetermineHandStrengthCategory(GameState gameState)
	{
		float strength = EvaluateCurrentHandStrength(gameState);
		
		// Check if AI is bluffing (betting strong with weak hand)
		if (strength < 0.35f && (GD.Randf() < Personality.CurrentBluffFrequency))
		{
			return HandStrength.Bluffing;
		}
		
		if (strength > 0.65f)
			return HandStrength.Strong;
		else if (strength > 0.35f)
			return HandStrength.Medium;
		else
			return HandStrength.Weak;
	}
	
	public float EvaluateCurrentHandStrength(GameState gameState)
	{
		if (decisionMaker == null)
		{
			GD.PrintErr("DecisionMaker is null in EvaluateCurrentHandStrength! Returning default 0.5f");
			return 0.5f;
		}
		
		// Pass the exact same randomness seed this player is using for this hand
		return decisionMaker.EvaluateHandStrength(
			this.Hand, 
			gameState.CommunityCards, 
			gameState.Street, 
			this.HandRandomnessSeed
		);
	}
	
	/// <summary>
	/// Chip management methods
	/// </summary>
	public void AddChips(int amount)
	{
		ChipStack += amount;
		EmitSignal(SignalName.ChipsChanged, ChipStack);
	}
	
	public void RemoveChips(int amount)
	{
		ChipStack -= amount;
		if (ChipStack <= 0)
		{
			ChipStack = 0;
			IsAllIn = true;
		}
		EmitSignal(SignalName.ChipsChanged, ChipStack);
	}
	
	public bool CanBet(int amount)
	{
		return ChipStack >= amount;
	}
	
	/// <summary>
	/// Deal cards to this player
	/// </summary>
	public void DealCard(Card card)
	{
		Hand.Add(card);
	}
	
	/// <summary>
	/// Create a default personality if none provided
	/// </summary>
	private PokerPersonality CreateDefaultPersonality()
	{
		return new PokerPersonality
		{
			CharacterName = "AI Player",
			BaseAggression = 0.5f,
			BaseBluffFrequency = 0.3f,
			BaseFoldThreshold = 0.5f,
			BaseRiskTolerance = 0.5f,
			TiltSensitivity = 0.4f,
			CallTendency = 0.5f,
			Chattiness = 0.5f,
			TellReliability = 0.5f
		};
	}
	
	// Helper property to get state enum
	public TiltState CurrentTiltState
	{
		get
		{
			float t = Personality.TiltMeter;
			if (t >= 50f) return TiltState.Monkey;
			if (t >= 25f) return TiltState.Steaming;
			if (t >= 10f) return TiltState.Annoyed;
			return TiltState.Zen;
		}
	}
	
	/// <summary>
	/// Debug information
	/// </summary>
	public string GetDebugInfo()
	{
		return $"{PlayerName} | Chips: {ChipStack} | Tilt: {Personality.TiltMeter:F1} | " +
			   $"Aggression: {Personality.CurrentAggression:F2} | Losses: {Personality.ConsecutiveLosses}";
	}
}
