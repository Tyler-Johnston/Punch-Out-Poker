// File: ActionWeightTables.cs
using Godot;
using System;
using System.Collections.Generic;

public static class ActionWeightTables
{
	// Get base strategy weights for a situation
	public static ActionWeights GetBaseWeights(
		HandStrength strength,
		Street street,
		bool facingBet,
		float potOdds,
		float spr)
	{
		if (facingBet)
			return GetDefensiveWeights(strength, street, potOdds, spr);
		else
			return GetAggressiveWeights(strength, street, spr);
	}
	
	// When facing a bet
	private static ActionWeights GetDefensiveWeights(HandStrength strength, Street street, float potOdds, float spr)
	{
		// PREFLOP
		if (street == Street.Preflop)
		{
			switch (strength)
			{
				case HandStrength.Nuts:    // AA, KK, AK
					return new ActionWeights(0.00f, 0.20f, 0.80f); // Mostly raise
				case HandStrength.Strong:  // QQ, JJ, AQ
					return new ActionWeights(0.05f, 0.45f, 0.50f); // Raise or call
				case HandStrength.Medium:  // Small pairs, suited connectors
					if (potOdds < 0.30f) // Good price
						return new ActionWeights(0.25f, 0.65f, 0.10f);
					else
						return new ActionWeights(0.60f, 0.35f, 0.05f);
				case HandStrength.Weak:    // Weak aces, random Broadway
					if (potOdds < 0.25f)
						return new ActionWeights(0.40f, 0.55f, 0.05f);
					else
						return new ActionWeights(0.75f, 0.23f, 0.02f);
				case HandStrength.Trash:   // 72o, 93o
					return new ActionWeights(0.90f, 0.08f, 0.02f);
			}
		}
		
		// FLOP
		if (street == Street.Flop)
		{
			switch (strength)
			{
				case HandStrength.Nuts:
					return new ActionWeights(0.00f, 0.30f, 0.70f); // Raise/slowplay
				case HandStrength.Strong:
					return new ActionWeights(0.05f, 0.60f, 0.35f); // Mostly call
				case HandStrength.Medium:
					if (potOdds < 0.35f && spr < 3.0f) // Good price, committed
						return new ActionWeights(0.30f, 0.65f, 0.05f);
					else if (potOdds < 0.35f)
						return new ActionWeights(0.40f, 0.55f, 0.05f);
					else
						return new ActionWeights(0.65f, 0.30f, 0.05f);
				case HandStrength.Weak:    // Draws, weak pairs
					if (potOdds < 0.30f) // Great price for draw
						return new ActionWeights(0.35f, 0.60f, 0.05f);
					else if (potOdds < 0.40f)
						return new ActionWeights(0.55f, 0.40f, 0.05f);
					else
						return new ActionWeights(0.75f, 0.20f, 0.05f);
				case HandStrength.Trash:
					return new ActionWeights(0.85f, 0.10f, 0.05f);
			}
		}
		
		// TURN
		if (street == Street.Turn)
		{
			switch (strength)
			{
				case HandStrength.Nuts:
					return new ActionWeights(0.00f, 0.25f, 0.75f);
				case HandStrength.Strong:
					return new ActionWeights(0.05f, 0.70f, 0.25f);
				case HandStrength.Medium:
					if (potOdds < 0.30f && spr < 2.0f) // Very committed
						return new ActionWeights(0.25f, 0.70f, 0.05f);
					else if (potOdds < 0.35f)
						return new ActionWeights(0.45f, 0.50f, 0.05f);
					else
						return new ActionWeights(0.70f, 0.25f, 0.05f);
				case HandStrength.Weak:
					if (potOdds < 0.25f && spr < 1.5f) // Drawing, committed
						return new ActionWeights(0.40f, 0.55f, 0.05f);
					else
						return new ActionWeights(0.80f, 0.15f, 0.05f);
				case HandStrength.Trash:
					return new ActionWeights(0.90f, 0.05f, 0.05f);
			}
		}
		
		// RIVER
		if (street == Street.River)
		{
			switch (strength)
			{
				case HandStrength.Nuts:
					return new ActionWeights(0.00f, 0.20f, 0.80f);
				case HandStrength.Strong:
					return new ActionWeights(0.10f, 0.70f, 0.20f);
				case HandStrength.Medium:
					if (potOdds < 0.30f) // Good price, bluff catcher
						return new ActionWeights(0.40f, 0.58f, 0.02f);
					else
						return new ActionWeights(0.70f, 0.28f, 0.02f);
				case HandStrength.Weak:
					if (potOdds < 0.20f) // Great price
						return new ActionWeights(0.50f, 0.48f, 0.02f);
					else
						return new ActionWeights(0.85f, 0.13f, 0.02f);
				case HandStrength.Trash:
					return new ActionWeights(0.95f, 0.03f, 0.02f);
			}
		}
		
		// Fallback
		return new ActionWeights(0.70f, 0.25f, 0.05f);
	}
	
	// When not facing a bet (can check)
	private static ActionWeights GetAggressiveWeights(HandStrength strength, Street street, float spr)
	{
		// PREFLOP (SB completing or BB checking)
		if (street == Street.Preflop)
		{
			switch (strength)
			{
				case HandStrength.Nuts:
					return new ActionWeights(0.00f, 0.10f, 0.90f); // Always raise
				case HandStrength.Strong:
					return new ActionWeights(0.00f, 0.30f, 0.70f);
				case HandStrength.Medium:
					return new ActionWeights(0.00f, 0.70f, 0.30f); // Mostly check
				case HandStrength.Weak:
					return new ActionWeights(0.00f, 0.85f, 0.15f);
				case HandStrength.Trash:
					return new ActionWeights(0.00f, 0.90f, 0.10f); // Mostly check
			}
		}
		
		// FLOP/TURN/RIVER (can check or bet)
		switch (strength)
		{
			case HandStrength.Nuts:
				return new ActionWeights(0.00f, 0.25f, 0.75f); // Bet or slowplay
			case HandStrength.Strong:
				return new ActionWeights(0.00f, 0.40f, 0.60f); // Bet for value
			case HandStrength.Medium:
				if (spr < 2.0f) // Short stack, more aggressive
					return new ActionWeights(0.00f, 0.45f, 0.55f);
				else
					return new ActionWeights(0.00f, 0.70f, 0.30f); // Mostly check
			case HandStrength.Weak:
				return new ActionWeights(0.00f, 0.80f, 0.20f); // Mostly check, some bluffs
			case HandStrength.Trash:
				return new ActionWeights(0.00f, 0.85f, 0.15f); // Check or bluff
		}
		
		return new ActionWeights(0.00f, 0.70f, 0.30f);
	}
}
