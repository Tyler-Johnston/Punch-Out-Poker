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

		GD.Print(
			$"[AI STATE] {player.PlayerName} | Street: {gameState.Street} | Strength: {handStrength:F2} | " +
			$"ToCall: {toCall} | BetRatio: {betRatio:F2} | Tilt: {player.CurrentTiltState} ({personality.TiltMeter:F0}) | " +
			$"Pos: {(gameState.IsAIInPosition ? "IP" : "OOP")}"
		);

		// No bet to face - check or bet decision.
		if (toCall <= 0)
		{
			Decision decision = DecideCheckOrBet(handStrength, gameState, personality, player, out float plannedBetRatio);
			PlayerAction action = (decision == Decision.Bet) ? PlayerAction.Raise : PlayerAction.Check;
			//GD.Print($"[AI ACTION] {player.PlayerName} {action} (plan: {plannedBetRatio:F2}x pot)");
			return action;
		}

		// All-in pressure (cannot call more than stack).
		if (toCall >= player.ChipStack)
		{
			// If all-in is small relative to pot, treat as normal call/fold decision.
			if (betRatio < 0.50f)
			{
				GD.Print($"[AI] Small all-in ({betRatio:F2}x pot), using normal call logic");

				Decision callFold = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality, player);
				PlayerAction action = (callFold == Decision.Fold) ? PlayerAction.Fold : PlayerAction.AllIn;
				GD.Print($"[AI ACTION] {player.PlayerName} {action}");
				return action;
			}

			PlayerAction allInAction = DecideAllIn(handStrength, betRatio, gameState.Street, personality, player, gameState);
			GD.Print($"[AI ACTION] {player.PlayerName} {allInAction}");
			return allInAction;
		}

		// Standard facing-bet decision.
		Decision callFoldDecision = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality, player);
		if (callFoldDecision == Decision.Fold)
		{
			GD.Print($"[AI ACTION] {player.PlayerName} Fold");
			return PlayerAction.Fold;
		}

		// We're continuing - now decide call vs raise.
		PlayerAction finalAction = DecideCallOrRaise(handStrength, betRatio, gameState.Street, personality, player, toCall);
		GD.Print($"[AI ACTION] {player.PlayerName} {finalAction}");
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
			GD.Print($"[AI] Easy call - pot odds: {potOdds:F2}, equity: {handStrength:F2} (Sticky Mode: {betRatio < 0.40f})");
			return Decision.Call;
		}

		// 2) Effective stats.
		// IMPORTANT: CurrentAggression/CurrentBluffFrequency/CurrentRiskTolerance are already tilt-adjusted
		// in PokerPersonality.UpdateStatsFromTilt(). Do not re-apply tilt scaling here.
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
				GD.Print($"[AI] Folding to huge overbet ({betRatio:F1}x pot) - need {overbetThreshold:F2}, have {handStrength:F2}");
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
				GD.Print($"[AI] HERO CALL! (State: {player.CurrentTiltState}, Strength: {handStrength:F2}, Seed: {heroSeed:F2})");
			}

			if (!heroCall)
			{
				float bluffAdjust = Mathf.Lerp(0.03f, -0.03f, effBluffFreq);
				float callThreshold = Mathf.Clamp(threshold + bluffAdjust, 0f, 1f);

				if (handStrength < callThreshold)
				{
					GD.Print($"[AI] Folding to large bet ({betRatio:F1}x pot) on {street} - need {callThreshold:F2}, have {handStrength:F2}");
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
				GD.Print($"[AI] Folding to small/mid bet ({betRatio:F1}x pot) - need {lightThreshold:F2}, have {handStrength:F2}");
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
				GD.Print($"[AI] Trapping with {handStrength:F2} strength");
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
					GD.Print($"[AI] Checking value hand ({handStrength:F2}) for deception");
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
				GD.Print($"[AI] Bluffing on {street} (strength: {handStrength:F2}, size: {plannedBetRatio:F2}x)");
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
			GD.Print($"[AI] Betting medium hand ({handStrength:F2}) for protection/value");
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
				GD.Print($"[AI] Protecting stack (State: {player.CurrentTiltState}). Folding {handStrength:F2} to preflop shove.");
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
			GD.Print($"[AI] Calling all-in with {handStrength:F2} on {street} (threshold: {allInThreshold:F2})");
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
			GD.Print($"[AI] Bluff shoving on {street}!");
			return PlayerAction.AllIn;
		}

		GD.Print($"[AI] Folding to all-in pressure on {street} ({handStrength:F2} < {allInThreshold:F2})");
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
				GD.Print($"[AI] Value raising ({handStrength:F2})");
				return PlayerAction.Raise;
			}
		}

		float bluffRaiseProb = effBluffFreq * (street == Street.Flop ? 0.35f : 0.20f);
		if (player.CurrentTiltState >= TiltState.Steaming)
			bluffRaiseProb *= 1.5f;

		float bluffRaiseSeed = GetDecisionSeedForStreet(player, street);
		if (handStrength < 0.32f && bluffRaiseSeed < bluffRaiseProb && player.ChipStack > toCall * 3f)
		{
			GD.Print($"[AI] Bluff raising on {street}");
			return PlayerAction.Raise;
		}

		return PlayerAction.Call;
	}

	public int CalculateRaiseToTotal(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		//
		//if (GameManager.Instance.DevTestMode)
		//{
			//// Check if Ctrl+Shift are held (this won't work in _Input, must poll)
			//bool ctrlHeld = Input.IsKeyPressed(Key.Ctrl);
			//bool shiftHeld = Input.IsKeyPressed(Key.Shift);
			//
			//int tinyRaise = (int)(gameState.CurrentBet * 0.5f); // 50% of currentBet (illegal!)
			//GD.Print($"[DEBUG OVERRIDE] 🔧 Forcing illegal tiny raise: {tinyRaise} (currentBet: {gameState.CurrentBet})");
			//GD.Print($"[DEBUG OVERRIDE] This should trigger safety checks in OnOpponentRaise()");
			//return tinyRaise;
		//}
	
		PokerPersonality personality = player.Personality;

		float effectivePot = Mathf.Max(gameState.PotSize, 1f);
		float currentBet = gameState.CurrentBet;

		float baseBetRatio = CalculatePlannedBetRatio(handStrength, personality, gameState.Street, player.BetSizeSeed, player);
		float targetTotal = effectivePot * baseBetRatio;

		if (player.CurrentTiltState >= TiltState.Steaming)
			targetTotal *= 1.15f;

		float minTotal;
		if (currentBet <= 0)
		{
			minTotal = gameState.BigBlind;
		}
		else
		{
			float lastRaiseIncrement = Mathf.Max(currentBet - gameState.PreviousBet, gameState.BigBlind);
			minTotal = currentBet + lastRaiseIncrement;
		}

		float maxTotal = gameState.GetPlayerCurrentBet(player) + player.ChipStack;

		float legalTotal = (maxTotal < minTotal)
			? maxTotal
			: Mathf.Clamp(targetTotal, minTotal, maxTotal);

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

		GD.Print($"[{player.PlayerName}] Raise-to total: {finalTotal} (effPot: {effectivePot}, strength: {handStrength:F2}, minTotal: {minTotal}, maxTotal: {maxTotal})");
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

		// 1) Absolute strength.
		int myRank = HandEvaluator.EvaluateHand(holeCards, communityCards);
		float myAbsStrength = 1.0f - ((myRank - 1) / 7461.0f);

		// 2) Board strength (river counterfeit check).
		float boardStrength = 0f;
		if (communityCards.Count >= 5)
		{
			int boardRank = HandEvaluator.EvaluateHand(new List<Card>(), communityCards);
			boardStrength = 1.0f - ((boardRank - 1) / 7461.0f);
		}

		// 3) Counterfeit adjustments.
		float adjustedStrength = myAbsStrength;
		bool isCounterfeit = false;

		// Case A: River counterfeit (exact-ish).
		if (communityCards.Count >= 5 && myAbsStrength > 0.28f && (myAbsStrength - boardStrength < 0.03f))
		{
			GD.Print($"[AI] Counterfeit detected! Abs: {myAbsStrength:F2} vs Board: {boardStrength:F2}");
			adjustedStrength = 0.2f;
			isCounterfeit = true;
		}
		// Case B: Flop/Turn counterfeit heuristic.
		else if (communityCards.Count >= 3)
		{
			var boardRankGroups = communityCards.GroupBy(c => c.Rank).ToList();
			int maxBoardMatch = boardRankGroups.Max(g => g.Count());

			bool boardHasTrips = maxBoardMatch >= 3;
			bool boardHasQuads = maxBoardMatch >= 4;
			bool boardIsPaired = maxBoardMatch >= 2;

			bool holeCardsArePair = holeCards[0].Rank == holeCards[1].Rank;

			bool hitBoard = holeCards.Any(card => communityCards.Any(c => c.Rank == card.Rank));

			bool havePocketPairBetterThanBoard = false;
			if (holeCardsArePair)
			{
				int ourPairRank = (int)holeCards[0].Rank;
				int boardPairRank = boardRankGroups
					.Where(g => g.Count() >= 2)
					.Select(g => (int)g.Key)
					.FirstOrDefault();
				havePocketPairBetterThanBoard = ourPairRank > boardPairRank;
			}

			// Trips/quads on board.
			if (boardHasTrips || boardHasQuads)
			{
				var tripRank = boardRankGroups.First(g => g.Count() >= 3).Key;
				bool haveQuads = holeCardsArePair && holeCards[0].Rank == tripRank;
				bool haveFullHouse = holeCardsArePair && !haveQuads;
				bool haveKicker = holeCards.Any(c => c.Rank == tripRank);

				if (haveQuads)
				{
					GD.Print($"[AI] We have QUADS on trip board! Keeping strength {myAbsStrength:F2}");
				}
				else if (haveFullHouse && havePocketPairBetterThanBoard)
				{
					GD.Print($"[AI] Full house on trip board with {holeCards[0].Rank} pocket pair");
					adjustedStrength = Mathf.Max(adjustedStrength, 0.65f);
				}
				else if (haveKicker)
				{
					int kickerValue = (int)holeCards.First(c => c.Rank != tripRank).Rank;
					float kickerStrength = kickerValue / 14f;

					adjustedStrength = 0.25f + (kickerStrength * 0.25f);
					isCounterfeit = true;
					GD.Print($"[AI] Trip board kicker battle: {adjustedStrength:F2} (kicker: {kickerValue})");
				}
				else
				{
					adjustedStrength = 0.15f;
					isCounterfeit = true;
					GD.Print($"[AI] EXTREME COUNTERFEIT! Trip/Quad board, no connection. Strength: {adjustedStrength:F2}");
				}
			}
			// Paired board + missed.
			else if (boardIsPaired && !holeCardsArePair && !hitBoard && myAbsStrength < 0.75f)
			{
				adjustedStrength *= 0.60f;
				isCounterfeit = true;
				GD.Print($"[AI] Early Counterfeit Heuristic: Board Paired & Missed. Downgrading {myAbsStrength:F2} -> {adjustedStrength:F2}");
			}
		}

		// 4) Curve strength.
		adjustedStrength = (float)Math.Pow(adjustedStrength, 0.75);

		// 5) Safety floor for made hands (skip if counterfeit).
		if (!isCounterfeit && myRank <= 6185)
		{
			if (adjustedStrength >= myAbsStrength * 0.9f)
				adjustedStrength = Math.Max(adjustedStrength, 0.38f);
		}

		// 6) Add draw potential (flop/turn only).
		if (street == Street.Flop || street == Street.Turn)
		{
			List<Card> allCards = new List<Card>(holeCards);
			allCards.AddRange(communityCards);
			adjustedStrength += EvaluateDrawPotential(allCards) * 0.10f;
		}

		// 7) Add small per-hand randomness.
		float randomness = randomnessSeed * 0.08f;
		return Mathf.Clamp(adjustedStrength + randomness, 0.10f, 1.0f);
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
