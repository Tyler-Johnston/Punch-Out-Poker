public static class OpponentProfiles
{
	public static OpponentProfile[] CircuitAOpponents() => new[]
	{
		// Opponent 1
		new OpponentProfile
		{
			Name = "Steve",
			BuyIn = 25,
			Aggression = 0.25f,      // Very passive
			Looseness = 0.4f,       // Tight hand selection
			Bluffiness = 0.15f,      // Never bluffs
			Adaptability = 0.15f,    // Doesn't adjust
			PreflopAggression = 0.8f,  // Even more passive preflop
			PostflopAggression = 1.0f,
			MistakeFactor = 1.55f
		},

		// Opponent 2
		new OpponentProfile
		{
			Name = "Aryll",
			BuyIn = 50,
			Aggression = 0.3f,      // Passive
			Looseness = 0.7f,       // Plays lots of hands
			Bluffiness = 0.20f,      // Rarely bluffs
			Adaptability = 0.2f,    // Slight adjustment
			PreflopAggression = 1.2f,  // Slightly more aggressive preflop
			PostflopAggression = 0.8f,  // Passive postflop (calls too much)
			MistakeFactor = 1.52f
		},

		// Opponent 3
		new OpponentProfile
		{
			Name = "Boy Wizard",
			BuyIn = 100,
			Aggression = 0.85f,     // Very aggressive
			Looseness = 0.8f,       // Plays almost any hand
			Bluffiness = 0.7f,      // Bluffs frequentlyer g
			Adaptability = 0.4f,    // Adjusts somewhat
			PreflopAggression = 1.3f,  // Extra aggressive preflop
			PostflopAggression = 1.1f,  // Extra aggressive postflop
			MistakeFactor = 1.51f
		}
	};
	
	public static OpponentProfile[] CircuitBOpponents() => new[]
	{
		// Opponent 4
		new OpponentProfile
		{
			Name = "Cowboy",
			BuyIn = 200,
			Aggression = 0.55f,        // Selective aggression
			Looseness = 0.35f,         // Tight hand selection
			Bluffiness = 0.30f,        // Occasional bluffs
			Adaptability = 0.50f,      // Moderate adjustment
			PreflopAggression = 1.3f,  // Aggressive with good hands
			PostflopAggression = 1.2f, // Continues aggression
			MistakeFactor = 1.45f
		},

		// Opponent 5
		new OpponentProfile
		{
			Name = "Hippie",
			BuyIn = 350,
			Aggression = 0.45f,        // Balanced aggression
			Looseness = 0.40f,         // Moderately tight
			Bluffiness = 0.25f,        // Occasional bluffs
			Adaptability = 0.60f,      // Good adjustment
			PreflopAggression = 1.1f,  // Standard preflop
			PostflopAggression = 1.0f, // Standard postflop (slightly weak)
			MistakeFactor = 1.42f
		},

		// Opponent 6 
		new OpponentProfile
		{
			Name = "Rumi",
			BuyIn = 500,
			Aggression = 0.50f,        // Balanced aggression
			Looseness = 0.45f,         // Balanced range
			Bluffiness = 0.35f,        // Strategic bluffs
			Adaptability = 0.70f,      // Strong adjustment
			PreflopAggression = 1.15f, // Slightly aggressive preflop
			PostflopAggression = 1.05f, // Slightly aggressive postflop
			MistakeFactor = 1.39f
		}
	};
	
	public static OpponentProfile[] CircuitCOpponents() => new[]
	{
		// Opponent 7
		new OpponentProfile
		{
			Name = "King",
			BuyIn = 750,
			Aggression = 0.65f,        // Strong aggression
			Looseness = 0.50f,         // Balanced range
			Bluffiness = 0.45f,        // Frequent strategic bluffs
			Adaptability = 0.80f,      // Excellent adjustment
			PreflopAggression = 1.25f, // Strong preflop pressure
			PostflopAggression = 1.20f, // Maintains aggression postflop
			MistakeFactor = 1.36f
		},

		// Opponent 8
		new OpponentProfile
		{
			Name = "Old Wizard",
			BuyIn = 1500,
			Aggression = 0.55f,        // Calculated aggression
			Looseness = 0.42f,         // Tight-balanced
			Bluffiness = 0.40f,        // Well-timed bluffs
			Adaptability = 0.90f,      // Near-perfect adjustment
			PreflopAggression = 1.20f, // Positionally aware
			PostflopAggression = 1.25f, // Exploits postflop edges
			MistakeFactor = 1.33f
		},

		// Opponent 9 (Final Boss)
		new OpponentProfile
		{
			Name = "Spade",
			BuyIn = 2000,
			Aggression = 0.60f,        // Solver-based aggression
			Looseness = 0.48f,         // Mathematically balanced
			Bluffiness = 0.50f,        // Optimal bluff frequency
			Adaptability = 0.95f,      // Nearly unexploitable
			PreflopAggression = 1.22f, // Frequency-based ranges
			PostflopAggression = 1.22f, // Balanced across streets
			MistakeFactor = 1.30f
		}
	};
}
