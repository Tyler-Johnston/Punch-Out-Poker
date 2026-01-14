using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Represents an AI-controlled poker player with personality-driven behavior
/// Handles chip management, hand tracking, tilt processing, and tell display
/// </summary>
public partial class AIPokerPlayer : Node
{
	// Core properties
	[Export] public PokerPersonality Personality { get; set; }
	[Export] public string PlayerName { get; set; }
	[Export] public int StartingChips { get; set; } = 20000;
	
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
	
	public override void _Ready()
	{
		Hand = new List<Card>();
		ChipStack = StartingChips;
		IsFolded = false;
		IsAllIn = false;
		CurrentBetThisRound = 0;
		
		if (HasNode("PokerDecisionMaker"))
		{
			decisionMaker = GetNode<PokerDecisionMaker>("PokerDecisionMaker");
			GD.Print($"[{PlayerName}] Found PokerDecisionMaker child node");
		}
		else
		{
			GD.Print($"[{PlayerName}] No PokerDecisionMaker child node found - will be set externally");
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

	
	/// <summary>
	/// Main decision-making method called during betting rounds
	/// </summary>
	public PlayerAction MakeDecision(GameState gameState)
	{
		if (IsFolded || IsAllIn)
		{
			return PlayerAction.Check;
		}
		
		PlayerAction action = decisionMaker.DecideAction(this, gameState);
		
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
	
	/// <summary>
	/// Process the result of a completed hand and adjust tilt
	/// </summary>
	public void ProcessHandResult(HandResult result)
	{
		float previousTilt = Personality.TiltMeter;
		
		switch (result)
		{
			case HandResult.BadBeat:
				Personality.AddTilt(20f);
				Personality.ConsecutiveLosses++;
				GD.Print($"{PlayerName} suffered a bad beat! Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.BluffCaught:
				Personality.AddTilt(12f);
				GD.Print($"{PlayerName} caught a bluff and folded! Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.Loss:
				Personality.ConsecutiveLosses++;
				float lossAmount = 5f * Personality.ConsecutiveLosses;
				Personality.AddTilt(lossAmount);
				GD.Print($"{PlayerName} lost (streak: {Personality.ConsecutiveLosses}). Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.AllInLoss:
				Personality.AddTilt(25f);
				Personality.ConsecutiveLosses++;
				GD.Print($"{PlayerName} lost all-in! Tilt: {Personality.TiltMeter}");
				break;
				
			case HandResult.Win:
				Personality.ConsecutiveLosses = 0;
				Personality.ReduceTilt(3f);
				GD.Print($"{PlayerName} won! Tilt reduced to: {Personality.TiltMeter}");
				break;
				
			case HandResult.Neutral:
				Personality.ReduceTilt(2f);
				break;
		}
		
		// Emit signal if tilt changed significantly
		if (Mathf.Abs(previousTilt - Personality.TiltMeter) > 1f)
		{
			EmitSignal(SignalName.TiltLevelChanged, Personality.TiltMeter);
		}
	}
	
	/// <summary>
	/// Get an appropriate tell based on current hand strength
	/// Returns empty string if no tell should be shown (25% of the time)
	/// </summary>
	public string GetTellForHandStrength(HandStrength strength)
	{
		string tellCategory = strength switch
		{
			HandStrength.Strong => "strong_hand",
			HandStrength.Weak => "weak_hand",
			HandStrength.Bluffing => "bluffing",
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
	/// Set the decision maker externally (when not added as child node)
	/// </summary>
	public void SetDecisionMaker(PokerDecisionMaker dm)
	{
		decisionMaker = dm;
		GD.Print($"[{PlayerName}] DecisionMaker set externally");
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
	
	/// <summary>
	/// Evaluate current hand strength (delegates to decision maker)
	/// </summary>
	private float EvaluateCurrentHandStrength(GameState gameState)
	{
		if (gameState.Street == Street.Preflop)
		{
			return EvaluatePreflop();
		}
		
		int phevalRank = HandEvaluator.EvaluateHand(Hand, gameState.CommunityCards);
		float strength = 1.0f - ((phevalRank - 1) / 7461.0f);
		strength = (float)Math.Pow(strength, 0.75);
		
		if (phevalRank <= 6185) // Any pair or better
		{
			strength = Math.Max(strength, 0.38f);
		}
		
		return Mathf.Clamp(strength, 0.10f, 1.0f);
	}

	private float EvaluatePreflop()
	{
		if (Hand.Count != 2) return 0.2f;
		
		bool isPair = Hand[0].Rank == Hand[1].Rank;
		bool isSuited = Hand[0].Suit == Hand[1].Suit;
		int highCard = Mathf.Max((int)Hand[0].Rank, (int)Hand[1].Rank);
		
		if (isPair)
		{
			return 0.5f + (highCard / 28f);
		}
		
		float strength = 0.2f + (highCard / 40f);
		if (isSuited) strength += 0.1f;
		
		return Mathf.Clamp(strength, 0.1f, 1.0f);
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
	/// Reset player state for new hand
	/// </summary>
	public void ResetForNewHand()
	{
		Hand.Clear();
		IsFolded = false;
		CurrentBetThisRound = 0;
		
		if (ChipStack > 0)
		{
			IsAllIn = false;
		}
		
		// Gradual tilt decay between hands
		Personality.ReduceTilt(2f);
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
			CallTendency = 0.5f
		};
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
