using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;
using GodotDict = Godot.Collections.Dictionary<string, Godot.Collections.Array<string>>;

/// <summary>
/// Represents an AI-controlled poker player with personality-driven behavior.
/// 
/// Notes:
/// - ChipStack is synced by the game engine (PokerGame) and is effectively display/state for AI.
/// - This class should not be the authority on betting or pot logic; PokerGame executes actions.
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

	// Game state (synced by engine)
	public int ChipStack { get; set; }
	public List<Card> Hand { get; private set; } = new();
	public bool IsFolded { get; set; }
	public bool IsAllIn { get; set; }

	// Legacy/optional: keep if other code still references it
	public int CurrentBetThisRound { get; set; }

	// Decision making (set by engine or by scene composition)
	private PokerDecisionMaker decisionMaker;

	// Signals for UI updates
	[Signal] public delegate void ChipsChangedEventHandler(int newAmount);
	[Signal] public delegate void ActionTakenEventHandler(PlayerAction action, int amount);
	[Signal] public delegate void TellDisplayedEventHandler(string tellName);
	[Signal] public delegate void TiltLevelChangedEventHandler(float tiltAmount);
	[Signal] public delegate void OpponentExitedEventHandler(int exitTypeInt);

	public override void _Ready()
	{
		Hand ??= new List<Card>();
		IsFolded = false;
		IsAllIn = false;
		CurrentBetThisRound = 0;

		// Optional: allow scene composition where DM is a child
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
		IsAllIn = (ChipStack <= 0);
	}

	/// <summary>
	/// Engine calls this to sync stack and IsAllIn state.
	/// Prefer this over direct ChipStack assignments if you want the UI signal.
	/// </summary>
	public void SetChipStack(int amount)
	{
		ChipStack = Math.Max(0, amount);
		IsAllIn = (ChipStack <= 0);
		EmitSignal(SignalName.ChipsChanged, ChipStack);
	}

	public OpponentExitType CheckForEarlyExit()
	{
		// Rage quit
		if (Personality.TiltMeter >= Personality.RageQuitThreshold)
		{
			GD.Print($"\n!!! {PlayerName} RAGE QUITS (Tilt {Personality.TiltMeter:F1}) !!!");
			return OpponentExitType.RageQuit;
		}

		// Surrender (low chips + calm)
		int surrenderThresholdAmount = (int)(StartingChips * Personality.SurrenderChipPercent);
		bool isLowChips = ChipStack <= surrenderThresholdAmount;
		bool isCalm = CurrentTiltState == TiltState.Zen;

		if (isLowChips && isCalm)
		{
			GD.Print($"\n!!! {PlayerName} SURRENDERS (Chips {ChipStack} <= {surrenderThresholdAmount}) !!!");
			return OpponentExitType.Surrender;
		}

		return OpponentExitType.None;
	}

	public PlayerAction MakeDecision(GameState gameState)
	{
		GameManager.LogVerbose($"[{PlayerName}] MakeDecision - Folded: {IsFolded}, AllIn: {IsAllIn}, ChipStack: {ChipStack}");

		// Defensive: if IsAllIn but we have chips, clear it (engine should sync it anyway)
		if (IsAllIn && ChipStack > 0)
		{
			GD.PrintErr($"[{PlayerName}] ERROR: IsAllIn=true but ChipStack={ChipStack}! Resetting IsAllIn.");
			IsAllIn = false;
		}

		if (IsFolded)
		{
			GameManager.LogVerbose($"[{PlayerName}] Folded - returning Check");
			return PlayerAction.Check;
		}

		if (IsAllIn)
		{
			GameManager.LogVerbose($"[{PlayerName}] All-in with 0 chips - returning Check");
			return PlayerAction.Check;
		}

		if (decisionMaker == null)
		{
			GD.PrintErr($"[{PlayerName}] DecisionMaker is null! Folding as fallback.");
			return PlayerAction.Fold;
		}

		PlayerAction action = decisionMaker.DecideAction(this, gameState);

		// Validation: can't check when facing a bet
		float toCall = gameState.CurrentBet - gameState.GetPlayerCurrentBet(this);
		if (action == PlayerAction.Check && toCall > 0)
		{
			GD.PrintErr($"[{PlayerName}] ERROR: Tried to check when facing {toCall} bet! Converting to Fold.");
			action = PlayerAction.Fold;
		}

		// PokerGame executes/sizes actions; we emit action for UI/telemetry only
		EmitSignal(SignalName.ActionTaken, (int)action, 0);
		return action;
	}

	public void OnFolded(float betRatio)
	{
		IsFolded = true;

		float tiltPenalty = betRatio switch
		{
			>= 1.0f => 8.0f,
			> 0.6f => 4.0f,
			> 0.3f => 2.0f,
			_ => 0f
		};

		if (tiltPenalty > 0)
		{
			GameManager.LogVerbose($"[TILT] Bullied. Ratio {betRatio:F2}. Tilt +{tiltPenalty}.");
			Personality.AddTilt(tiltPenalty);
		}
	}

	public void ProcessHandResult(HandResult result, int potSize, int bigBlind)
	{
		float previousTilt = Personality.TiltMeter;

		switch (result)
		{
			case HandResult.BadBeat:
				AddTiltSafe(20f);
				Personality.ConsecutiveLosses++;
				break;

			case HandResult.BluffCaught:
				AddTiltSafe(12f);
				GameManager.LogVerbose($"{PlayerName}'s bluff was caught! Tilt: {Personality.TiltMeter}");
				break;

			case HandResult.Loss:
				Personality.ConsecutiveLosses++;
				AddTiltSafe(7f * Personality.ConsecutiveLosses);
				GameManager.LogVerbose($"{PlayerName} lost (streak: {Personality.ConsecutiveLosses}). Tilt: {Personality.TiltMeter}");
				break;

			case HandResult.AllInLoss:
				AddTiltSafe(26f);
				Personality.ConsecutiveLosses++;
				GameManager.LogVerbose($"{PlayerName} lost all-in! Tilt: {Personality.TiltMeter}");
				break;

			case HandResult.Win:
				Personality.ConsecutiveLosses = 0;

				float bigBlindsWon = (float)potSize / Math.Max(bigBlind, 1);
				float reliefAmount = 2.0f;

				if (bigBlindsWon > 20) reliefAmount = 12.0f;
				else if (bigBlindsWon > 10) reliefAmount = 8.0f;

				Personality.ReduceTilt(reliefAmount);
				GameManager.LogVerbose($"{PlayerName} won ({bigBlindsWon:F1} BB). Tilt reduced by {reliefAmount}");
				break;

			case HandResult.Neutral:
				Personality.ReduceTilt(3f);
				break;
		}

		if (Mathf.Abs(previousTilt - Personality.TiltMeter) > 0.5f)
		{
			EmitSignal(SignalName.TiltLevelChanged, Personality.TiltMeter);
			// VISUAL IMPROVEMENT: Log meaningful Tilt changes at the end of the hand
			GD.Print($"[RESULT] {PlayerName} Tilt {(Personality.TiltMeter > previousTilt ? "+" : "")}{(Personality.TiltMeter - previousTilt):F1} (Total: {Personality.TiltMeter:F1})");
		}
	}

	// Helper to prevent tilt from exploding past 100
	private void AddTiltSafe(float amount)
	{
		Personality.AddTilt(amount);
		if (Personality.TiltMeter > 100f)
			Personality.TiltMeter = 100f;
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

		IsAllIn = (ChipStack <= 0);
	}

	/// <summary>
	/// Returns a spoken dialogue line based on action + hand strength + bluffing.
	/// Uses Composure to decide whether we use Strong/Weak/Bluffing lines,
	/// and falls back to the action categories (OnFold/OnCheck/OnCall/OnRaise/OnAllIn).
	/// </summary>
	public string GetDialogueForAction(PlayerAction action, HandStrength strength, bool isBluffing)
	{
		GodotDict dialog = Personality.Dialogue;

		bool lacksComposure = GD.Randf() > Personality.Composure; 
		if (lacksComposure)
		{
			if (isBluffing && TryGetRandom(dialog, "Bluffing", out string bluffLine))
				return bluffLine;

			if (strength == HandStrength.Strong && TryGetRandom(dialog, "StrongHand", out string strongLine))
				return strongLine;

			if (strength == HandStrength.Weak && TryGetRandom(dialog, "WeakHand", out string weakLine))
				return weakLine;
		}

		string key = action switch
		{
			PlayerAction.Fold => "OnFold",
			PlayerAction.Check => "OnCheck",
			PlayerAction.Call => "OnCall",
			PlayerAction.Raise => "OnRaise",
			PlayerAction.AllIn => "OnAllIn",
			_ => null
		};

		if (key != null && TryGetRandom(dialog, key, out string actionLine))
			return actionLine;

		return "";
	}

	// Compatibility helper used by existing UI code (PokerGame.UI.cs)
	public string GetRandomDialogue(string category)
	{
		if (Personality == null) return "";
		if (Personality.Dialogue == null) return "";

		GodotDict dialog = Personality.Dialogue;
		if (!dialog.TryGetValue(category, out var lines) || lines.Count == 0)
			return "";

		int idx = (int)GD.RandRange(0, lines.Count - 1);
		return lines[idx].ToString();
	}

	private bool TryGetRandom(GodotDict dict, string key, out string line)
	{
		line = "";
		if (!dict.TryGetValue(key, out var lines) || lines.Count == 0)
			return false;

		int idx = (int)GD.RandRange(0, lines.Count - 1);
		line = lines[idx].ToString();
		return true;
	}

	public void SetDecisionMaker(PokerDecisionMaker dm) => decisionMaker = dm;

	public HandStrength DetermineHandStrengthCategory(GameState gameState)
	{
		float strength = EvaluateCurrentHandStrength(gameState);

		if (strength > 0.65f) return HandStrength.Strong;
		if (strength > 0.35f) return HandStrength.Medium;
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
			Hand,
			gameState.CommunityCards,
			gameState.Street,
			HandRandomnessSeed
		);
	}

	/// <summary>
	/// Deal cards to this player
	/// </summary>
	public void DealCard(Card card)
	{
		Hand.Add(card);
	}

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
			Composure = 0.5f
		};
	}

	// Helper property to get state enum
	public TiltState CurrentTiltState
	{
		get
		{
			float t = Personality.TiltMeter;
			if (t >= 40f) return TiltState.Monkey;
			if (t >= 20f) return TiltState.Steaming;
			if (t >= 10f) return TiltState.Annoyed;
			return TiltState.Zen;
		}
	}

	public string GetDebugInfo()
	{
		return $"{PlayerName} | Chips: {ChipStack} | Tilt: {Personality.TiltMeter:F1} | " +
			   $"Aggression: {Personality.CurrentAggression:F2} | Losses: {Personality.ConsecutiveLosses}";
	}
	
	#if TOOLS
		public void ForceBetSizeSeedForTesting(float seed)
		{
			this.BetSizeSeed = seed;
		}
	#endif
}
