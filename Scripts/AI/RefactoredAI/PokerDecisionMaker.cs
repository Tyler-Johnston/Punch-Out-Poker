using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PokerDecisionMaker : Node
{
	public PlayerAction DecideAction(AIPokerPlayer player, GameState gameState)
	{
		PlayerStats playerStats = gameState.CurrentPlayerStats ?? new PlayerStats();

		float handStrength = EvaluateHandStrength(
			player.Hand, gameState.CommunityCards,
			gameState.Street, player.HandRandomnessSeed);

		PokerPersonality personality = player.Personality;
		float toCall   = gameState.CurrentBet - gameState.GetPlayerCurrentBet(player);
		float potSize  = Mathf.Max(gameState.PotSize, 1f);
		float betRatio = toCall / potSize;

		GameManager.LogVerbose(
			$"[AI STATE] {player.PlayerName} | Street: {gameState.Street} | Strength: {handStrength:F2} | " +
			$"ToCall: {toCall} | BetRatio: {betRatio:F2} | Tilt: {player.CurrentTiltState} ({personality.TiltMeter:F0}) | " +
			$"Pos: {(gameState.IsAIInPosition ? "IP" : "OOP")}");

		if (toCall <= 0)
		{
			bool shouldBet = DecideCheckOrBet(handStrength, gameState, personality, player);
			return shouldBet ? PlayerAction.Raise : PlayerAction.Check;
		}

		if (toCall >= player.ChipStack)
		{
			if (betRatio < PokerAIConfig.ALLIN_SMALL_BET_RATIO)
			{
				GameManager.LogVerbose($"[AI] Small all-in ({betRatio:F2}x pot), using normal call logic");
				Decision callFold = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality, player, playerStats);
				return (callFold == Decision.Fold) ? PlayerAction.Fold : PlayerAction.AllIn;
			}
			return DecideAllIn(handStrength, betRatio, gameState.Street, personality, player, gameState, playerStats);
		}

		Decision callFoldDecision = DecideCallOrFold(handStrength, betRatio, potSize, toCall, gameState.Street, personality, player, playerStats);
		if (callFoldDecision == Decision.Fold)
			return PlayerAction.Fold;

		if (!gameState.CanAIReopenBetting)
		{
			GameManager.LogVerbose("[AI] Cannot raise - betting not reopened. Forcing Call.");
			return PlayerAction.Call;
		}

		return DecideCallOrRaise(handStrength, betRatio, gameState.Street, personality, player, toCall);
	}

	private Decision DecideCallOrFold(
		float handStrength, float betRatio, float potSize, float toCall,
		Street street, PokerPersonality personality, AIPokerPlayer player, PlayerStats playerStats)
	{
		// 1) Easy pot-odds call
		float potOdds        = toCall / (potSize + toCall);
		float oddsMultiplier = (betRatio < 0.40f) ? 1.0f : PokerAIConfig.POT_ODDS_MULTIPLIER;

		if (potOdds < PokerAIConfig.POT_ODDS_OVERRIDE_THRESHOLD &&
			handStrength > potOdds * oddsMultiplier)
		{
			GameManager.LogVerbose($"[AI] Easy call - pot odds: {potOdds:F2}, equity: {handStrength:F2}");
			return Decision.Call;
		}

		float effCallTend  = Mathf.Clamp(personality.CallTendency * (1f + personality.TiltMeter / 200f), 0f, 1f);
		float effRiskTol   = Mathf.Clamp(personality.CurrentRiskTolerance, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);

		// 2) Opponent model adjustment
		float opponentAdjust = GetOpponentAggressionAdjustment(playerStats);

		// 3) Street base threshold + adjustments
		float baseThreshold  = GetCallBaseThreshold(street);
		baseThreshold       += opponentAdjust;
		baseThreshold       += Mathf.Lerp(0.06f, -0.06f, effCallTend);

		// 4) Bet size scaling
		float sizeFactor = Mathf.Clamp(betRatio, 0f, 3f);
		float sizeBump   = (street == Street.Flop)
			? PokerAIConfig.FLOP_SIZE_BUMP * sizeFactor
			: PokerAIConfig.LATER_STREET_SIZE_BUMP * sizeFactor;
		sizeBump *= (1f - 0.5f * effRiskTol);

		float threshold = Mathf.Clamp(baseThreshold + sizeBump, 0f, 1f);

		// 5) Overbet (2.5x+ pot)
		if (betRatio >= PokerAIConfig.OVERBET_RATIO_THRESHOLD)
		{
			if (player.CurrentTiltState == TiltState.Monkey) return Decision.Call;

			float overbetTighten   = Mathf.Lerp(PokerAIConfig.OVERBET_RISK_MIN, PokerAIConfig.OVERBET_RISK_MAX, 1f - effRiskTol);
			float overbetThreshold = Mathf.Clamp(PokerAIConfig.OVERBET_BASE_THRESHOLD + overbetTighten, 0f, 1f);
			overbetThreshold      -= PokerAIConfig.OVERBET_BLUFF_DISCOUNT * effBluffFreq;

			if (handStrength < overbetThreshold)
			{
				GameManager.LogVerbose($"[AI] Folding to overbet ({betRatio:F1}x) - need {overbetThreshold:F2}, have {handStrength:F2}");
				return Decision.Fold;
			}
			return Decision.Call;
		}

		// 6) Large bet (0.8x+ pot)
		if (betRatio > PokerAIConfig.LARGE_BET_RATIO)
		{
			if (ShouldHeroCall(handStrength, player, street))
				return Decision.Call;

			float bluffAdjust   = Mathf.Lerp(0.03f, -0.03f, effBluffFreq);
			float callThreshold = Mathf.Clamp(threshold + bluffAdjust, 0f, 1f);

			if (handStrength < callThreshold)
			{
				GameManager.LogVerbose($"[AI] Folding to large bet ({betRatio:F1}x) on {street} - need {callThreshold:F2}, have {handStrength:F2}");
				return Decision.Fold;
			}
			return Decision.Call;
		}

		// 7) Small / medium bets
		float lightThreshold = GetLightCallThreshold(threshold, betRatio, street);
		if (handStrength < lightThreshold)
		{
			GameManager.LogVerbose($"[AI] Folding to small/mid bet ({betRatio:F1}x) - need {lightThreshold:F2}, have {handStrength:F2}");
			return Decision.Fold;
		}
		return Decision.Call;
	}

	private bool DecideCheckOrBet(
		float handStrength, GameState gameState,
		PokerPersonality personality, AIPokerPlayer player)
	{
		Street street       = gameState.Street;
		float effAggression = Mathf.Clamp(personality.CurrentAggression, 0f, 1f);
		float effBluffFreq  = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);
		float effRiskTol    = Mathf.Clamp(personality.CurrentRiskTolerance, 0f, 1f);

		float plannedBetRatio = CalculatePlannedBetRatio(handStrength, personality, street, player.BetSizeSeed, player);
		float valueThreshold  = GetValueThreshold(street, gameState.IsAIInPosition, plannedBetRatio, effRiskTol);
		float bluffCeiling    = GetBluffCeiling(street, gameState.IsAIInPosition, plannedBetRatio);

		// VALUE BET
		if (handStrength >= valueThreshold)
		{
			if (player.CurrentTiltState < TiltState.Steaming &&
				handStrength > 0.85f &&
				player.TrapDecisionSeed < PokerAIConfig.TRAP_PROBABILITY)
			{
				GameManager.LogVerbose($"[AI] Trapping with {handStrength:F2}");
				return false;
			}

			if (handStrength < 0.75f)
			{
				float valueBetFreq = PokerAIConfig.VALUE_BET_BASE_FREQ + (effAggression * PokerAIConfig.VALUE_BET_AGGRESSION_WEIGHT);
				if (GetDecisionSeedForStreet(player, street) > valueBetFreq)
				{
					GameManager.LogVerbose($"[AI] Checking value hand ({handStrength:F2}) for deception");
					return false;
				}
			}
			return true;
		}

		// BLUFF
		if (handStrength <= bluffCeiling)
		{
			float bluffProb = PokerAIConfig.BLUFF_BASE_PROB * effBluffFreq +
							  PokerAIConfig.BLUFF_AGGRESSION_WEIGHT * effAggression;
			if (player.CurrentTiltState >= TiltState.Steaming) bluffProb += 0.20f;

			if (GetDecisionSeedForStreet(player, street) < bluffProb)
			{
				GameManager.LogVerbose($"[AI] Bluffing on {street} ({handStrength:F2})");
				return true;
			}
			return false;
		}

		// MEDIUM HAND: mixed strategy
		float mediumBetFreq = GetMediumBetFrequency(street, effAggression);
		if (GetDecisionSeedForStreet(player, street) < mediumBetFreq)
		{
			GameManager.LogVerbose($"[AI] Betting medium hand ({handStrength:F2}) for protection/value");
			return true;
		}
		return false;
	}

	private PlayerAction DecideAllIn(
		float handStrength, float betRatio, Street street,
		PokerPersonality personality, AIPokerPlayer player,
		GameState gameState, PlayerStats playerStats)
	{
		float effRiskTol   = Mathf.Clamp(personality.CurrentRiskTolerance, 0f, 1f);
		float effBluffFreq = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);

		if (street == Street.Preflop &&
			player.ChipStack > gameState.BigBlind * PokerAIConfig.ALLIN_STACK_GUARD_BB_MULTIPLIER &&
			player.CurrentTiltState < TiltState.Steaming &&
			handStrength < PokerAIConfig.ALLIN_PREFLOP_STACK_GUARD_MIN_STRENGTH)
		{
			GameManager.LogVerbose($"[AI] Stack guard fold ({handStrength:F2})");
			return PlayerAction.Fold;
		}

		float allInThreshold = GetAllInThreshold(street, effRiskTol);

		if (playerStats.HasEnoughData())
		{
			float callAdjustment  = playerStats.GetAllInCallAdjustment();
			allInThreshold       -= callAdjustment;
			GameManager.LogVerbose($"[AI] All-in threshold adjusted by {-callAdjustment:F2} (shove freq {playerStats.AllInFrequency:P0})");
		}

		allInThreshold = Mathf.Clamp(allInThreshold, 0.20f, 0.95f);

		if (handStrength >= allInThreshold)
		{
			GameManager.LogVerbose($"[AI] Calling all-in ({handStrength:F2} >= {allInThreshold:F2}) on {street}");
			return PlayerAction.AllIn;
		}

		float bluffShoveProb = GetBluffShoveProb(street, effBluffFreq, player.CurrentTiltState);
		if (handStrength < PokerAIConfig.ALLIN_BLUFF_MAX_STRENGTH &&
			GetDecisionSeedForStreet(player, street) < bluffShoveProb)
		{
			GameManager.LogVerbose($"[AI] Bluff shoving on {street}!");
			return PlayerAction.AllIn;
		}

		GameManager.LogVerbose($"[AI] Folding to all-in ({handStrength:F2} < {allInThreshold:F2}) on {street}");
		return PlayerAction.Fold;
	}

	private PlayerAction DecideCallOrRaise(
		float handStrength, float betRatio, Street street,
		PokerPersonality personality, AIPokerPlayer player, float toCall)
	{
		float effAggression = Mathf.Clamp(personality.CurrentAggression, 0f, 1f);
		float effBluffFreq  = Mathf.Clamp(personality.CurrentBluffFrequency, 0f, 1f);

		float raiseThreshold = GetRaiseThreshold(street, effAggression);

		if (handStrength >= raiseThreshold &&
			player.ChipStack > toCall * PokerAIConfig.RAISE_MIN_STACK_MULTIPLIER)
		{
			float raiseProb = PokerAIConfig.RAISE_PROB_BASE + effAggression * PokerAIConfig.RAISE_PROB_AGG_WEIGHT;
			if (GetDecisionSeedForStreet(player, street) < raiseProb)
			{
				GameManager.LogVerbose($"[AI] Value raising ({handStrength:F2})");
				return PlayerAction.Raise;
			}
		}

		float bluffRaiseScale = (street == Street.Flop)
			? PokerAIConfig.BLUFF_RAISE_FLOP_SCALE
			: PokerAIConfig.BLUFF_RAISE_LATER_SCALE;
		float bluffRaiseProb = effBluffFreq * bluffRaiseScale;
		if (player.CurrentTiltState >= TiltState.Steaming)
			bluffRaiseProb *= PokerAIConfig.BLUFF_RAISE_TILT_MULTIPLIER;

		if (handStrength < PokerAIConfig.BLUFF_RAISE_MAX_STRENGTH &&
			GetDecisionSeedForStreet(player, street) < bluffRaiseProb &&
			player.ChipStack > toCall * PokerAIConfig.BLUFF_RAISE_MIN_STACK_MULTIPLIER)
		{
			GameManager.LogVerbose($"[AI] Bluff raising on {street}");
			return PlayerAction.Raise;
		}

		return PlayerAction.Call;
	}

	// --- PRIVATE HELPERS ---

	private float GetCallBaseThreshold(Street street) => street switch
	{
		Street.Flop  => PokerAIConfig.FLOP_BASE_THRESHOLD,
		Street.Turn  => PokerAIConfig.TURN_BASE_THRESHOLD,
		Street.River => PokerAIConfig.RIVER_BASE_THRESHOLD,
		_            => PokerAIConfig.PREFLOP_BASE_THRESHOLD
	};

	private float GetAllInThreshold(Street street, float effRiskTol) => street switch
	{
		Street.Preflop => PokerAIConfig.ALLIN_PREFLOP_BASE - (effRiskTol * PokerAIConfig.ALLIN_RISK_TOL_SCALE),
		Street.Flop    => PokerAIConfig.ALLIN_FLOP_BASE    - (effRiskTol * PokerAIConfig.ALLIN_RISK_TOL_SCALE),
		Street.Turn    => PokerAIConfig.ALLIN_TURN_BASE    - (effRiskTol * PokerAIConfig.ALLIN_RISK_TOL_SCALE),
		Street.River   => PokerAIConfig.ALLIN_RIVER_BASE   - (effRiskTol * PokerAIConfig.ALLIN_RISK_TOL_SCALE),
		_              => PokerAIConfig.ALLIN_FLOP_BASE    - (effRiskTol * PokerAIConfig.ALLIN_RISK_TOL_SCALE)
	};

	private float GetRaiseThreshold(Street street, float effAggression) => street switch
	{
		Street.Flop  => PokerAIConfig.RAISE_FLOP_BASE   - effAggression * PokerAIConfig.RAISE_AGGRESSION_SCALE,
		Street.Turn  => PokerAIConfig.RAISE_TURN_BASE   - effAggression * PokerAIConfig.RAISE_AGGRESSION_SCALE,
		Street.River => PokerAIConfig.RAISE_RIVER_BASE  - effAggression * PokerAIConfig.RAISE_AGGRESSION_SCALE,
		_            => PokerAIConfig.RAISE_PREFLOP_BASE - effAggression * PokerAIConfig.RAISE_AGGRESSION_SCALE
	};

	private float GetBluffShoveProb(Street street, float effBluffFreq, TiltState tilt)
	{
		float baseProb = street switch
		{
			Street.Preflop => effBluffFreq * PokerAIConfig.ALLIN_BLUFF_SHOVE_PREFLOP,
			Street.Flop    => effBluffFreq * PokerAIConfig.ALLIN_BLUFF_SHOVE_FLOP,
			_              => effBluffFreq * PokerAIConfig.ALLIN_BLUFF_SHOVE_LATER
		};
		if (tilt >= TiltState.Steaming) baseProb *= PokerAIConfig.ALLIN_BLUFF_TILT_MULTIPLIER;
		return baseProb;
	}

	private float GetValueThreshold(Street street, bool isOOP, float plannedBetRatio, float effRiskTol)
	{
		float threshold = street switch
		{
			Street.Flop  => PokerAIConfig.FLOP_VALUE_THRESHOLD,
			Street.Turn  => PokerAIConfig.TURN_VALUE_THRESHOLD,
			Street.River => PokerAIConfig.RIVER_VALUE_THRESHOLD,
			_            => PokerAIConfig.FLOP_VALUE_THRESHOLD
		};
		if (isOOP) threshold += PokerAIConfig.OOP_VALUE_TIGHTEN;

		float sizeFactor = Mathf.Clamp(plannedBetRatio, 0f, 2f);
		threshold += PokerAIConfig.SIZE_FACTOR_VALUE_ADJUST * sizeFactor * (1f - effRiskTol);
		return threshold;
	}

	private float GetBluffCeiling(Street street, bool isOOP, float plannedBetRatio)
	{
		float ceiling = street switch
		{
			Street.Flop  => PokerAIConfig.FLOP_BLUFF_CEILING,
			Street.Turn  => PokerAIConfig.TURN_BLUFF_CEILING,
			Street.River => PokerAIConfig.RIVER_BLUFF_CEILING,
			_            => PokerAIConfig.FLOP_BLUFF_CEILING
		};
		if (isOOP) ceiling -= PokerAIConfig.OOP_BLUFF_REDUCE;

		float sizeFactor = Mathf.Clamp(plannedBetRatio, 0f, 2f);
		ceiling -= PokerAIConfig.SIZE_FACTOR_BLUFF_ADJUST * sizeFactor;
		return ceiling;
	}

	private float GetMediumBetFrequency(Street street, float effAggression) => street switch
	{
		Street.Flop  => 0.25f + (0.40f * effAggression),
		Street.Turn  => 0.20f + (0.45f * effAggression),
		Street.River => 0.15f + (0.35f * effAggression),
		_            => 0.20f + (0.35f * effAggression)
	};

	private float GetOpponentAggressionAdjustment(PlayerStats playerStats)
	{
		if (!playerStats.HasEnoughData()) return 0f;

		if (playerStats.RaiseFrequency > PokerAIConfig.OPPONENT_MANIAC_RAISE_FREQ ||
			playerStats.AggressionFactor > PokerAIConfig.OPPONENT_MANIAC_AGG_FACTOR)
		{
			GameManager.LogVerbose($"[AI] Player is aggressive — loosening defense by {PokerAIConfig.OPPONENT_MANIAC_ADJUST:F2}");
			return PokerAIConfig.OPPONENT_MANIAC_ADJUST;
		}

		if (playerStats.RaiseFrequency < PokerAIConfig.OPPONENT_PASSIVE_RAISE_FREQ)
		{
			GameManager.LogVerbose($"[AI] Player is passive — tightening defense by {PokerAIConfig.OPPONENT_PASSIVE_ADJUST:F2}");
			return PokerAIConfig.OPPONENT_PASSIVE_ADJUST;
		}

		return 0f;
	}

	private float GetLightCallThreshold(float threshold, float betRatio, Street street)
	{
		float light;
		if (betRatio < PokerAIConfig.SMALL_BET_RATIO)
		{
			light = threshold - 0.15f;
			if (street == Street.Flop) light -= 0.08f;
		}
		else if (betRatio < PokerAIConfig.MID_BET_RATIO)
		{
			light = threshold - 0.10f;
			if (street == Street.Flop) light -= 0.05f;
		}
		else
		{
			light = threshold - 0.04f;
			if (street == Street.Flop) light -= 0.02f;
		}
		return Mathf.Clamp(light, 0.15f, 1f);
	}

	private bool ShouldHeroCall(float handStrength, AIPokerPlayer player, Street street)
	{
		float heroCallChance = 0f;
		if (player.CurrentTiltState >= TiltState.Annoyed)  heroCallChance += 0.20f;
		if (player.CurrentTiltState >= TiltState.Steaming) heroCallChance += 0.20f;
		if (player.Personality.CallTendency > 0.6f)        heroCallChance += 0.15f;

		float heroSeed = GetDecisionSeedForStreet(player, street);
		bool  heroCall = handStrength > 0.30f && heroSeed < heroCallChance;

		if (heroCall) GameManager.LogVerbose($"[AI] HERO CALL! (State: {player.CurrentTiltState}, Str: {handStrength:F2}, Seed: {heroSeed:F2})");
		return heroCall;
	}

	private float GetDecisionSeedForStreet(AIPokerPlayer player, Street street) => street switch
	{
		Street.Preflop => player.PreflopDecisionSeed,
		Street.Flop    => player.FlopDecisionSeed,
		Street.Turn    => player.TurnDecisionSeed,
		Street.River   => player.RiverDecisionSeed,
		_              => player.FlopDecisionSeed
	};

	private float CalculatePlannedBetRatio(
		float handStrength, PokerPersonality personality,
		Street street, float betSizeSeed, AIPokerPlayer player)
	{
		float baseBetMultiplier;

		if      (handStrength >= 0.80f) baseBetMultiplier = PokerAIConfig.BET_STRONG_BASE + betSizeSeed * PokerAIConfig.BET_STRONG_SEED;
		else if (handStrength >= 0.65f) baseBetMultiplier = PokerAIConfig.BET_GOOD_BASE   + betSizeSeed * PokerAIConfig.BET_GOOD_SEED;
		else if (handStrength >= 0.45f) baseBetMultiplier = PokerAIConfig.BET_MEDIUM_BASE + betSizeSeed * PokerAIConfig.BET_MEDIUM_SEED;
		else if (handStrength >= 0.35f) baseBetMultiplier = PokerAIConfig.BET_WEAK_BASE   + betSizeSeed * PokerAIConfig.BET_WEAK_SEED;
		else
		{
			if (betSizeSeed < PokerAIConfig.BET_BLUFF_SPLIT)
			{
				float t = betSizeSeed / PokerAIConfig.BET_BLUFF_SPLIT;
				baseBetMultiplier = PokerAIConfig.BET_BLUFF_SMALL_BASE + t * PokerAIConfig.BET_BLUFF_SMALL_RANGE;
			}
			else
			{
				float t = (betSizeSeed - PokerAIConfig.BET_BLUFF_SPLIT) / (1f - PokerAIConfig.BET_BLUFF_SPLIT);
				baseBetMultiplier = PokerAIConfig.BET_BLUFF_BIG_BASE + t * PokerAIConfig.BET_BLUFF_BIG_RANGE;
			}
		}

		float streetMultiplier = street switch
		{
			Street.Preflop => PokerAIConfig.PREFLOP_BET_MULTIPLIER,
			Street.Flop    => PokerAIConfig.FLOP_BET_MULTIPLIER,
			Street.Turn    => PokerAIConfig.TURN_BET_MULTIPLIER,
			Street.River   => PokerAIConfig.RIVER_BET_MULTIPLIER,
			_              => 1.0f
		};

		float aggressionMultiplier = PokerAIConfig.BET_AGG_BASE + personality.CurrentAggression * PokerAIConfig.BET_AGG_SCALE;
		if (player.CurrentTiltState >= TiltState.Steaming) aggressionMultiplier *= PokerAIConfig.BET_TILT_MULTIPLIER;

		return baseBetMultiplier * streetMultiplier * aggressionMultiplier;
	}

	public int CalculateRaiseToTotal(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		PokerPersonality personality = player.Personality;
		float effectivePot = Mathf.Max(gameState.PotSize, 1f);
		float currentBet   = gameState.CurrentBet;

		if (!gameState.CanAIReopenBetting)
		{
			GameManager.LogVerbose($"[{player.PlayerName}] Cannot raise - not reopened. Returning currentBet.");
			return (int)currentBet;
		}

		float baseBetRatio = CalculatePlannedBetRatio(handStrength, personality, gameState.Street, player.BetSizeSeed, player);
		float targetTotal  = effectivePot * baseBetRatio;

		if (player.CurrentTiltState >= TiltState.Steaming)
			targetTotal *= PokerAIConfig.ALLIN_TILT_BET_MULTIPLIER;

		int minTotalInt = PokerRules.CalculateMinRaiseTotal(
			(int)currentBet, (int)gameState.PreviousBet,
			gameState.LastFullRaiseIncrement, (int)gameState.BigBlind);

		float minTotal = (float)minTotalInt;
		float maxTotal = gameState.GetPlayerCurrentBet(player) + player.ChipStack;

		GameManager.LogVerbose($"[AI RAISE CALC] LastFullRaiseInc={gameState.LastFullRaiseIncrement}, MinTotal={minTotal}");

		float legalTotal = (maxTotal < minTotal)
			? maxTotal
			: Mathf.Clamp(targetTotal, minTotal, maxTotal);

		// River commitment
		if (gameState.Street == Street.River && handStrength >= 0.60f)
		{
			float spr = player.ChipStack / effectivePot;
			if (spr < PokerAIConfig.RIVER_COMMIT_SPR_THRESHOLD)
			{
				float committedFrac = (legalTotal - gameState.GetPlayerCurrentBet(player)) / Mathf.Max(player.ChipStack, 1f);
				if (committedFrac >= PokerAIConfig.RIVER_COMMIT_FRAC_THRESHOLD)
				{
					bool isStrongHand   = handStrength >= PokerAIConfig.RIVER_STRONG_THRESHOLD;
					bool isMediumCommit = handStrength >= PokerAIConfig.RIVER_MEDIUM_THRESHOLD &&
										 player.AllInCommitmentSeed < PokerAIConfig.RIVER_MEDIUM_SEED_CUTOFF;
					if (isStrongHand || isMediumCommit) legalTotal = maxTotal;
				}
			}
		}

		// Near-all-in commitment
		float amountToAdd = legalTotal - gameState.GetPlayerCurrentBet(player);
		if (amountToAdd >= player.ChipStack * PokerAIConfig.ALLIN_COMMIT_STACK_FRAC)
		{
			if (player.AllInCommitmentSeed < personality.CurrentRiskTolerance || handStrength > 0.80f)
			{
				legalTotal = maxTotal;
			}
			else
			{
				float backedOff = gameState.GetPlayerCurrentBet(player) + player.ChipStack * PokerAIConfig.ALLIN_BACKOFF_STACK_FRAC;
				legalTotal = (maxTotal < minTotal) ? maxTotal : Mathf.Clamp(backedOff, minTotal, maxTotal);
			}
		}

		int finalTotal = Mathf.Clamp((int)Mathf.Floor(legalTotal), 1, (int)maxTotal);
		if (maxTotal >= minTotal) finalTotal = Math.Max(finalTotal, (int)Mathf.Ceil(minTotal));

		GameManager.LogVerbose($"[{player.PlayerName}] Raise-to: {finalTotal} (effPot: {effectivePot:F0}, str: {handStrength:F2}, min: {minTotal}, max: {maxTotal})");
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

		int   myRank       = HandEvaluator.EvaluateHand(holeCards, communityCards);
		float linearNorm   = 1.0f - ((myRank - 1) / PokerAIConfig.HAND_EVAL_MAX_RANK);
		float absStrength  = (float)Math.Pow(linearNorm, PokerAIConfig.HAND_EVAL_POWER_EXPONENT);

		float boardStrength = 0f;
		if (communityCards.Count >= 5)
		{
			int   boardRank   = HandEvaluator.EvaluateHand(new List<Card>(), communityCards);
			float boardLinear = 1.0f - ((boardRank - 1) / PokerAIConfig.HAND_EVAL_MAX_RANK);
			boardStrength     = (float)Math.Pow(boardLinear, PokerAIConfig.HAND_EVAL_POWER_EXPONENT);
		}

		float adjustedStrength = absStrength;

		// Counterfeit detection
		if (communityCards.Count >= 5 && (absStrength - boardStrength < PokerAIConfig.COUNTERFEIT_MARGIN))
			adjustedStrength = PokerAIConfig.COUNTERFEIT_WEAKNESS;

		// Draw potential
		if (street == Street.Flop || street == Street.Turn)
		{
			float drawStrength = EvaluateDrawPotential(holeCards, communityCards, street);
			adjustedStrength   = drawStrength > adjustedStrength
				? drawStrength
				: adjustedStrength + drawStrength * PokerAIConfig.DRAW_BLEND_WEIGHT;
		}

		float randomness = randomnessSeed * PokerAIConfig.RANDOMNESS_SCALE;
		return Mathf.Clamp(adjustedStrength + randomness, PokerAIConfig.STRENGTH_FLOOR, 1.0f);
	}

	private float EvaluatePreflopHand(List<Card> holeCards)
	{
		if (holeCards.Count != 2) return PokerAIConfig.PREFLOP_BASE;

		Card card1 = holeCards[0];
		Card card2 = holeCards[1];

		bool isPair  = card1.Rank == card2.Rank;
		bool isSuited = card1.Suit == card2.Suit;
		int  highCard = Mathf.Max((int)card1.Rank, (int)card2.Rank);
		int  lowCard  = Mathf.Min((int)card1.Rank, (int)card2.Rank);
		int  rankDiff = highCard - lowCard;

		bool isConnector      = rankDiff == 1;
		bool isGapper         = rankDiff == 2;
		bool isWheelConnector = (highCard == 12 && lowCard == 0);
		bool isWheelGapper    = (highCard == 12 && lowCard == 1);

		float strength;

		if (isPair)
		{
			float pairPower = (float)highCard / PokerAIConfig.PREFLOP_ACE_INDEX;
			strength = PokerAIConfig.PREFLOP_PAIR_BASE + pairPower * pairPower * PokerAIConfig.PREFLOP_PAIR_POWER_WEIGHT;
		}
		else
		{
			strength  = PokerAIConfig.PREFLOP_HIGH_CARD_BASE + (highCard / PokerAIConfig.PREFLOP_ACE_INDEX) * PokerAIConfig.PREFLOP_HIGH_CARD_WEIGHT;
			strength += (lowCard / PokerAIConfig.PREFLOP_ACE_INDEX) * PokerAIConfig.PREFLOP_KICKER_WEIGHT;

			if (isSuited)                        strength += PokerAIConfig.PREFLOP_SUITED_BONUS;
			if (isConnector || isWheelConnector) strength += PokerAIConfig.PREFLOP_CONNECTOR_BONUS;
			if (isGapper    || isWheelGapper)    strength += PokerAIConfig.PREFLOP_GAPPER_BONUS;

			bool isTrash = highCard < PokerAIConfig.PREFLOP_TRASH_RANK_CUTOFF
						   && !isSuited && !isConnector && !isGapper
						   && !isWheelConnector && !isWheelGapper;
			if (isTrash) strength -= PokerAIConfig.PREFLOP_TRASH_PENALTY;
		}

		return Mathf.Clamp(strength, PokerAIConfig.PREFLOP_STRENGTH_FLOOR, PokerAIConfig.PREFLOP_STRENGTH_CEIL);
	}

	private float EvaluateDrawPotential(List<Card> holeCards, List<Card> communityCards, Street street)
	{
		if (street == Street.River) return 0f;

		List<Card> allCards = new List<Card>(holeCards);
		allCards.AddRange(communityCards);

		var holeSuits = new HashSet<Suit>(holeCards.Select(c => c.Suit));
		var holeRanks = new HashSet<int>(holeCards.Select(c => (int)c.Rank));

		// Flush draws — materialize counts to avoid double enumeration
		var suitCounts = allCards
			.GroupBy(c => c.Suit)
			.Where(g => holeSuits.Contains(g.Key))
			.Select(g => g.Count())
			.ToList();
		bool flushDraw     = suitCounts.Any(c => c >= 4);
		bool backdoorFlush = suitCounts.Any(c => c == 3);

		var ranks   = allCards.Select(c => (int)c.Rank).OrderBy(r => r).Distinct().ToList();
		var rankSet = new HashSet<int>(ranks);
		bool oesd = false, gutshot = false;
		
		GameManager.LogVerbose($"[DRAW DEBUG] Sorted ranks: [{string.Join(",", ranks)}]");

		// Single pass: 4-card window (OESD / single gutshot) + 5-card window (double gutshot)
		// NOTE: True double gutshots (e.g. 5-7-9-J, span=6) are not detected by the 5-card
		// window below — they require a span-6 check not currently implemented. Those hands
		// score 0 draw strength, which is acceptable for this game's purposes.
		for (int i = 0; i < ranks.Count && !(oesd && gutshot); i++)
		{
			// 4-card window
			if (i + 3 < ranks.Count)
			{
				int span4 = ranks[i + 3] - ranks[i];

				if (span4 == 3) // 4 consecutive = OESD (broadway/wheel edges demoted to gutshot)
				{
					if (Enumerable.Range(ranks[i], 4).Any(r => holeRanks.Contains(r)))
						(ranks[i + 3] == 12 || ranks[i] == 0 ? ref gutshot : ref oesd) = true;
				}
				else if (span4 == 4) // one internal gap = single gutshot
				{
					if (Enumerable.Range(0, 4).Any(j => holeRanks.Contains(ranks[i + j])))
						gutshot = true;
				}
			}

			// 5-card window: catches cases like A-2-3-4-6 (missing 5) where span==4 over 5 slots
			if (i + 4 < ranks.Count)
			{
				int span5 = ranks[i + 4] - ranks[i];
				if (span5 == 4)
				{
					int presentCount = Enumerable.Range(ranks[i], 5).Count(r => rankSet.Contains(r));
					if (presentCount == 4 && Enumerable.Range(ranks[i], 5).Any(r => holeRanks.Contains(r)))
						gutshot = true;
				}
			}
		}

		// Wheel edge case (A-2-3-4)
		if (rankSet.Contains(12))
		{
			var wheelRanks = ranks.Where(r => r <= 3).ToList();
			if (wheelRanks.Count >= 3 && (holeRanks.Contains(12) || wheelRanks.Any(r => holeRanks.Contains(r))))
				gutshot = true;
		}
		
		GameManager.LogVerbose($"[DRAW DEBUG] Result: oesd={oesd} gutshot={gutshot} flushDraw={flushDraw} backdoor={backdoorFlush}");

		if (flushDraw && oesd)    return PokerAIConfig.DRAW_FLUSH_OESD;
		if (flushDraw && gutshot) return PokerAIConfig.DRAW_FLUSH_GUTSHOT;
		if (flushDraw)            return PokerAIConfig.DRAW_FLUSH;
		if (oesd)                 return PokerAIConfig.DRAW_OESD;
		if (gutshot)              return PokerAIConfig.DRAW_GUTSHOT;
		if (street == Street.Flop && backdoorFlush) return PokerAIConfig.DRAW_BACKDOOR;

		return 0f;
	}
}
