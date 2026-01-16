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
	public const float TURN_VALUE_THRESHOLD = 0.58f;
	public const float RIVER_VALUE_THRESHOLD = 0.62f;
	
	// ✅ INCREASED: Bluff ceilings allow more bluff opportunities
	public const float FLOP_BLUFF_CEILING = 0.48f;   // Was 0.42f → 0.48f
	public const float TURN_BLUFF_CEILING = 0.45f;   // Was 0.38f → 0.45f
	public const float RIVER_BLUFF_CEILING = 0.42f;  // Was 0.35f → 0.42f
	
	// Size respect (how much bet size affects thresholds)
	public const float FLOP_SIZE_BUMP = 0.025f;
	public const float LATER_STREET_SIZE_BUMP = 0.045f;
	
	// Pot odds - only override for strong value spots
	public const float POT_ODDS_MULTIPLIER = 1.25f;
	public const float POT_ODDS_OVERRIDE_THRESHOLD = 0.40f;
	
	// Position adjustments
	public const float OOP_VALUE_TIGHTEN = 0.03f;
	public const float OOP_BLUFF_REDUCE = 0.03f;
	
	// Street-specific bet size multipliers
	public const float PREFLOP_BET_MULTIPLIER = 1.0f;
	public const float FLOP_BET_MULTIPLIER = 1.15f;
	public const float TURN_BET_MULTIPLIER = 1.25f;
	public const float RIVER_BET_MULTIPLIER = 1.20f;
	
	// Threshold adjustment factors
	public const float SIZE_FACTOR_VALUE_ADJUST = 0.03f;
	public const float SIZE_FACTOR_BLUFF_ADJUST = 0.04f;
	
	// ✅ INCREASED: Bluff probabilities to match stated personality
	public const float BLUFF_BASE_PROB = 0.55f;              // Was 0.40f → 0.55f
	public const float BLUFF_AGGRESSION_WEIGHT = 0.35f;      // Was 0.25f → 0.35f
	
	// ✅ REDUCED: Trap less often to extract more value
	public const float TRAP_PROBABILITY = 0.08f;             // Was 0.20f → 0.08f
	
	// ✅ ADJUSTED: Value bet frequencies for better balance
	public const float VALUE_BET_BASE_FREQ = 0.70f;          // Was 0.60f → 0.70f
	public const float VALUE_BET_AGGRESSION_WEIGHT = 0.20f;  // Was 0.30f → 0.20f
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
				GD.Print($"[AI] Small all-in ({betRatio:F2}x pot), using normal call logic");
				
				Decision callFoldDecision = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality);
				PlayerAction action = callFoldDecision == Decision.Fold ? PlayerAction.Fold : PlayerAction.AllIn;
				GD.Print($"[AI ACTION] {player.PlayerName} {action}");
				return action;
			}
			
			PlayerAction allInAction = DecideAllIn(handStrength, betRatio, gameState.Street, personality, player);
			GD.Print($"[AI ACTION] {player.PlayerName} {allInAction}");
			return allInAction;
		}

		// Standard facing-bet decision
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
		// Only use pot odds override for clear value spots
		float potOdds = toCall / (potSize + toCall);
		if (potOdds < PokerAIConfig.POT_ODDS_OVERRIDE_THRESHOLD && 
			handStrength > potOdds * PokerAIConfig.POT_ODDS_MULTIPLIER)
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
	// CENTRAL CHECK/BET LOGIC - ✅ CORRECTED & BALANCED
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
			valueThreshold += PokerAIConfig.OOP_VALUE_TIGHTEN;
			bluffCeiling -= PokerAIConfig.OOP_BLUFF_REDUCE;
		}

		// Adjust thresholds based on planned bet size
		float sizeFactor = Mathf.Clamp(plannedBetRatio, 0f, 2f);
		valueThreshold += PokerAIConfig.SIZE_FACTOR_VALUE_ADJUST * sizeFactor * (1f - effRiskTol);
		bluffCeiling -= PokerAIConfig.SIZE_FACTOR_BLUFF_ADJUST * sizeFactor;

		// ═══════════════════════════════════════════════════════
		// VALUE HANDS (>= valueThreshold)
		// ═══════════════════════════════════════════════════════
		if (handStrength >= valueThreshold)
		{
			// ✅ REDUCED: Trap 8% instead of 20%
			if (handStrength > 0.85f && player.TrapDecisionSeed < PokerAIConfig.TRAP_PROBABILITY)
			{
				GD.Print($"[AI] Trapping with {handStrength:F2} strength");
				return (Decision.Check, 0f);
			}
			
			// ✅ CORRECTED: Check 10-30% of medium-strong value hands
			if (handStrength < 0.75f)  // Not premium
			{
				// Bet 70-90% of the time (check 10-30%)
				float valueBetFreq = PokerAIConfig.VALUE_BET_BASE_FREQ + 
									 (effAggression * PokerAIConfig.VALUE_BET_AGGRESSION_WEIGHT);
				// Steve (0.45 agg): 0.70 + (0.45 * 0.20) = 0.79 = bet 79%, check 21%
				// Boy Wizard (0.85): 0.70 + (0.85 * 0.20) = 0.87 = bet 87%, check 13%
				
				float valueSeed = GetDecisionSeedForStreet(player, street);
				
				// ✅ CORRECTED: Check when seed EXCEEDS bet frequency
				if (valueSeed > valueBetFreq)
				{
					GD.Print($"[AI] Checking value hand ({handStrength:F2}) for deception");
					return (Decision.Check, 0f);
				}
			}
			
			// Bet value hands
			return (Decision.Bet, plannedBetRatio);
		}

		// ═══════════════════════════════════════════════════════
		// BLUFF RANGE (<= bluffCeiling)
		// ═══════════════════════════════════════════════════════
		if (handStrength <= bluffCeiling)
		{
			// ✅ INCREASED: More aggressive bluff formula
			float bluffProb = PokerAIConfig.BLUFF_BASE_PROB * effBluffFreq + 
							  PokerAIConfig.BLUFF_AGGRESSION_WEIGHT * effAggression;
			// Steve (0.33 bluff, 0.45 agg): 0.55 * 0.33 + 0.35 * 0.45 = 0.34 = 34%
			// Boy Wizard (0.70, 0.85): 0.55 * 0.70 + 0.35 * 0.85 = 0.68 = 68%
			
			float bluffSeed = GetDecisionSeedForStreet(player, street);
			
			if (bluffSeed < bluffProb)
			{
				GD.Print($"[AI] Bluffing on {street} (strength: {handStrength:F2}, size: {plannedBetRatio:F2}x)");
				return (Decision.Bet, plannedBetRatio);
			}
			return (Decision.Check, 0f);
		}

		// ═══════════════════════════════════════════════════════
		// MEDIUM HANDS (between bluffCeiling and valueThreshold)
		// ═══════════════════════════════════════════════════════
		float mediumBetFreq = street switch
		{
			Street.Flop => 0.25f + (0.40f * effAggression),    // ~45% at 0.50 agg, 59% at 0.85 agg
			Street.Turn => 0.20f + (0.45f * effAggression),    // ~43% at 0.50 agg, 58% at 0.85 agg
			Street.River => 0.15f + (0.35f * effAggression),   // ~33% at 0.50 agg, 45% at 0.85 agg
			_ => 0.20f + (0.35f * effAggression)
		};
		
		float mediumSeed = GetDecisionSeedForStreet(player, street);
		if (mediumSeed < mediumBetFreq)
		{
			GD.Print($"[AI] Betting medium hand ({handStrength:F2}) for protection/value");
			return (Decision.Bet, plannedBetRatio);
		}

		// Check back
		return (Decision.Check, 0f);
	}

	/// <summary>
	/// Get consistent decision seed for each street
	/// </summary>
	private float GetDecisionSeedForStreet(AIPokerPlayer player, Street street)
	{
		return street switch
		{
			Street.Preflop => player.PreflopDecisionSeed,
			Street.Flop => player.FlopDecisionSeed,
			Street.Turn => player.TurnDecisionSeed,
			Street.River => player.RiverDecisionSeed,
			_ => player.FlopDecisionSeed
		};
	}

	/// <summary>
	/// Calculate planned bet ratio with increased sizing + street multipliers
	/// </summary>
	private float CalculatePlannedBetRatio(float handStrength, PokerPersonality personality, Street street, float betSizeSeed)
	{
		// BetSizeSeed is already 0-1 from GD.Randf()
		float normalizedSeed = betSizeSeed;
		
		float baseBetMultiplier;
		
		// Base bet ranges by hand strength
		if (handStrength >= 0.80f) // Premium hands
		{
			baseBetMultiplier = 0.85f + (normalizedSeed * 0.35f); // 0.85-1.20x pot
		}
		else if (handStrength >= 0.65f) // Strong hands
		{
			baseBetMultiplier = 0.65f + (normalizedSeed * 0.30f); // 0.65-0.95x pot
		}
		else if (handStrength >= 0.45f) // Medium hands
		{
			baseBetMultiplier = 0.50f + (normalizedSeed * 0.25f); // 0.50-0.75x pot
		}
		else if (handStrength >= 0.35f) // Weak hands
		{
			baseBetMultiplier = 0.35f + (normalizedSeed * 0.25f); // 0.35-0.60x pot
		}
		else // Polarized bluff sizing (capped)
		{
			if (normalizedSeed < 0.60f)
			{
				// Small bluff (60% of the time)
				float smallBluffSeed = normalizedSeed / 0.60f;
				baseBetMultiplier = 0.35f + (smallBluffSeed * 0.20f); // 0.35-0.55x
			}
			else
			{
				// Big bluff (40% of the time) - capped to prevent exploitation
				float bigBluffSeed = (normalizedSeed - 0.60f) / 0.40f;
				baseBetMultiplier = 1.20f + (bigBluffSeed * 0.40f); // 1.20-1.60x
			}
		}
		
		// Apply street-specific multipliers
		float streetMultiplier = street switch
		{
			Street.Preflop => PokerAIConfig.PREFLOP_BET_MULTIPLIER,
			Street.Flop => PokerAIConfig.FLOP_BET_MULTIPLIER,
			Street.Turn => PokerAIConfig.TURN_BET_MULTIPLIER,
			Street.River => PokerAIConfig.RIVER_BET_MULTIPLIER,
			_ => 1.0f
		};
		
		baseBetMultiplier *= streetMultiplier;
		
		// Apply aggression multiplier
		float aggressionMultiplier = 0.8f + (personality.CurrentAggression * 0.5f);
		
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

		// Use seeded bluff shove decision
		float bluffShoveProb = street switch
		{
			Street.Preflop => effBluffFreq * 0.25f,
			Street.Flop => effBluffFreq * 0.20f,
			_ => effBluffFreq * 0.10f
		};
		
		float bluffSeed = GetDecisionSeedForStreet(player, street);
		if (handStrength < 0.25f && bluffSeed < bluffShoveProb)
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

		// Strong hand - consider raising
		if (handStrength >= raiseThreshold && player.ChipStack > toCall * 2.5f)
		{
			float raiseProb = effAggression * 0.8f + 0.2f;
			float raiseSeed = GetDecisionSeedForStreet(player, street);
			
			if (raiseSeed < raiseProb)
			{
				GD.Print($"[AI] Value raising ({handStrength:F2})");
				return PlayerAction.Raise;
			}
		}

		// Bluff raise
		float bluffRaiseProb = effBluffFreq * (street == Street.Flop ? 0.35f : 0.20f);
		float bluffRaiseSeed = GetDecisionSeedForStreet(player, street);
		
		if (handStrength < 0.32f && bluffRaiseSeed < bluffRaiseProb && player.ChipStack > toCall * 3f)
		{
			GD.Print($"[AI] Bluff raising on {street}");
			return PlayerAction.Raise;
		}

		return PlayerAction.Call;
	}

	// ══════════════════════════════════════════════════════════════
	// BET SIZE CALCULATION
	// ══════════════════════════════════════════════════════════════
	
	public int CalculateBetSize(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		var personality = player.Personality;
		float potSize = gameState.PotSize;
		float currentBet = gameState.CurrentBet;
		float toCall = currentBet - gameState.GetPlayerCurrentBet(player);
		
		float baseBetRatio = CalculatePlannedBetRatio(handStrength, personality, gameState.Street, player.BetSizeSeed);
		float betSize = potSize * baseBetRatio;
		
		// Tilt adjustments
		if (personality.TiltMeter > 20f)
		{
			betSize *= 1.15f;
			GD.Print($"[{player.PlayerName}] Tilted betting ({personality.TiltMeter:F0} tilt)");
		}
		
		// Proper minimum raise calculation
		float minRaise;
		if (toCall > 0)
		{
			float lastRaiseSize = Mathf.Max(currentBet - gameState.PreviousBet, gameState.BigBlind);
			minRaise = currentBet + lastRaiseSize;
		}
		else
		{
			minRaise = gameState.BigBlind;
		}
		
		betSize = Mathf.Max(betSize, minRaise);
		
		// River stack commitment with strong hands
		if (gameState.Street == Street.River && handStrength >= 0.60f)
		{
			float stackToPotRatio = player.ChipStack / potSize;
			
			if (stackToPotRatio < 1.0f && betSize >= player.ChipStack * 0.60f)
			{
				if (handStrength >= 0.70f || player.AllInCommitmentSeed < 0.70f)
				{
					GD.Print($"[{player.PlayerName}] River stack commitment! ({player.ChipStack} chips, {handStrength:F2} strength)");
					return player.ChipStack;
				}
			}
		}
		
		// Check for all-in situations
		if (betSize >= player.ChipStack * 0.9f)
		{
			if (player.AllInCommitmentSeed < personality.CurrentRiskTolerance || handStrength > 0.80f)
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
	// HAND STRENGTH EVALUATION
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
		
		// Use seeded randomness (consistent per hand)
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
			// Non-linear scaling (AA = 0.85, not 0.75)
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
	public float PreviousBet { get; set; }
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
		PreviousBet = CurrentBet;
		playerBets.Clear();
	}
}
