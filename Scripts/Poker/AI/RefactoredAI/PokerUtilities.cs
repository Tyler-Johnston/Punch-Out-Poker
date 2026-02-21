using Godot;
using System.Collections.Generic;

public static class PokerUtilities
{
	public static HandRank ConvertPhevalRankToHandRank(int phevalRank)
	{
		if (phevalRank == 1) return HandRank.RoyalFlush;
		if (phevalRank <= 10) return HandRank.StraightFlush;
		if (phevalRank <= 166) return HandRank.FourOfAKind;
		if (phevalRank <= 322) return HandRank.FullHouse;
		if (phevalRank <= 1599) return HandRank.Flush;
		if (phevalRank <= 1609) return HandRank.Straight;
		if (phevalRank <= 2467) return HandRank.ThreeOfAKind;
		if (phevalRank <= 3325) return HandRank.TwoPair;
		if (phevalRank <= 6185) return HandRank.OnePair;
		return HandRank.HighCard;
	}
}

public static class PokerAIConfig
{
	public const float FLOP_BASE_THRESHOLD = 0.32f;    // Was 0.38
	public const float TURN_BASE_THRESHOLD = 0.40f;    // Was 0.48
	public const float RIVER_BASE_THRESHOLD = 0.46f;   // Was 0.54
	public const float PREFLOP_BASE_THRESHOLD = 0.36f; // Was 0.42 we did
	
	public const float FLOP_VALUE_THRESHOLD = 0.55f;
	public const float TURN_VALUE_THRESHOLD = 0.58f;
	public const float RIVER_VALUE_THRESHOLD = 0.62f;
	
	public const float FLOP_BLUFF_CEILING = 0.48f;
	public const float TURN_BLUFF_CEILING = 0.45f;
	public const float RIVER_BLUFF_CEILING = 0.42f;
	
	public const float FLOP_SIZE_BUMP = 0.025f;
	public const float LATER_STREET_SIZE_BUMP = 0.045f;

	public const float POT_ODDS_MULTIPLIER = 1.25f;
	public const float POT_ODDS_OVERRIDE_THRESHOLD = 0.40f;
	
	public const float OOP_VALUE_TIGHTEN = 0.03f;
	public const float OOP_BLUFF_REDUCE = 0.03f;
	
	public const float PREFLOP_BET_MULTIPLIER = 1.0f;
	public const float FLOP_BET_MULTIPLIER = 1.12f;
	public const float TURN_BET_MULTIPLIER = 1.10f;
	public const float RIVER_BET_MULTIPLIER = 1.15f;
	
	public const float SIZE_FACTOR_VALUE_ADJUST = 0.03f;
	public const float SIZE_FACTOR_BLUFF_ADJUST = 0.04f;
	
	public const float BLUFF_BASE_PROB = 0.55f;
	public const float BLUFF_AGGRESSION_WEIGHT = 0.35f;
	
	public const float TRAP_PROBABILITY = 0.08f;
	
	public const float VALUE_BET_BASE_FREQ = 0.70f;
	public const float VALUE_BET_AGGRESSION_WEIGHT = 0.20f;
	
		// --- HAND EVALUATION ---
	public const float HAND_EVAL_MAX_RANK = 7461.0f;
	public const float HAND_EVAL_POWER_EXPONENT = 0.8f;
	public const float COUNTERFEIT_MARGIN = 0.05f;
	public const float COUNTERFEIT_WEAKNESS = 0.25f;
	public const float DRAW_BLEND_WEIGHT = 0.20f;
	public const float RANDOMNESS_SCALE = 0.05f;
	public const float STRENGTH_FLOOR = 0.05f;

	// --- PREFLOP EVALUATION ---
	public const float PREFLOP_BASE = 0.20f;
	public const float PREFLOP_HIGH_CARD_BASE = 0.30f;
	public const float PREFLOP_HIGH_CARD_WEIGHT = 0.35f;
	public const float PREFLOP_KICKER_WEIGHT = 0.10f;
	public const float PREFLOP_SUITED_BONUS = 0.04f;
	public const float PREFLOP_CONNECTOR_BONUS = 0.03f;
	public const float PREFLOP_GAPPER_BONUS = 0.015f;
	public const float PREFLOP_TRASH_PENALTY = 0.10f;
	public const int   PREFLOP_TRASH_RANK_CUTOFF = 10;
	public const float PREFLOP_PAIR_BASE = 0.50f;
	public const float PREFLOP_PAIR_POWER_WEIGHT = 0.45f;
	public const float PREFLOP_ACE_INDEX = 12.0f;
	public const float PREFLOP_STRENGTH_FLOOR = 0.15f;
	public const float PREFLOP_STRENGTH_CEIL = 0.98f;

	// --- DRAW EVALUATION ---
	public const float DRAW_FLUSH_OESD    = 0.55f;
	public const float DRAW_FLUSH_GUTSHOT = 0.45f;
	public const float DRAW_FLUSH         = 0.35f;
	public const float DRAW_OESD          = 0.30f;
	public const float DRAW_GUTSHOT       = 0.15f;
	public const float DRAW_BACKDOOR      = 0.05f;

