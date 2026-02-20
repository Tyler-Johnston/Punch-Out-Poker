// Scripts/AI/RefactoredAI/PlayerTendencyTracker.cs
using Godot;
using System.Collections.Generic;

public class PlayerTendencyTracker
{
	// Aggression metrics
	public float VPIPRate;           // Voluntarily Put $ In Pot (non-fold preflop %)
	public float PFRRate;            // Pre-Flop Raise %
	public float AggressionFactor;   // (Bets + Raises) / Calls
	public float ThreeBetRate;       // How often they re-raise preflop
	
	// Post-flop patterns
	public float ContinuationBetRate;     // Bet flop after preflop raise
	public float FoldToCBetRate;          // Fold when facing continuation bet
	public float CheckRaiseRate;          // Check-raise frequency
	public float BarrelRate;              // Bet flop + turn frequency
	
	// Showdown analysis
	public float ShowdownWentToShowdownWith; // Average hand strength at showdown
	public float BluffSuccessRate;           // Shown bluffs vs total bluffs
	public float ValueBetThinness;           // How thin they value bet
	
	// Bet sizing tells
	public Dictionary<Street, float> AverageBetSizeByStrength; // Weak/Medium/Strong
	public float OverbetBluffFrequency;      // Large bets with weak hands
	
	// Sample sizes
	public int HandsSeen;
	public int ShowdownsReached;
}
