// File: OpponentProfiles.cs
using Godot;
using System;

public static class OpponentProfiles
{
	public static OpponentProfile[] CircuitAOpponents() => new[]
	{
		// Opponent 1: Steve - Calling station
		new OpponentProfile
		{
			Name = "Steve",
			BuyIn = 25,
			Looseness = 0.65f,           // Plays too many hands
			Aggressiveness = 0.30f,      // Very passive
			CallingStationRate = 0.40f,  // Refuses to fold 40% of time
			BadBluffRate = 0.08f,        // Rarely bluffs
			OverfoldRate = 0.12f,        // Sometimes folds medium
			MissedValueRate = 0.30f      // Often checks strong hands
		},

		// Opponent 2: Aryll - Loose passive (calls everything)
		new OpponentProfile
		{
			Name = "Aryll",
			BuyIn = 50,
			Looseness = 0.75f,           // Plays almost everything
			Aggressiveness = 0.35f,      // Passive
			CallingStationRate = 0.50f,  // Even more stubborn than Steve
			BadBluffRate = 0.05f,        // Almost never bluffs
			OverfoldRate = 0.08f,        // Rarely folds
			MissedValueRate = 0.35f      // Very passive with strong hands
		},

		// Opponent 3: Boy Wizard - Maniac (bluffs constantly)
		new OpponentProfile
		{
			Name = "Boy Wizard",
			BuyIn = 100,
			Looseness = 0.85f,           // Plays everything
			Aggressiveness = 0.80f,      // Very aggressive
			CallingStationRate = 0.15f,  // Not a calling station
			BadBluffRate = 0.35f,        // Bluffs way too much!
			OverfoldRate = 0.05f,        // Never folds
			MissedValueRate = 0.10f      // Usually bets his strong hands
		}
	};
	
	public static OpponentProfile[] CircuitBOpponents() => new[]
	{
		// Opponent 4: Cowboy - Tight-aggressive
		new OpponentProfile
		{
			Name = "Cowboy",
			BuyIn = 200,
			Looseness = 0.40f,           // Tight hand selection
			Aggressiveness = 0.65f,      // Aggressive when he plays
			CallingStationRate = 0.12f,
			BadBluffRate = 0.18f,        // Strategic bluffs
			OverfoldRate = 0.20f,        // Folds medium hands
			MissedValueRate = 0.15f
		},

		// Opponent 5: Hippie - Balanced but passive
		new OpponentProfile
		{
			Name = "Hippie",
			BuyIn = 350,
			Looseness = 0.50f,           // Balanced
			Aggressiveness = 0.40f,      // Slightly passive
			CallingStationRate = 0.20f,
			BadBluffRate = 0.15f,
			OverfoldRate = 0.15f,
			MissedValueRate = 0.25f      // Too passive postflop
		},

		// Opponent 6: Rumi - Solid player
		new OpponentProfile
		{
			Name = "Rumi",
			BuyIn = 500,
			Looseness = 0.52f,
			Aggressiveness = 0.55f,
			CallingStationRate = 0.12f,
			BadBluffRate = 0.15f,
			OverfoldRate = 0.10f,
			MissedValueRate = 0.15f
		}
	};
	
	public static OpponentProfile[] CircuitCOpponents() => new[]
	{
		// Opponent 7: King - Strong aggressive player
		new OpponentProfile
		{
			Name = "King",
			BuyIn = 750,
			Looseness = 0.55f,
			Aggressiveness = 0.70f,
			CallingStationRate = 0.08f,
			BadBluffRate = 0.12f,
			OverfoldRate = 0.08f,
			MissedValueRate = 0.10f
		},

		// Opponent 8: Old Wizard - Exploitative expert
		new OpponentProfile
		{
			Name = "Old Wizard",
			BuyIn = 1500,
			Looseness = 0.50f,
			Aggressiveness = 0.60f,
			CallingStationRate = 0.05f,
			BadBluffRate = 0.10f,
			OverfoldRate = 0.05f,
			MissedValueRate = 0.08f
		},

		// Opponent 9: Spade - Near-perfect GTO
		new OpponentProfile
		{
			Name = "Spade",
			BuyIn = 2000,
			Looseness = 0.50f,           // Balanced
			Aggressiveness = 0.50f,      // Balanced
			CallingStationRate = 0.03f,  // Almost no mistakes
			BadBluffRate = 0.08f,        // Optimal bluff frequency
			OverfoldRate = 0.02f,
			MissedValueRate = 0.03f
		}
	};
}