	// --- ALL-IN THRESHOLDS ---
	public const float ALLIN_PREFLOP_BASE  = 0.52f;
	public const float ALLIN_FLOP_BASE     = 0.62f;
	public const float ALLIN_TURN_BASE     = 0.68f;
	public const float ALLIN_RIVER_BASE    = 0.72f;
	public const float ALLIN_RISK_TOL_SCALE = 0.20f;
	public const float ALLIN_PREFLOP_STACK_GUARD = 0.60f; // fold threshold when stack > 15BB
	public const float ALLIN_PREFLOP_STACK_GUARD_MIN_STRENGTH = 0.60f;
	public const int   ALLIN_STACK_GUARD_BB_MULTIPLIER = 15;
	public const float ALLIN_BLUFF_SHOVE_PREFLOP  = 0.25f;
	public const float ALLIN_BLUFF_SHOVE_FLOP     = 0.20f;
	public const float ALLIN_BLUFF_SHOVE_LATER    = 0.10f;
	public const float ALLIN_BLUFF_TILT_MULTIPLIER = 2.0f;
	public const float ALLIN_BLUFF_MAX_STRENGTH   = 0.25f;
	public const float ALLIN_SMALL_BET_RATIO      = 0.50f; // treat as normal call if below this

	// --- CALL / RAISE THRESHOLDS ---
	public const float RAISE_FLOP_BASE   = 0.62f;
	public const float RAISE_TURN_BASE   = 0.67f;
	public const float RAISE_RIVER_BASE  = 0.72f;
	public const float RAISE_PREFLOP_BASE = 0.60f;
	public const float RAISE_AGGRESSION_SCALE = 0.15f;
	public const float RAISE_MIN_STACK_MULTIPLIER = 2.5f;
	public const float RAISE_PROB_BASE   = 0.20f;
	public const float RAISE_PROB_AGG_WEIGHT = 0.80f;
	public const float BLUFF_RAISE_FLOP_SCALE  = 0.35f;
	public const float BLUFF_RAISE_LATER_SCALE = 0.20f;
	public const float BLUFF_RAISE_TILT_MULTIPLIER = 1.5f;
	public const float BLUFF_RAISE_MAX_STRENGTH = 0.32f;
	public const float BLUFF_RAISE_MIN_STACK_MULTIPLIER = 3.0f;

	// --- OPPONENT MODELING ---
	public const float OPPONENT_MANIAC_RAISE_FREQ  = 0.40f;
	public const float OPPONENT_MANIAC_AGG_FACTOR  = 2.0f;
	public const float OPPONENT_MANIAC_ADJUST      = -0.12f;
	public const float OPPONENT_PASSIVE_RAISE_FREQ = 0.15f;
	public const float OPPONENT_PASSIVE_ADJUST     = +0.08f;

	// --- CALL/FOLD OVERBET RULE ---
	public const float OVERBET_RATIO_THRESHOLD = 2.5f;
	public const float OVERBET_BASE_THRESHOLD  = 0.58f;
	public const float OVERBET_RISK_MIN        = 0.08f;
	public const float OVERBET_RISK_MAX        = 0.18f;
	public const float OVERBET_BLUFF_DISCOUNT  = 0.04f;
	public const float LARGE_BET_RATIO         = 0.80f;
	public const float SMALL_BET_RATIO         = 0.33f;
	public const float MID_BET_RATIO           = 0.55f;

	// --- PLANNED BET RATIO BREAKPOINTS ---
	public const float BET_STRONG_BASE    = 0.85f;
	public const float BET_GOOD_BASE      = 0.65f;
	public const float BET_MEDIUM_BASE    = 0.50f;
	public const float BET_WEAK_BASE      = 0.35f;
	public const float BET_BLUFF_SMALL_BASE = 0.35f;
	public const float BET_BLUFF_BIG_BASE   = 1.20f;
	public const float BET_BLUFF_SPLIT      = 0.60f;
	public const float BET_AGG_BASE         = 0.90f;
	public const float BET_TILT_MULTIPLIER  = 1.25f;

	public const float BET_STRONG_SEED    = 0.35f; // hs >= 0.80
	public const float BET_GOOD_SEED      = 0.30f; // hs >= 0.65
	public const float BET_MEDIUM_SEED    = 0.25f; // hs >= 0.45
	public const float BET_WEAK_SEED      = 0.25f; // hs >= 0.35
	public const float BET_BLUFF_SMALL_RANGE = 0.20f;
	public const float BET_BLUFF_BIG_RANGE  = 0.40f;
	public const float BET_AGG_SCALE = 0.30f;

	// --- RIVER COMMITMENT ---
	public const float RIVER_COMMIT_SPR_THRESHOLD  = 1.0f;
	public const float RIVER_COMMIT_FRAC_THRESHOLD = 0.60f;
	public const float RIVER_STRONG_THRESHOLD      = 0.75f;
	public const float RIVER_MEDIUM_THRESHOLD      = 0.62f;
	public const float RIVER_MEDIUM_SEED_CUTOFF    = 0.45f;
	public const float ALLIN_COMMIT_STACK_FRAC     = 0.90f;
	public const float ALLIN_BACKOFF_STACK_FRAC    = 0.60f;
	public const float ALLIN_TILT_BET_MULTIPLIER   = 1.15f;
}
