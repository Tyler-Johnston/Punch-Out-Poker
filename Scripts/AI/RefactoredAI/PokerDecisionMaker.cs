using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ══════════════════════════════════════════════════════════════
// CONFIGURATION (Extract magic numbers for easy tuning)
// ══════════════════════════════════════════════════════════════

public static class PokerAIConfig
{
	// Call thresholds by street
	public const float FLOP_BASE_THRESHOLD = 0.38f;
	public const float TURN_BASE_THRESHOLD = 0.48f;
	public const float RIVER_BASE_THRESHOLD = 0.54f;
	public const float PREFLOP_BASE_THRESHOLD = 0.42f;
	
	// Value bet thresholds by street
	public const float FLOP_VALUE_THRESHOLD = 0.55f;
	public const float TURN_VALUE_THRESHOLD = 0.60f;
	public const float RIVER_VALUE_THRESHOLD = 0.65f;
	
	// Bluff ceilings by street
	public const float FLOP_BLUFF_CEILING = 0.32f;
	public const float TURN_BLUFF_CEILING = 0.30f;
	public const float RIVER_BLUFF_CEILING = 0.28f;
	
	// Size respect (how much bet size affects thresholds)
	public const float FLOP_SIZE_BUMP = 0.025f;
	public const float LATER_STREET_SIZE_BUMP = 0.045f;
	
	// Pot odds safety margin
	public const float POT_ODDS_MULTIPLIER = 1.3f;
	
	// Position adjustments
	public const float OOP_VALUE_TIGHTEN = 0.03f;
	public const float OOP_BLUFF_REDUCE = 0.03f;
}

public partial class PokerDecisionMaker : Node
{
	// ══════════════════════════════════════════════════════════════
	// MAIN ENTRY POINT
	// ══════════════════════════════════════════════════════════════
	
	public PlayerAction DecideAction(AIPokerPlayer player, GameState gameState)
	{
		float handStrength = EvaluateHandStrength(player.Hand, gameState.CommunityCards, gameState.Street, player.HandRandomnessSeed);
		var personality = player.Personality;
		
		float toCall = gameState.CurrentBet - gameState.GetPlayerCurrentBet(player);
		float potSize = Mathf.Max(gameState.PotSize, 1f);
		float betRatio = toCall / potSize;

		GD.Print($"[AI STATE] {player.PlayerName} | Street: {gameState.Street} | Strength: {handStrength:F2} | " +
				 $"ToCall: {toCall} | BetRatio: {betRatio:F2} | Tilt: {personality.TiltMeter:F0} | Pos: {(gameState.IsAIInPosition ? "IP" : "OOP")}");

		// No bet to face - check or bet decision
		if (toCall <= 0)
		{
			(Decision decision, float plannedBetRatio) = DecideCheckOrBet(handStrength, gameState, personality, player);
			PlayerAction action = decision == Decision.Bet ? PlayerAction.Raise : PlayerAction.Check;
			GD.Print($"[AI ACTION] {player.PlayerName} {action}");
			return action;
		}

		// All-in pressure (can't just call)
		if (toCall >= player.ChipStack)
		{
			// If all-in is small relative to pot, treat as normal call decision
			if (betRatio < 0.50f)
			{
				GD.Print($"[AI] Small all-in ({betRatio:F2}x pot), using normal call logic with pot odds");
				float potOdds = toCall / (potSize + toCall);
				
				// Excellent pot odds - auto-call if equity exceeds odds
				if (handStrength > potOdds * PokerAIConfig.POT_ODDS_MULTIPLIER)
				{
					GD.Print($"[AI ACTION] {player.PlayerName} AllIn (pot odds: {potOdds:F2}, equity: {handStrength:F2})");
					return PlayerAction.AllIn;
				}
				
				Decision callFoldDecision = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality);
				PlayerAction action = callFoldDecision == Decision.Fold ? PlayerAction.Fold : PlayerAction.AllIn;
				GD.Print($"[AI ACTION] {player.PlayerName} {action}");
				return action;
			}
			
			PlayerAction allInAction = DecideAllIn(handStrength, betRatio, gameState.Street, personality, player);
			GD.Print($"[AI ACTION] {player.PlayerName} {allInAction}");
			return allInAction;
		}

