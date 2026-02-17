using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Central AI decision logic.
/// Returns only the action type; the game/executor is responsible for sizing raises via CalculateRaiseToTotal.
/// </summary>
public partial class PokerDecisionMaker : Node
{
	public PlayerAction DecideAction(AIPokerPlayer player, GameState gameState)
	{
		float handStrength = EvaluateHandStrength(
			player.Hand,
			gameState.CommunityCards,
			gameState.Street,
			player.HandRandomnessSeed
		);

		PokerPersonality personality = player.Personality;

		float toCall = gameState.CurrentBet - gameState.GetPlayerCurrentBet(player);
		float potSize = Mathf.Max(gameState.PotSize, 1f);
		float betRatio = toCall / potSize;

		GameManager.LogVerbose(
			$"[AI STATE] {player.PlayerName} | Street: {gameState.Street} | Strength: {handStrength:F2} | " +
			$"ToCall: {toCall} | BetRatio: {betRatio:F2} | Tilt: {player.CurrentTiltState} ({personality.TiltMeter:F0}) | " +
			$"Pos: {(gameState.IsAIInPosition ? "IP" : "OOP")}"
		);

		// No bet to face - check or bet decision.
		if (toCall <= 0)
		{
			Decision decision = DecideCheckOrBet(handStrength, gameState, personality, player, out float plannedBetRatio);
			PlayerAction action = (decision == Decision.Bet) ? PlayerAction.Raise : PlayerAction.Check;
			return action;
		}

		// All-in pressure (cannot call more than stack).
		if (toCall >= player.ChipStack)
		{
			// If all-in is small relative to pot, treat as normal call/fold decision.
			if (betRatio < 0.50f)
			{
				GameManager.LogVerbose($"[AI] Small all-in ({betRatio:F2}x pot), using normal call logic");

				Decision callFold = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality, player);
				PlayerAction action = (callFold == Decision.Fold) ? PlayerAction.Fold : PlayerAction.AllIn;
				return action;
			}

			PlayerAction allInAction = DecideAllIn(handStrength, betRatio, gameState.Street, personality, player, gameState);
			return allInAction;
		}

