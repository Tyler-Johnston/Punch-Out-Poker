public static class OpponentProfiles
{
	 public static OpponentProfile[] CircuitAOpponents() => new[]
	{
		// Opponent 1. Passive, scared money
		new OpponentProfile
		{
			Name = "Vidro Boy",
			BuyIn = 50,
			Aggression = 0.25f,      // Very passive
			Looseness = 0.4f,       // Tight hand selection
			Bluffiness = 0.15f,      // Never bluffs
			Adaptability = 0.15f,    // Doesn't adjust
			PreflopAggression = 0.8f,  // Even more passive preflop
			PostflopAggression = 1.0f
		},

		// Opponent 2: "Calling Carl" - Passive calling station
		new OpponentProfile
		{
			Name = "Calling Carl",
			BuyIn = 150,
			Aggression = 0.3f,      // Passive
			Looseness = 0.7f,       // Plays lots of hands
			Bluffiness = 0.20f,      // Rarely bluffs
			Adaptability = 0.2f,    // Slight adjustment
			PreflopAggression = 1.2f,  // Slightly more aggressive preflop
			PostflopAggression = 0.8f  // Passive postflop (calls too much)
		},

		// Opponent 3: "Wild Willie" - Aggressive maniac
		new OpponentProfile
		{
			Name = "Wild Willie",
			BuyIn = 500,
			Aggression = 0.85f,     // Very aggressive
			Looseness = 0.8f,       // Plays almost any hand
			Bluffiness = 0.7f,      // Bluffs frequently
			Adaptability = 0.4f,    // Adjusts somewhat
			PreflopAggression = 1.3f,  // Extra aggressive preflop
			PostflopAggression = 1.1f  // Extra aggressive postflop
		}
	};
	
}
