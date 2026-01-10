public static class OpponentProfiles
{
	public static OpponentProfile[] CircuitAOpponents() => new[]
	{
		// Opponent 1
		new OpponentProfile
		{
			Name = "Bro. Goldn",
			BuyIn = 50,
			Aggression = 0.25f,      // Very passive
			Looseness = 0.4f,       // Tight hand selection
			Bluffiness = 0.15f,      // Never bluffs
			Adaptability = 0.15f,    // Doesn't adjust
			PreflopAggression = 0.8f,  // Even more passive preflop
			PostflopAggression = 1.0f,
			MistakeFactor = 1.45f
		},

		// Opponent 2
		new OpponentProfile
		{
			Name = "Aryll",
			BuyIn = 150,
			Aggression = 0.3f,      // Passive
			Looseness = 0.7f,       // Plays lots of hands
			Bluffiness = 0.20f,      // Rarely bluffs
			Adaptability = 0.2f,    // Slight adjustment
			PreflopAggression = 1.2f,  // Slightly more aggressive preflop
			PostflopAggression = 0.8f,  // Passive postflop (calls too much)
			MistakeFactor = 1.40f
		},

		// Opponent 3
		new OpponentProfile
		{
			Name = "Boy Wizard",
			BuyIn = 500,
			Aggression = 0.85f,     // Very aggressive
			Looseness = 0.8f,       // Plays almost any hand
			Bluffiness = 0.7f,      // Bluffs frequently
			Adaptability = 0.4f,    // Adjusts somewhat
			PreflopAggression = 1.3f,  // Extra aggressive preflop
			PostflopAggression = 1.1f,  // Extra aggressive postflop
			MistakeFactor = 1.35f
		}
	};
	
	public static OpponentProfile[] CircuitBOpponents() => new[]
	{
		// Opponent 4
		new OpponentProfile
		{
			Name = "Robo",
			BuyIn = 1000,
			Aggression = 0.55f,        // Selective aggression
			Looseness = 0.35f,         // Tight hand selection
			Bluffiness = 0.30f,        // Occasional bluffs
			Adaptability = 0.50f,      // Moderate adjustment
			PreflopAggression = 1.3f,  // Aggressive with good hands
			PostflopAggression = 1.2f, // Continues aggression
			MistakeFactor = 1.30f
		},

		// Opponent 5 - Viktor "The Grinder" (Weak Regular)
		new OpponentProfile
		{
			Name = "Old Wizard",
			BuyIn = 2500,
			Aggression = 0.45f,        // Balanced aggression
			Looseness = 0.40f,         // Moderately tight
			Bluffiness = 0.25f,        // Occasional bluffs
			Adaptability = 0.60f,      // Good adjustment
			PreflopAggression = 1.1f,  // Standard preflop
			PostflopAggression = 1.0f, // Standard postflop (slightly weak)
			MistakeFactor = 1.25f
		},

		// Opponent 6 - Duchess Amelia (Strong Regular)
		new OpponentProfile
		{
			Name = "Rumi",
			BuyIn = 5000,
			Aggression = 0.50f,        // Balanced aggression
			Looseness = 0.45f,         // Balanced range
			Bluffiness = 0.35f,        // Strategic bluffs
			Adaptability = 0.70f,      // Strong adjustment
			PreflopAggression = 1.15f, // Slightly aggressive preflop
			PostflopAggression = 1.05f, // Slightly aggressive postflop
			MistakeFactor = 1.20f
		}
	};

	
}
