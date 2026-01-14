public static class PersonalityPresets
{
	public static PokerPersonality CreateSteve()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Steve",
			BaseAggression = 0.45f,
			BaseBluffFrequency = 0.35f,
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
			BaseBluffFrequency = 0.15f,
			BaseFoldThreshold = 0.40f,
			BaseRiskTolerance = 0.35f,
			TiltSensitivity = 0.50f,
			CallTendency = 0.75f
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
			CallTendency = 0.40f
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
}
