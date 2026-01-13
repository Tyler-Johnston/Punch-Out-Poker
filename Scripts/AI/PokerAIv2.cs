// File: PokerAIv2.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class PokerAIv2
{
	private OpponentProfile currentProfile;
	
	public PokerAIv2(OpponentProfile profile)
	{
		currentProfile = profile;
	}
	
	// Main decision function
	public AIAction MakeDecision(
		float equity,
		Street street,
		bool facingBet,
		int toCall,
		int pot,
		int effectiveStack,
		int currentBet,
		int opponentBet)
	{
		GD.Print("\n========== AI DECISION START ==========");
		GD.Print($"Street: {street}, Facing Bet: {facingBet}, To Call: {toCall}");
		GD.Print($"Equity: {equity:P1}, Pot: {pot}, Stack: {effectiveStack}");
		
		// STEP 1: Classify hand strength
		HandStrength strength = ClassifyHand(equity, street, facingBet);
		GD.Print($"[CLASSIFICATION] {strength} (equity: {equity:P1})");
		
		// STEP 2: Calculate SPR and pot odds
		float spr = pot > 0 ? (float)effectiveStack / pot : 10.0f;
		float potOdds = (facingBet && toCall > 0) ? (float)toCall / (pot + toCall) : 0f;
		GD.Print($"[SITUATION] SPR: {spr:F2}, Pot Odds: {potOdds:P1}");
		
		// STEP 3: Get base weights
		ActionWeights baseWeights = ActionWeightTables.GetBaseWeights(strength, street, facingBet, potOdds, spr);
		GD.Print($"[BASE WEIGHTS] {baseWeights}");
		
		// STEP 4: Apply profile adjustments
		ActionWeights adjustedWeights = currentProfile.AdjustWeights(baseWeights, strength, street);
		GD.Print($"[PROFILE: {currentProfile.Name}] {adjustedWeights}");
		
		// STEP 5: Check for mistakes
		MistakeType mistake = currentProfile.CheckForMistake(strength, street, facingBet);
		
		// STEP 6: Apply mistake (overrides weights)
		ActionWeights finalWeights = ApplyMistake(adjustedWeights, mistake, strength, street);
		if (mistake != MistakeType.None)
		{
			GD.Print($"[FINAL WEIGHTS (After Mistake)] {finalWeights}");
		}
		else
		{
			GD.Print($"[FINAL WEIGHTS] {finalWeights}");
		}
		
		// STEP 7: Handle all-in situations (can't call with 0 chips)
		if (facingBet && toCall >= effectiveStack)
		{
			GD.Print($"[ALL-IN SITUATION] Must call {toCall} with {effectiveStack} stack");
			// Can't call, only fold or raise all-in
			float commitment = (float)opponentBet / effectiveStack;
			if (commitment > 0.40f) // Already invested a lot
			{
				GD.Print($"[ALL-IN] High commitment ({commitment:P0}) - adjusting weights");
				finalWeights.Call = 0;
				finalWeights.Raise += finalWeights.Call; // Merge call into raise (all-in)
				finalWeights.Normalize();
			}
		}
		
		// STEP 8: Pick action based on weighted random
		AIAction decision = PickAction(finalWeights, facingBet);
		GD.Print($"[DECISION] {decision}");
		GD.Print("========== AI DECISION END ==========\n");
		
		return decision;
	}
	
	// Classify hand into strength bucket
	private HandStrength ClassifyHand(float equity, Street street, bool facingBet)
	{
		// Postflop facing bet - use tighter ranges
		if (street != Street.Preflop && facingBet)
		{
			if (equity >= 0.70f) return HandStrength.Nuts;
			if (equity >= 0.55f) return HandStrength.Strong;
			if (equity >= 0.40f) return HandStrength.Medium;
			if (equity >= 0.25f) return HandStrength.Weak;
			return HandStrength.Trash;
		}
		
		// Postflop not facing bet - slightly wider
		if (street != Street.Preflop)
		{
			if (equity >= 0.65f) return HandStrength.Nuts;
			if (equity >= 0.50f) return HandStrength.Strong;
			if (equity >= 0.35f) return HandStrength.Medium;
			if (equity >= 0.22f) return HandStrength.Weak;
			return HandStrength.Trash;
		}
		
		// Preflop facing bet
		if (facingBet)
		{
			if (equity >= 0.65f) return HandStrength.Nuts;    // AA, KK, AK
			if (equity >= 0.52f) return HandStrength.Strong;  // QQ-TT, AQ, AJs
			if (equity >= 0.38f) return HandStrength.Medium;  // 99-22, suited connectors
			if (equity >= 0.25f) return HandStrength.Weak;    // Weak aces, random Broadway
			return HandStrength.Trash;
		}
		
		// Preflop not facing bet (opening)
		if (equity >= 0.60f) return HandStrength.Nuts;
		if (equity >= 0.45f) return HandStrength.Strong;
		if (equity >= 0.32f) return HandStrength.Medium;
		if (equity >= 0.20f) return HandStrength.Weak;
		return HandStrength.Trash;
	}
	
	// Apply mistake patterns
	private ActionWeights ApplyMistake(ActionWeights weights, MistakeType mistake, HandStrength strength, Street street)
	{
		switch (mistake)
		{
			case MistakeType.CallingStation:
				// Refuses to fold, calls almost everything
				return new ActionWeights(0.05f, 0.90f, 0.05f);
				
			case MistakeType.BadBluff:
				// Bluffs aggressively with trash
				return new ActionWeights(0.10f, 0.20f, 0.70f);
				
			case MistakeType.Overfold:
				// Folds medium hands too easily
				return new ActionWeights(0.75f, 0.20f, 0.05f);
				
			case MistakeType.MissedValue:
				// Too passive with strong hands (checks instead of betting)
				return new ActionWeights(0.00f, 0.75f, 0.25f);
				
			case MistakeType.BadFold:
				// Panics and folds strong hands
				return new ActionWeights(0.80f, 0.15f, 0.05f);
				
			default:
				return weights; // No mistake, return normal weights
		}
	}
	
	// Pick action using weighted random
	private AIAction PickAction(ActionWeights weights, bool facingBet)
	{
		// If not facing bet, convert fold to check
		if (!facingBet)
		{
			weights.Call += weights.Fold; // Merge fold weight into check/call
			weights.Fold = 0f;
			weights.Normalize();
		}
		
		float roll = GD.Randf();
		float cumulative = 0f;
		
		cumulative += weights.Fold;
		if (roll <= cumulative)
		{
			return facingBet ? AIAction.Fold : AIAction.Check; // ✅ FIX: Check if not facing bet
		}
		
		cumulative += weights.Call;
		if (roll <= cumulative)
		{
			return facingBet ? AIAction.Call : AIAction.Check; // ✅ FIX: Check if not facing bet
		}
		
		cumulative += weights.Raise;
		if (roll <= cumulative)
		{
			return facingBet ? AIAction.Raise : AIAction.Bet; // ✅ FIX: Bet if not facing bet
		}
		
		// Fallback
		return facingBet ? AIAction.Fold : AIAction.Check; // ✅ FIX: Check if not facing bet
	}
}