		// Standard facing-bet decision (pot odds check integrated)
		Decision callFoldDecision2 = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality);
		
		if (callFoldDecision2 == Decision.Fold)
		{
			GD.Print($"[AI ACTION] {player.PlayerName} Fold");
			return PlayerAction.Fold;
		}

		// We're continuing - now decide call vs raise
		PlayerAction finalAction = DecideCallOrRaise(handStrength, betRatio, gameState.Street, personality, player, toCall);
		GD.Print($"[AI ACTION] {player.PlayerName} {finalAction}");
		return finalAction;
	}

	// ══════════════════════════════════════════════════════════════
	// CENTRAL CALL/FOLD LOGIC (with pot odds check)
	// ══════════════════════════════════════════════════════════════
	
	private Decision DecideCallOrFold(
		float handStrength,
		float betRatio,
		float potSize,
		float toCall,
		Street street,
		PokerPersonality personality)
	{
		float potOdds = toCall / (potSize + toCall);
		if (handStrength > potOdds * PokerAIConfig.POT_ODDS_MULTIPLIER)
		{
			GD.Print($"[AI] Easy call - pot odds: {potOdds:F2}, equity: {handStrength:F2}");
			return Decision.Call;
		}
		
		// 1) Effective stats under tilt
		float tiltFactor = 1f + (personality.TiltMeter / 100f);
		float effCallTend = Mathf.Clamp(personality.CallTendency * (1f + personality.TiltMeter / 200f), 0f, 1f);
		float effRiskTol = Mathf.Clamp(personality.CurrentRiskTolerance * tiltFactor, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency * tiltFactor, 0f, 1f);

		// 2) Base call thresholds per street
		float baseThreshold = street switch
		{
			Street.Flop => PokerAIConfig.FLOP_BASE_THRESHOLD,
			Street.Turn => PokerAIConfig.TURN_BASE_THRESHOLD,
			Street.River => PokerAIConfig.RIVER_BASE_THRESHOLD,
			_ => PokerAIConfig.PREFLOP_BASE_THRESHOLD
		};

		// 3) Personality shift
		float callShift = Mathf.Lerp(0.06f, -0.06f, effCallTend);
		baseThreshold += callShift;

		// 4) Bet size scaling
		float sizeFactor = Mathf.Clamp(betRatio, 0f, 3f);
		float sizeBump = street == Street.Flop 
			? PokerAIConfig.FLOP_SIZE_BUMP * sizeFactor
			: PokerAIConfig.LATER_STREET_SIZE_BUMP * sizeFactor;
		
		sizeBump *= (1f - 0.5f * effRiskTol);
		float threshold = Mathf.Clamp(baseThreshold + sizeBump, 0f, 1f);

		// 5) HUGE OVERBET RULE (2.5x+ pot)
		if (betRatio >= 2.5f)
		{
			float overbetTighten = Mathf.Lerp(0.08f, 0.18f, 1f - effRiskTol);
			float overbetThreshold = Mathf.Clamp(0.58f + overbetTighten, 0f, 1f);
			float bluffDiscount = 0.04f * effBluffFreq;
			overbetThreshold -= bluffDiscount;

			if (handStrength < overbetThreshold)
			{
				GD.Print($"[AI] Folding to huge overbet ({betRatio:F1}x pot) - need {overbetThreshold:F2}, have {handStrength:F2}");
				return Decision.Fold;
			}
		}

		// 6) Large bet (0.8x+ pot)
		if (betRatio > 0.8f)
		{
			float bluffAdjust = Mathf.Lerp(0.03f, -0.03f, effBluffFreq);
			float callThreshold = Mathf.Clamp(threshold + bluffAdjust, 0f, 1f);

			if (handStrength < callThreshold)
			{
				GD.Print($"[AI] Folding to large bet ({betRatio:F1}x pot) on {street} - need {callThreshold:F2}, have {handStrength:F2}");
				return Decision.Fold;
			}
		}
		else
		{
			// Small/medium bets - tiered defense
			float lightThreshold;
			
			if (betRatio < 0.33f)
			{
				lightThreshold = threshold - 0.10f;
				if (street == Street.Flop) lightThreshold -= 0.05f;
			}
			else if (betRatio < 0.55f)
			{
				lightThreshold = threshold - 0.06f;
				if (street == Street.Flop) lightThreshold -= 0.04f;
			}
			else
			{
				lightThreshold = threshold - 0.03f;
				if (street == Street.Flop) lightThreshold -= 0.02f;
			}
			
			lightThreshold = Mathf.Clamp(lightThreshold, 0.15f, 1f);

			if (handStrength < lightThreshold)
			{
				GD.Print($"[AI] Folding to small bet ({betRatio:F1}x pot) - need {lightThreshold:F2}, have {handStrength:F2}");
				return Decision.Fold;
			}
		}

		return Decision.Call;
	}

	// ══════════════════════════════════════════════════════════════
	// CENTRAL CHECK/BET LOGIC (now calculates actual bet size)
	// ══════════════════════════════════════════════════════════════
	
	private (Decision decision, float plannedBetRatio) DecideCheckOrBet(
		float handStrength,
		GameState gameState,
		PokerPersonality personality,
		AIPokerPlayer player)
	{
		Street street = gameState.Street;
		float tiltFactor = 1f + (personality.TiltMeter / 100f);
		float effAggression = Mathf.Clamp(personality.CurrentAggression * tiltFactor, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency * tiltFactor, 0f, 1f);
		float effRiskTol = Mathf.Clamp(personality.CurrentRiskTolerance * tiltFactor, 0f, 1f);

		// ✅ Calculate actual planned bet size for this hand strength
		float plannedBetRatio = CalculatePlannedBetRatio(handStrength, personality, street, player.BetSizeSeed);

		// Value and bluff thresholds by street
		float valueThreshold, bluffCeiling;
		switch (street)
		{
			case Street.Flop:
				valueThreshold = PokerAIConfig.FLOP_VALUE_THRESHOLD;
				bluffCeiling = PokerAIConfig.FLOP_BLUFF_CEILING;
				break;
			case Street.Turn:
				valueThreshold = PokerAIConfig.TURN_VALUE_THRESHOLD;
				bluffCeiling = PokerAIConfig.TURN_BLUFF_CEILING;
				break;
			case Street.River:
				valueThreshold = PokerAIConfig.RIVER_VALUE_THRESHOLD;
				bluffCeiling = PokerAIConfig.RIVER_BLUFF_CEILING;
				break;
			default:
				valueThreshold = PokerAIConfig.FLOP_VALUE_THRESHOLD;
				bluffCeiling = PokerAIConfig.FLOP_BLUFF_CEILING;
				break;
		}

		if (!gameState.IsAIInPosition)
		{
			//valueThreshold += PokerAIConfig.OOP_VALUE_TIGHTEN;
			bluffCeiling -= PokerAIConfig.OOP_BLUFF_REDUCE;
		}

		// Adjust thresholds based on ACTUAL planned bet size
		float sizeFactor = Mathf.Clamp(plannedBetRatio, 0f, 2f);
		valueThreshold += 0.03f * sizeFactor * (1f - effRiskTol);
		bluffCeiling -= 0.04f * sizeFactor;

		// VALUE BET
		if (handStrength >= valueThreshold)
		{
			// ✅ Removed double-trap: only trap here, not in DecideCallOrRaise
			if (handStrength > 0.85f && GD.Randf() < 0.20f)
			{
				GD.Print($"[AI] Trapping with {handStrength:F2} strength");
				return (Decision.Check, 0f);
			}
			
			if (GD.Randf() < effAggression)
				return (Decision.Bet, plannedBetRatio);
			
			return (Decision.Bet, plannedBetRatio);
		}

		// BLUFF (only with very weak hands)
		if (handStrength <= bluffCeiling)
		{
			float bluffProb = 0.4f * effBluffFreq + 0.25f * effAggression;
			if (GD.Randf() < bluffProb)
			{
				GD.Print($"[AI] Bluffing on {street} (strength: {handStrength:F2}, size: {plannedBetRatio:F2}x)");
				return (Decision.Bet, plannedBetRatio);
			}
			return (Decision.Check, 0f);
		}

		// ✅ MEDIUM HANDS - more aggressive, street-dependent
		float mediumBetFreq = street switch
		{
			Street.Flop => 0.50f * effAggression,
			Street.Turn => 0.45f * effAggression,
			Street.River => 0.40f * effAggression,
			_ => 0.40f* effAggression
		};
		
		if (GD.Randf() < mediumBetFreq)
		{
			GD.Print($"[AI] Betting medium hand ({handStrength:F2}) for protection/value");
			return (Decision.Bet, plannedBetRatio);
		}

		return (Decision.Check, 0f);
	}

	/// <summary>
	/// Calculate planned bet ratio based on hand strength using consistent seeded randomness
	/// </summary>
	private float CalculatePlannedBetRatio(float handStrength, PokerPersonality personality, Street street, float betSizeSeed)
	{
		// Normalize seed to 0-1 range if it's not already
		float normalizedSeed = (betSizeSeed + 1f) / 2f; // Convert from [-1,1] to [0,1] if needed
		if (betSizeSeed >= 0f && betSizeSeed <= 1f)
			normalizedSeed = betSizeSeed; // Already normalized
		
		float baseBetMultiplier;
		
		if (handStrength >= 0.80f) // Premium hands
		{
			baseBetMultiplier = 0.70f + (normalizedSeed * 0.30f); // 0.70-1.00x pot
		}
		else if (handStrength >= 0.65f) // Strong hands
		{
			baseBetMultiplier = 0.55f + (normalizedSeed * 0.25f); // 0.55-0.80x pot
		}
		else if (handStrength >= 0.45f) // Medium hands
		{
			baseBetMultiplier = 0.40f + (normalizedSeed * 0.20f); // 0.40-0.60x pot
		}
		else if (handStrength >= 0.35f) // Weak hands
		{
			baseBetMultiplier = 0.25f + (normalizedSeed * 0.20f); // 0.25-0.45x pot
		}
		else // ✅ POLARIZED BLUFF SIZING (< 0.35)
		{
			// Use seed to decide small vs big bluff (60/40 split)
			if (normalizedSeed < 0.60f)
			{
				// Small bluff: map first 60% of seed range to 0.25-0.40x
				float smallBluffSeed = normalizedSeed / 0.60f; // Rescale to [0,1]
				baseBetMultiplier = 0.25f + (smallBluffSeed * 0.15f);
			}
			else
			{
				// Big bluff: map last 40% of seed range to 1.20-1.70x
				float bigBluffSeed = (normalizedSeed - 0.60f) / 0.40f; // Rescale to [0,1]
				baseBetMultiplier = 1.20f + (bigBluffSeed * 0.50f);
			}
		}
		
		float aggressionMultiplier = 0.7f + (personality.CurrentAggression * 0.6f);
		return baseBetMultiplier * aggressionMultiplier;
	}


	// ══════════════════════════════════════════════════════════════
	// ALL-IN DECISION LOGIC
	// ══════════════════════════════════════════════════════════════
	
	private PlayerAction DecideAllIn(
		float handStrength, 
		float betRatio, 
		Street street, 
		PokerPersonality personality, 
		AIPokerPlayer player)
	{
		float tiltFactor = 1f + (personality.TiltMeter / 100f);
		float effRiskTol = Mathf.Clamp(personality.CurrentRiskTolerance * tiltFactor, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency * tiltFactor, 0f, 1f);

		float allInThreshold = street switch
		{
			Street.Preflop => 0.52f - (effRiskTol * 0.18f),
			Street.Flop => 0.62f - (effRiskTol * 0.20f),
			Street.Turn => 0.68f - (effRiskTol * 0.20f),
			Street.River => 0.72f - (effRiskTol * 0.20f),
			_ => 0.62f - (effRiskTol * 0.20f)
		};

		if (handStrength >= allInThreshold)
		{
			GD.Print($"[AI] Calling all-in with {handStrength:F2} on {street} (threshold: {allInThreshold:F2})");
			return PlayerAction.AllIn;
		}

		// Occasional bluff shove
		float bluffShoveProb = street switch
		{
			Street.Preflop => effBluffFreq * 0.25f,
			Street.Flop => effBluffFreq * 0.20f,
			_ => effBluffFreq * 0.10f
		};
		
		if (handStrength < 0.25f && GD.Randf() < bluffShoveProb)
		{
			GD.Print($"[AI] Bluff shoving on {street}!");
			return PlayerAction.AllIn;
		}

		GD.Print($"[AI] Folding to all-in pressure on {street} ({handStrength:F2} < {allInThreshold:F2})");
		return PlayerAction.Fold;
	}

	// ══════════════════════════════════════════════════════════════
	// CALL VS RAISE DECISION LOGIC
	// ══════════════════════════════════════════════════════════════
	
	private PlayerAction DecideCallOrRaise(
		float handStrength, 
		float betRatio, 
		Street street,
		PokerPersonality personality, 
		AIPokerPlayer player, 
		float toCall)
	{
		float tiltFactor = 1f + (personality.TiltMeter / 100f);
		float effAggression = Mathf.Clamp(personality.CurrentAggression * tiltFactor, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency * tiltFactor, 0f, 1f);

		float raiseThreshold = street switch
		{
			Street.Flop => 0.62f,
			Street.Turn => 0.67f,
			Street.River => 0.72f,
			_ => 0.60f
		};
		raiseThreshold -= effAggression * 0.15f;

		// Strong hand - consider raising (no trap here, already handled in DecideCheckOrBet)
		if (handStrength >= raiseThreshold && player.ChipStack > toCall * 2.5f)
		{
			if (GD.Randf() < effAggression * 0.8f + 0.2f)
			{
				GD.Print($"[AI] Value raising ({handStrength:F2})");
				return PlayerAction.Raise;
			}
		}

		// Bluff raise
		float bluffRaiseProb = effBluffFreq * (street == Street.Flop ? 0.35f : 0.20f);
		if (handStrength < 0.32f && GD.Randf() < bluffRaiseProb && player.ChipStack > toCall * 3f)
		{
			GD.Print($"[AI] Bluff raising on {street}");
			return PlayerAction.Raise;
		}

		return PlayerAction.Call;
	}

	// ══════════════════════════════════════════════════════════════
	// BET SIZE CALCULATION (now uses actual strength-based sizing)
	// ══════════════════════════════════════════════════════════════
	
	public int CalculateBetSize(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		var personality = player.Personality;
		float potSize = gameState.PotSize;
		float currentBet = gameState.CurrentBet;
		float toCall = currentBet - gameState.GetPlayerCurrentBet(player);
		
		// ✅ Use the same seeded calculation for consistency
		float baseBetRatio = CalculatePlannedBetRatio(handStrength, personality, gameState.Street, player.BetSizeSeed);
		
		// Convert ratio to actual bet size
		float betSize = potSize * baseBetRatio;
		
		// Tilt adjustments
		if (personality.TiltMeter > 20f)
		{
			betSize *= 1.15f;
			GD.Print($"[{player.PlayerName}] Tilted betting ({personality.TiltMeter:F0} tilt)");
		}
		
		// Street-based adjustments
		if (gameState.Street == Street.River && handStrength > 0.70f)
		{
			betSize *= 1.2f;
			GD.Print($"[{player.PlayerName}] River value bet");
		}
		
		// Ensure minimum raise is valid
		float minRaise = toCall > 0 ? (currentBet - gameState.GetPlayerCurrentBet(player)) + currentBet : gameState.BigBlind;
		betSize = Mathf.Max(betSize, minRaise);
		
		// Check for all-in situations
		if (betSize >= player.ChipStack * 0.9f)
		{
			if (GD.Randf() < personality.CurrentRiskTolerance || handStrength > 0.80f)
			{
				GD.Print($"[{player.PlayerName}] Going all-in! ({player.ChipStack} chips)");
				return player.ChipStack;
			}
			else
			{
				betSize = player.ChipStack * 0.6f;
			}
		}
		
		int finalBet = (int)Mathf.Min(betSize, player.ChipStack);
		finalBet = Mathf.Max(1, finalBet);
		
		GD.Print($"[{player.PlayerName}] Bet size: {finalBet} (pot: {potSize}, strength: {handStrength:F2})");
		
		return finalBet;
	}


	// ══════════════════════════════════════════════════════════════
	// HAND STRENGTH EVALUATION (✅ now uses seeded randomness)
	// ══════════════════════════════════════════════════════════════
	
	private float EvaluateHandStrength(List<Card> holeCards, List<Card> communityCards, Street street, float randomnessSeed)
	{
		if (holeCards == null || holeCards.Count != 2)
		{
			GD.PrintErr("Invalid hole cards in EvaluateHandStrength");
			return 0.2f;
		}
		
		if (street == Street.Preflop || communityCards == null || communityCards.Count == 0)
		{
			return EvaluatePreflopHand(holeCards);
		}
		
		int phevalRank = HandEvaluator.EvaluateHand(holeCards, communityCards);
		float strength = 1.0f - ((phevalRank - 1) / 7461.0f);
		strength = (float)Math.Pow(strength, 0.75);
		
		if (phevalRank <= 6185)
		{
			strength = Math.Max(strength, 0.38f);
		}
		
		if (street == Street.Flop || street == Street.Turn)
		{
			List<Card> allCards = new List<Card>(holeCards);
			allCards.AddRange(communityCards);
			strength += EvaluateDrawPotential(allCards) * 0.10f;
		}
		
		// ✅ Use seeded randomness (consistent per hand)
		float randomness = randomnessSeed * 0.08f;
		
		return Mathf.Clamp(strength + randomness, 0.10f, 1.0f);
	}
	
	private float EvaluatePreflopHand(List<Card> holeCards)
	{
		if (holeCards.Count != 2) return 0.2f;
		
		Card card1 = holeCards[0];
		Card card2 = holeCards[1];
		
		bool isPair = card1.Rank == card2.Rank;
		bool isSuited = card1.Suit == card2.Suit;
		int rankDiff = Mathf.Abs((int)card1.Rank - (int)card2.Rank);
		int highCard = Mathf.Max((int)card1.Rank, (int)card2.Rank);
		
		float strength = 0.2f;
		
		if (isPair)
		{
			// ✅ FIXED: Non-linear scaling (AA = 0.85, not 0.75)
			strength = 0.50f + ((float)Math.Pow(highCard / 14f, 1.3) * 0.35f);
		}
		else
		{
			strength += (highCard / 40f);
			if (isSuited) strength += 0.1f;
			if (rankDiff <= 2) strength += 0.05f;
			if (highCard >= 12 && rankDiff <= 2) strength += 0.15f;
		}
		
		return Mathf.Clamp(strength, 0.1f, 1.0f);
	}
	
	private float EvaluateDrawPotential(List<Card> cards)
	{
		var suitCounts = cards.GroupBy(c => c.Suit).Select(g => g.Count());
		if (suitCounts.Any(count => count == 4)) return 0.35f;
		
		var ranks = cards.Select(c => (int)c.Rank).OrderBy(r => r).Distinct().ToList();
		for (int i = 0; i < ranks.Count - 3; i++)
		{
			if (ranks[i + 3] - ranks[i] == 3) return 0.30f;
		}
		
		return 0f;
	}
}

// ══════════════════════════════════════════════════════════════
// GAME STATE
// ══════════════════════════════════════════════════════════════

public partial class GameState : RefCounted
{
	public List<Card> CommunityCards { get; set; } = new List<Card>();
	public float PotSize { get; set; }
	public float CurrentBet { get; set; }
	public Street Street { get; set; }
	public float BigBlind { get; set; }
	public int OpponentChipStack { get; set; }
	public bool IsAIInPosition { get; set; }
	
	private Dictionary<AIPokerPlayer, float> playerBets = new Dictionary<AIPokerPlayer, float>();
	
	public float GetPlayerCurrentBet(AIPokerPlayer player)
	{
		return playerBets.ContainsKey(player) ? playerBets[player] : 0f;
	}
	
	public void SetPlayerBet(AIPokerPlayer player, float amount)
	{
		playerBets[player] = amount;
	}
	
	public void ResetBetsForNewStreet()
	{
		playerBets.Clear();
	}
}