		// Standard facing-bet decision.
		Decision callFoldDecision = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality, player);
		if (callFoldDecision == Decision.Fold)
		{
			return PlayerAction.Fold;
		}
		
		if (!gameState.CanAIReopenBetting)
		{
			GameManager.LogVerbose($"[AI] Cannot raise - betting not reopened (under-raise all-in rule). Forcing Call.");
			return PlayerAction.Call;
		}


		// We're continuing - now decide call vs raise.
		PlayerAction finalAction = DecideCallOrRaise(handStrength, betRatio, gameState.Street, personality, player, toCall);
		return finalAction;
	}

	private Decision DecideCallOrFold(
		float handStrength,
		float betRatio,
		float potSize,
		float toCall,
		Street street,
		PokerPersonality personality,
		AIPokerPlayer player)
	{
		// 1) Pot odds override.
		float potOdds = toCall / (potSize + toCall);
		float oddsMultiplier = (betRatio < 0.40f) ? 1.0f : PokerAIConfig.POT_ODDS_MULTIPLIER;

		if (potOdds < PokerAIConfig.POT_ODDS_OVERRIDE_THRESHOLD &&
			handStrength > potOdds * oddsMultiplier)
		{
			GameManager.LogVerbose($"[AI] Easy call - pot odds: {potOdds:F2}, equity: {handStrength:F2} (Sticky Mode: {betRatio < 0.40f})");
			return Decision.Call;
		}

		// 2) Effective stats.
		float effCallTend = Mathf.Clamp(
			personality.CallTendency * (1f + personality.TiltMeter / 200f),
			0f,
			1f
		);
		float effRiskTol = Mathf.Clamp(personality.CurrentRiskTolerance, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);

		// 3) Base call thresholds per street.
		float baseThreshold = street switch
		{
			Street.Flop => PokerAIConfig.FLOP_BASE_THRESHOLD,
			Street.Turn => PokerAIConfig.TURN_BASE_THRESHOLD,
			Street.River => PokerAIConfig.RIVER_BASE_THRESHOLD,
			_ => PokerAIConfig.PREFLOP_BASE_THRESHOLD
		};

		// 4) Personality shift: higher call tendency => looser defense.
		float callShift = Mathf.Lerp(0.06f, -0.06f, effCallTend);
		baseThreshold += callShift;

		// 5) Bet size scaling.
		float sizeFactor = Mathf.Clamp(betRatio, 0f, 3f);
		float sizeBump = (street == Street.Flop)
			? PokerAIConfig.FLOP_SIZE_BUMP * sizeFactor
			: PokerAIConfig.LATER_STREET_SIZE_BUMP * sizeFactor;

		// Higher risk tolerance => less tightening vs big bets.
		sizeBump *= (1f - 0.5f * effRiskTol);
		float threshold = Mathf.Clamp(baseThreshold + sizeBump, 0f, 1f);

		// 6) Huge overbet rule (2.5x+ pot).
		if (betRatio >= 2.5f)
		{
			if (player.CurrentTiltState == TiltState.Monkey)
				return Decision.Call;

			float overbetTighten = Mathf.Lerp(0.08f, 0.18f, 1f - effRiskTol);
			float overbetThreshold = Mathf.Clamp(0.58f + overbetTighten, 0f, 1f);

			// If we think villain bluffs a lot (or we bluff a lot), we can defend slightly wider.
			overbetThreshold -= 0.04f * effBluffFreq;

			if (handStrength < overbetThreshold)
			{
				GameManager.LogVerbose($"[AI] Folding to huge overbet ({betRatio:F1}x pot) - need {overbetThreshold:F2}, have {handStrength:F2}");
				return Decision.Fold;
			}
			return Decision.Call;
		}

		// 7) Large bet (0.8x+ pot).
		if (betRatio > 0.8f)
		{
			bool heroCall = false;
			float heroCallChance = 0.0f;

			if (player.CurrentTiltState >= TiltState.Annoyed) heroCallChance += 0.20f;
			if (player.CurrentTiltState >= TiltState.Steaming) heroCallChance += 0.20f;
			if (personality.CallTendency > 0.6f) heroCallChance += 0.15f;

			// Use a real per-hand seed (not a deterministic function of handStrength).
			float heroSeed = GetDecisionSeedForStreet(player, street);
			if (handStrength > 0.30f && heroSeed < heroCallChance)
			{
				heroCall = true;
				GameManager.LogVerbose($"[AI] HERO CALL! (State: {player.CurrentTiltState}, Strength: {handStrength:F2}, Seed: {heroSeed:F2})");
			}

			if (!heroCall)
			{
				float bluffAdjust = Mathf.Lerp(0.03f, -0.03f, effBluffFreq);
				float callThreshold = Mathf.Clamp(threshold + bluffAdjust, 0f, 1f);

				if (handStrength < callThreshold)
				{
					GameManager.LogVerbose($"[AI] Folding to large bet ({betRatio:F1}x pot) on {street} - need {callThreshold:F2}, have {handStrength:F2}");
					return Decision.Fold;
				}
			}
		}
		else
		{
			float lightThreshold;

			// Micro/small bets (<33% pot)
			if (betRatio < 0.33f)
			{
				lightThreshold = threshold - 0.15f;
				if (street == Street.Flop) lightThreshold -= 0.08f;
			}
			// Medium bets (<55% pot)
			else if (betRatio < 0.55f)
			{
				lightThreshold = threshold - 0.10f;
				if (street == Street.Flop) lightThreshold -= 0.05f;
			}
			// Standard bets (0.55 - 0.80)
			else
			{
				lightThreshold = threshold - 0.04f;
				if (street == Street.Flop) lightThreshold -= 0.02f;
			}

			lightThreshold = Mathf.Clamp(lightThreshold, 0.15f, 1f);
			if (handStrength < lightThreshold)
			{
				GameManager.LogVerbose($"[AI] Folding to small/mid bet ({betRatio:F1}x pot) - need {lightThreshold:F2}, have {handStrength:F2}");
				return Decision.Fold;
			}
		}

		return Decision.Call;
	}

	private Decision DecideCheckOrBet(
		float handStrength,
		GameState gameState,
		PokerPersonality personality,
		AIPokerPlayer player,
		out float plannedBetRatio)
	{
		Street street = gameState.Street;

		// Current* stats are already tilt-adjusted in PokerPersonality.
		float effAggression = Mathf.Clamp(personality.CurrentAggression, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);
		float effRiskTol = Mathf.Clamp(personality.CurrentRiskTolerance, 0f, 1f);

		plannedBetRatio = CalculatePlannedBetRatio(handStrength, personality, street, player.BetSizeSeed, player);

		float valueThreshold = street switch
		{
			Street.Flop => PokerAIConfig.FLOP_VALUE_THRESHOLD,
			Street.Turn => PokerAIConfig.TURN_VALUE_THRESHOLD,
			Street.River => PokerAIConfig.RIVER_VALUE_THRESHOLD,
			_ => PokerAIConfig.FLOP_VALUE_THRESHOLD
		};

		float bluffCeiling = street switch
		{
			Street.Flop => PokerAIConfig.FLOP_BLUFF_CEILING,
			Street.Turn => PokerAIConfig.TURN_BLUFF_CEILING,
			Street.River => PokerAIConfig.RIVER_BLUFF_CEILING,
			_ => PokerAIConfig.FLOP_BLUFF_CEILING
		};

		if (!gameState.IsAIInPosition)
		{
			valueThreshold += PokerAIConfig.OOP_VALUE_TIGHTEN;
			bluffCeiling -= PokerAIConfig.OOP_BLUFF_REDUCE;
		}

		// Adjust thresholds based on planned bet size.
		float sizeFactor = Mathf.Clamp(plannedBetRatio, 0f, 2f);
		valueThreshold += PokerAIConfig.SIZE_FACTOR_VALUE_ADJUST * sizeFactor * (1f - effRiskTol);
		bluffCeiling -= PokerAIConfig.SIZE_FACTOR_BLUFF_ADJUST * sizeFactor;

		// VALUE
		if (handStrength >= valueThreshold)
		{
			bool canTrap = player.CurrentTiltState < TiltState.Steaming;
			if (canTrap && handStrength > 0.85f && player.TrapDecisionSeed < PokerAIConfig.TRAP_PROBABILITY)
			{
				GameManager.LogVerbose($"[AI] Trapping with {handStrength:F2} strength");
				plannedBetRatio = 0f;
				return Decision.Check;
			}

			if (handStrength < 0.75f)
			{
				float valueBetFreq = PokerAIConfig.VALUE_BET_BASE_FREQ +
									 (effAggression * PokerAIConfig.VALUE_BET_AGGRESSION_WEIGHT);

				float valueSeed = GetDecisionSeedForStreet(player, street);
				if (valueSeed > valueBetFreq)
				{
					GameManager.LogVerbose($"[AI] Checking value hand ({handStrength:F2}) for deception");
					plannedBetRatio = 0f;
					return Decision.Check;
				}
			}

			return Decision.Bet;
		}

		// BLUFF
		if (handStrength <= bluffCeiling)
		{
			float bluffProb = PokerAIConfig.BLUFF_BASE_PROB * effBluffFreq +
							  PokerAIConfig.BLUFF_AGGRESSION_WEIGHT * effAggression;

			if (player.CurrentTiltState >= TiltState.Steaming)
				bluffProb += 0.20f;

			float bluffSeed = GetDecisionSeedForStreet(player, street);
			if (bluffSeed < bluffProb)
			{
				GameManager.LogVerbose($"[AI] Bluffing on {street} (strength: {handStrength:F2}, size: {plannedBetRatio:F2}x)");
				return Decision.Bet;
			}

			plannedBetRatio = 0f;
			return Decision.Check;
		}

		// MEDIUM HANDS: mixed strategy by street + aggression.
		float mediumBetFreq = street switch
		{
			Street.Flop => 0.25f + (0.40f * effAggression),
			Street.Turn => 0.20f + (0.45f * effAggression),
			Street.River => 0.15f + (0.35f * effAggression),
			_ => 0.20f + (0.35f * effAggression)
		};

		float mediumSeed = GetDecisionSeedForStreet(player, street);
		if (mediumSeed < mediumBetFreq)
		{
			GameManager.LogVerbose($"[AI] Betting medium hand ({handStrength:F2}) for protection/value");
			return Decision.Bet;
		}

		plannedBetRatio = 0f;
		return Decision.Check;
	}

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

	private float CalculatePlannedBetRatio(
		float handStrength,
		PokerPersonality personality,
		Street street,
		float betSizeSeed,
		AIPokerPlayer player)
	{
		float normalizedSeed = betSizeSeed;
		float baseBetMultiplier;

		if (handStrength >= 0.80f)
			baseBetMultiplier = 0.85f + (normalizedSeed * 0.35f);
		else if (handStrength >= 0.65f)
			baseBetMultiplier = 0.65f + (normalizedSeed * 0.30f);
		else if (handStrength >= 0.45f)
			baseBetMultiplier = 0.50f + (normalizedSeed * 0.25f);
		else if (handStrength >= 0.35f)
			baseBetMultiplier = 0.35f + (normalizedSeed * 0.25f);
		else
		{
			if (normalizedSeed < 0.60f)
			{
				float smallBluffSeed = normalizedSeed / 0.60f;
				baseBetMultiplier = 0.35f + (smallBluffSeed * 0.20f);
			}
			else
			{
				float bigBluffSeed = (normalizedSeed - 0.60f) / 0.40f;
				baseBetMultiplier = 1.20f + (bigBluffSeed * 0.40f);
			}
		}

		float streetMultiplier = street switch
		{
			Street.Preflop => PokerAIConfig.PREFLOP_BET_MULTIPLIER,
			Street.Flop => PokerAIConfig.FLOP_BET_MULTIPLIER,
			Street.Turn => PokerAIConfig.TURN_BET_MULTIPLIER,
			Street.River => PokerAIConfig.RIVER_BET_MULTIPLIER,
			_ => 1.0f
		};

		baseBetMultiplier *= streetMultiplier;

		float aggressionMultiplier = 0.9f + (personality.CurrentAggression * 0.3f);
		if (player.CurrentTiltState >= TiltState.Steaming)
			aggressionMultiplier *= 1.25f;

		return baseBetMultiplier * aggressionMultiplier;
	}

	private PlayerAction DecideAllIn(
		float handStrength,
		float betRatio,
		Street street,
		PokerPersonality personality,
		AIPokerPlayer player,
		GameState gameState)
	{
		float effRiskTol = Mathf.Clamp(personality.CurrentRiskTolerance, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);

		if (street == Street.Preflop && player.ChipStack > gameState.BigBlind * 15)
		{
			if (player.CurrentTiltState < TiltState.Steaming && handStrength < 0.60f)
			{
				GameManager.LogVerbose($"[AI] Protecting stack (State: {player.CurrentTiltState}). Folding {handStrength:F2} to preflop shove.");
				return PlayerAction.Fold;
			}
		}

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
			GameManager.LogVerbose($"[AI] Calling all-in with {handStrength:F2} on {street} (threshold: {allInThreshold:F2})");
			return PlayerAction.AllIn;
		}

		float bluffShoveProb = street switch
		{
			Street.Preflop => effBluffFreq * 0.25f,
			Street.Flop => effBluffFreq * 0.20f,
			_ => effBluffFreq * 0.10f
		};

		if (player.CurrentTiltState >= TiltState.Steaming)
			bluffShoveProb *= 2.0f;

		float bluffSeed = GetDecisionSeedForStreet(player, street);
		if (handStrength < 0.25f && bluffSeed < bluffShoveProb)
		{
			GameManager.LogVerbose($"[AI] Bluff shoving on {street}!");
			return PlayerAction.AllIn;
		}

		GameManager.LogVerbose($"[AI] Folding to all-in pressure on {street} ({handStrength:F2} < {allInThreshold:F2})");
		return PlayerAction.Fold;
	}

	private PlayerAction DecideCallOrRaise(
		float handStrength,
		float betRatio,
		Street street,
		PokerPersonality personality,
		AIPokerPlayer player,
		float toCall)
	{
		float effAggression = Mathf.Clamp(personality.CurrentAggression, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);

		float raiseThreshold = street switch
		{
			Street.Flop => 0.62f,
			Street.Turn => 0.67f,
			Street.River => 0.72f,
			_ => 0.60f
		};
		raiseThreshold -= effAggression * 0.15f;

		if (handStrength >= raiseThreshold && player.ChipStack > toCall * 2.5f)
		{
			float raiseProb = effAggression * 0.8f + 0.2f;
			float raiseSeed = GetDecisionSeedForStreet(player, street);

			if (raiseSeed < raiseProb)
			{
				GameManager.LogVerbose($"[AI] Value raising ({handStrength:F2})");
				return PlayerAction.Raise;
			}
		}

		float bluffRaiseProb = effBluffFreq * (street == Street.Flop ? 0.35f : 0.20f);
		if (player.CurrentTiltState >= TiltState.Steaming)
			bluffRaiseProb *= 1.5f;

		float bluffRaiseSeed = GetDecisionSeedForStreet(player, street);
		if (handStrength < 0.32f && bluffRaiseSeed < bluffRaiseProb && player.ChipStack > toCall * 3f)
		{
			GameManager.LogVerbose($"[AI] Bluff raising on {street}");
			return PlayerAction.Raise;
		}

		return PlayerAction.Call;
	}

	public int CalculateRaiseToTotal(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		PokerPersonality personality = player.Personality;

		float effectivePot = Mathf.Max(gameState.PotSize, 1f);
		float currentBet = gameState.CurrentBet;

		// ✅ STEP 1: Early-out if raising is not allowed (under-raise all-in rule)
		if (!gameState.CanAIReopenBetting)
		{
			GameManager.LogVerbose($"[{player.PlayerName}] Cannot raise - betting not reopened. Returning currentBet.");
			return (int)currentBet;  // No raise available
		}

		float baseBetRatio = CalculatePlannedBetRatio(handStrength, personality, gameState.Street, player.BetSizeSeed, player);
		float targetTotal = effectivePot * baseBetRatio;

		if (player.CurrentTiltState >= TiltState.Steaming)
			targetTotal *= 1.15f;

		// ✅ STEP 2: Calculate min-raise using PokerRules [INTEGRATION]
		int minTotalInt = PokerRules.CalculateMinRaiseTotal(
			(int)currentBet,
			(int)gameState.PreviousBet,
			gameState.LastFullRaiseIncrement,
			(int)gameState.BigBlind
		);
		float minTotal = (float)minTotalInt;
		
		GameManager.LogVerbose($"[AI RAISE CALC] LastFullRaiseInc={gameState.LastFullRaiseIncrement}, MinTotal={minTotal}");

		float maxTotal = gameState.GetPlayerCurrentBet(player) + player.ChipStack;

		float legalTotal = (maxTotal < minTotal)
			? maxTotal
			: Mathf.Clamp(targetTotal, minTotal, maxTotal);

		// River commitment logic
		if (gameState.Street == Street.River && handStrength >= 0.60f)
		{
			float spr = player.ChipStack / effectivePot;
			if (spr < 1.0f)
			{
				float committedFrac = (legalTotal - gameState.GetPlayerCurrentBet(player)) / Mathf.Max(player.ChipStack, 1f);
				if (committedFrac >= 0.60f)
				{
					if (handStrength >= 0.70f || player.AllInCommitmentSeed < 0.70f)
						legalTotal = maxTotal;
				}
			}
		}

		// All-in commitment logic
		float amountToAdd = legalTotal - gameState.GetPlayerCurrentBet(player);
		if (amountToAdd >= player.ChipStack * 0.90f)
		{
			if (player.AllInCommitmentSeed < personality.CurrentRiskTolerance || handStrength > 0.80f)
			{
				legalTotal = maxTotal;
			}
			else
			{
				float backedOffTotal = gameState.GetPlayerCurrentBet(player) + player.ChipStack * 0.60f;
				legalTotal = (maxTotal < minTotal)
					? maxTotal
					: Mathf.Clamp(backedOffTotal, minTotal, maxTotal);
			}
		}

		int finalTotal = Mathf.Clamp((int)Mathf.Floor(legalTotal), 1, (int)maxTotal);
		if (maxTotal >= minTotal)
			finalTotal = Math.Max(finalTotal, (int)Mathf.Ceil(minTotal));

		GameManager.LogVerbose($"[{player.PlayerName}] Raise-to total: {finalTotal} (effPot: {effectivePot}, strength: {handStrength:F2}, minTotal: {minTotal}, maxTotal: {maxTotal})");
		return finalTotal;
	}

	public float EvaluateHandStrength(List<Card> holeCards, List<Card> communityCards, Street street, float randomnessSeed)
	{
		if (holeCards == null || holeCards.Count != 2)
		{
			GD.PrintErr("Invalid hole cards in EvaluateHandStrength");
			return 0.2f;
		}

		if (street == Street.Preflop || communityCards == null || communityCards.Count == 0)
			return EvaluatePreflopHand(holeCards);

		// 1) Absolute strength (Raw Rank 1-7461)
		int myRank = HandEvaluator.EvaluateHand(holeCards, communityCards);
		
		// FIX: Use a curve, not a linear map.
		// Rank 1 (Royal) -> 1.0
		// Rank 1600 (Top Pair / Overpair) -> ~0.75
		// Rank 6185 (One Pair) -> ~0.40
		float linearNorm = 1.0f - ((myRank - 1) / 7461.0f);
		float myAbsStrength = (float)Math.Pow(linearNorm, 1.5); // Curves values down, making "good" hands stand out more.

		// 2) Board strength (Check if the board itself is strong/counterfeiting us)
		float boardStrength = 0f;
		if (communityCards.Count >= 5)
		{
			int boardRank = HandEvaluator.EvaluateHand(new List<Card>(), communityCards);
			float boardLinear = 1.0f - ((boardRank - 1) / 7461.0f);
			boardStrength = (float)Math.Pow(boardLinear, 1.5);
		}

		// 3) Counterfeit adjustments
		float adjustedStrength = myAbsStrength;
		bool isCounterfeit = false;

		// Case A: River Chop/Counterfeit
		// If our hand is barely better than the board (kicker problem), reduce strength.
		if (communityCards.Count >= 5 && (myAbsStrength - boardStrength < 0.05f))
		{
			// We are playing the board or have a bad kicker.
			adjustedStrength = 0.25f; // Treat as weak/bluff-catcher
			isCounterfeit = true;
		}
		
		// [Keep your existing Case B logic for Trips/Quads here, it was decent]
		// ... (Your heuristic code for Trips/Quads) ...

		// 4) DRAW POTENTIAL (The Critical Fix)
		if (street == Street.Flop || street == Street.Turn)
		{
			List<Card> allCards = new List<Card>(holeCards);
			allCards.AddRange(communityCards);
			
			// Pass 'street' to method now
			float drawStrength = EvaluateDrawPotential(allCards, street); 

			// CRITICAL CHANGE: Max, not Add.
			// If I have high card (0.1 strength) but a Flush Draw (0.35), my hand is worth 0.35.
			if (drawStrength > adjustedStrength)
			{
				adjustedStrength = drawStrength;
			}
			else
			{
				// Small bonus if we have a Made Hand + Draw (e.g., Top Pair + Flush Draw)
				// But don't double count.
				 adjustedStrength += (drawStrength * 0.20f); 
			}
		}

		// 5) Randomness
		float randomness = randomnessSeed * 0.05f; // Reduced variance
		return Mathf.Clamp(adjustedStrength + randomness, 0.10f, 1.0f);
	}


	private float EvaluatePreflopHand(List<Card> holeCards)
	{
		if (holeCards.Count != 2) return 0.2f;

		Card card1 = holeCards[0];
		Card card2 = holeCards[1];

		bool isPair = card1.Rank == card2.Rank;
		bool isSuited = card1.Suit == card2.Suit;
		
		// Ace is 12. 
		int highCard = Mathf.Max((int)card1.Rank, (int)card2.Rank);
		int lowCard = Mathf.Min((int)card1.Rank, (int)card2.Rank);
		int rankDiff = highCard - lowCard;
		bool isConnector = rankDiff == 1;
		bool isGapper = rankDiff == 2;

		float strength = 0.2f; // Base trash level

		if (isPair)
		{
			// Pairs are massive in Heads Up. 
			// 22 starts at 0.50. AA is 0.95.
			// Curve: 0.5 + (Rank/14)^2 * 0.45
			float pairPower = (float)highCard / 14.0f;
			strength = 0.50f + (pairPower * pairPower * 0.45f);
		}
		else
		{
			// 1. High Card Strength (Heads up, high card wins often)
			// A-high = 0.60 base. 7-high = 0.30 base.
			strength = 0.30f + ((highCard) / 12.0f) * 0.35f;

			// 2. Kicker Strength Adjustment
			// A-K is better than A-2.
			strength += ((lowCard) / 12.0f) * 0.10f;

			// 3. Suited Bonus
			// Suitedness is worth ~3-4% equity.
			if (isSuited) strength += 0.04f;

			// 4. Connector/Gapper Bonus
			// Connectors play well post-flop.
			if (isConnector) strength += 0.03f;
			if (isGapper) strength += 0.015f;
			
			// 5. Penalty for "Trash" (Low uncoordinated cards)
			// e.g. 9-2 offsuit.
			if (highCard < 10 && !isSuited && !isConnector && !isGapper)
			{
				strength -= 0.10f;
			}
		}

		return Mathf.Clamp(strength, 0.15f, 0.98f);
	}


	private float EvaluateDrawPotential(List<Card> cards, Street street)
	{
		// If we are on the River, there are no more draws.
		if (street == Street.River) return 0f;

		// Check Flush Draws
		var suitCounts = cards.GroupBy(c => c.Suit).Select(g => g.Count());
		bool flushDraw = suitCounts.Any(count => count == 4);
		bool backdoorFlush = suitCounts.Any(count => count == 3);

		// Check Straight Draws
		var ranks = cards.Select(c => (int)c.Rank).OrderBy(r => r).Distinct().ToList();
		
		// Check for OESD (4 consecutive ranks)
		bool oesd = false;
		for (int i = 0; i < ranks.Count - 3; i++)
		{
			if (ranks[i + 3] - ranks[i] == 3) oesd = true;
		}

		// Check for Gutshot (gap of 1 inside a 4-card span, e.g., 4,5,7,8)
		// Or simply 4 cards within a span of 5 values (e.g. 4,5,7,8 is span 4..8 = 4)
		bool gutshot = false;
		if (!oesd)
		{
			// Simple heuristic for gutshot: 4 cards within a range of 5
			for (int i = 0; i < ranks.Count - 3; i++)
			{
				 if (ranks[i + 3] - ranks[i] == 4) gutshot = true;
			}
		}

		// --- SCORING ---
		// Values represent approximate equity or "semi-bluffability"
		
		if (flushDraw && oesd) return 0.55f; // Monster draw (Combo)
		if (flushDraw && gutshot) return 0.45f; // Strong combo
		if (flushDraw) return 0.35f; // Standard flush draw (~36% equity)
		if (oesd) return 0.30f; // Standard straight draw (~32% equity)
		if (gutshot) return 0.15f; // Weak draw (~16% equity)
		
		// Flop only: Backdoor equity
		if (street == Street.Flop && backdoorFlush) return 0.05f;

		return 0f;
	}

}
