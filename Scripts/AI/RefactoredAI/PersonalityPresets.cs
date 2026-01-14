public static class PersonalityPresets
{
	// ========== CIRCUIT A: Beginner Opponents ==========
	
	public static PokerPersonality CreateSteve()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Steve",
			BaseAggression = 0.45f,
			BaseBluffFrequency = 0.33f,
			BaseFoldThreshold = 0.55f,
			BaseRiskTolerance = 0.60f,
			TiltSensitivity = 0.20f,
			CallTendency = 0.50f
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "relaxed_posture", "casual_bet" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "checks_cards_again", "hesitant" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "nervous_laugh", "quick_decision" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	public static PokerPersonality CreateAryll()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Aryll",
			BaseAggression = 0.30f,
			BaseBluffFrequency = 0.25f,
			BaseFoldThreshold = 0.40f,
			BaseRiskTolerance = 0.35f,
			TiltSensitivity = 0.50f,
			CallTendency = 0.75f  // Calling station - plays too many hands
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "excited_expression", "confident_smile" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "uncertain_look", "bites_lip" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "avoids_eye_contact", "fidgets" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	public static PokerPersonality CreateBoyWizard()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Boy Wizard",
			BaseAggression = 0.85f,
			BaseBluffFrequency = 0.70f,
			BaseFoldThreshold = 0.65f,
			BaseRiskTolerance = 0.80f,
			TiltSensitivity = 0.60f,
			CallTendency = 0.40f  // Low - folds or raises, rarely calls
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "smirks", "pushes_chips_forward_confidently" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "taps_fingers", "looks_away" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "overconfident_speech", "aggressive_posture" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	// ========== CIRCUIT B: Intermediate Opponents ==========
	
	public static PokerPersonality CreateCowboy()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Cowboy",
			BaseAggression = 0.55f,        // Selective aggression
			BaseBluffFrequency = 0.30f,    // Occasional strategic bluffs
			BaseFoldThreshold = 0.50f,     // Tighter - folds marginal hands
			BaseRiskTolerance = 0.55f,     // Moderate risk-taking
			TiltSensitivity = 0.40f,       // Moderately tilts
			CallTendency = 0.45f           // Prefers raising over calling
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "tips_hat", "leans_back_confidently" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "adjusts_hat", "shifts_weight" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "stone_faced", "deliberate_bet" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	public static PokerPersonality CreateHippie()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Hippie",
			BaseAggression = 0.45f,        // Balanced, peaceful play
			BaseBluffFrequency = 0.25f,    // Occasional bluffs
			BaseFoldThreshold = 0.48f,     // Moderately tight
			BaseRiskTolerance = 0.50f,     // Balanced risk
			TiltSensitivity = 0.25f,       // Hard to tilt (zen mindset)
			CallTendency = 0.55f           // Prefers calling to aggression
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "peaceful_smile", "relaxed_breathing" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "sighs", "scratches_beard" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "forced_calmness", "closes_eyes_briefly" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	public static PokerPersonality CreateRumi()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Rumi",
			BaseAggression = 0.50f,        // Balanced aggression
			BaseBluffFrequency = 0.35f,    // Strategic bluffs
			BaseFoldThreshold = 0.47f,     // Balanced range
			BaseRiskTolerance = 0.52f,     // Slightly risk-tolerant
			TiltSensitivity = 0.35f,       // Moderate tilt resistance
			CallTendency = 0.50f           // Perfectly balanced
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "steady_gaze", "controlled_breathing" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "glances_at_chips", "subtle_frown" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "maintains_composure", "measured_speech" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	// ========== CIRCUIT C: Expert Opponents ==========
	
	public static PokerPersonality CreateKing()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "King",
			BaseAggression = 0.65f,        // Strong aggression
			BaseBluffFrequency = 0.45f,    // Frequent strategic bluffs
			BaseFoldThreshold = 0.52f,     // Disciplined folding
			BaseRiskTolerance = 0.60f,     // Calculated risks
			TiltSensitivity = 0.30f,       // Good emotional control
			CallTendency = 0.42f           // Aggressive - raises more than calls
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "regal_posture", "dismissive_gesture" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "fingers_drumming", "glances_at_exit" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "authoritative_tone", "commanding_stare" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	public static PokerPersonality CreateOldWizard()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Old Wizard",
			BaseAggression = 0.55f,        // Calculated aggression
			BaseBluffFrequency = 0.40f,    // Well-timed bluffs
			BaseFoldThreshold = 0.45f,     // Tight-aggressive
			BaseRiskTolerance = 0.58f,     // Exploits edges
			TiltSensitivity = 0.15f,       // Rarely tilts (wisdom)
			CallTendency = 0.48f           // Balanced, slight aggression preference
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "knowing_smile", "strokes_beard_confidently" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "contemplative_pause", "adjusts_spectacles" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "mysterious_chuckle", "cryptic_comment" };
		
		personality.ResetCurrentStats();
		return personality;
	}
	
	public static PokerPersonality CreateSpade()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Spade",
			BaseAggression = 0.60f,        // Solver-based balanced aggression
			BaseBluffFrequency = 0.50f,    // Optimal bluff frequency
			BaseFoldThreshold = 0.46f,     // Mathematically sound
			BaseRiskTolerance = 0.55f,     // Calculated risk-taking
			TiltSensitivity = 0.10f,       // Nearly impossible to tilt (final boss)
			CallTendency = 0.48f           // GTO-balanced calling frequency
		};
		
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> 
			{ "emotionless_stare", "precise_chip_placement" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> 
			{ "micro_hesitation", "calculated_pause" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> 
			{ "perfect_timing", "unwavering_focus" };
		
		personality.ResetCurrentStats();
		return personality;
	}
}
