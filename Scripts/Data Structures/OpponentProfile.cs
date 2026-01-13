// File: OpponentProfile.cs
using Godot;
using System;

public class OpponentProfile
{
	// Basic info
	public string Name { get; set; }
	public int BuyIn { get; set; }  // Keep for progression
	
	// Core personality (0.0 to 1.0) - THAT'S IT
	public float Looseness { get; set; }
	public float Aggressiveness { get; set; }
	
	// Mistake rates (0.0 to 1.0) - Direct, no calculation
	public float CallingStationRate { get; set; }
	public float BadBluffRate { get; set; }
	public float OverfoldRate { get; set; }
	public float MissedValueRate { get; set; }
	
	// Methods for new system
	public ActionWeights AdjustWeights(ActionWeights baseWeights, HandStrength strength, Street street)
	{
		var adjusted = baseWeights.Clone();
		
		// Looseness: folds less, calls more
		if (Looseness > 0.5f)
		{
			float loosenessFactor = (Looseness - 0.5f) * 2;
			adjusted.Fold *= (1.0f - loosenessFactor * 0.5f);
			adjusted.Call *= (1.0f + loosenessFactor * 0.5f);
		}
		else if (Looseness < 0.5f)
		{
			float tightnessFactor = (0.5f - Looseness) * 2;
			adjusted.Fold *= (1.0f + tightnessFactor * 0.3f);
			adjusted.Call *= (1.0f - tightnessFactor * 0.3f);
		}
		
		// Aggressiveness: raises more, calls less
		if (Aggressiveness > 0.5f)
		{
			float aggroFactor = (Aggressiveness - 0.5f) * 2;
			float callToRaise = adjusted.Call * aggroFactor * 0.4f;
			adjusted.Call -= callToRaise;
			adjusted.Raise += callToRaise;
		}
		else if (Aggressiveness < 0.5f)
		{
			float passiveFactor = (0.5f - Aggressiveness) * 2;
			float raiseToCall = adjusted.Raise * passiveFactor * 0.6f;
			adjusted.Raise -= raiseToCall;
			adjusted.Call += raiseToCall;
		}
		
		adjusted.Normalize();
		return adjusted;
	}
	
	public MistakeType CheckForMistake(HandStrength strength, Street street, bool facingBet)
	{
		float roll = GD.Randf();
		
		if (facingBet && (strength == HandStrength.Weak || strength == HandStrength.Trash))
		{
			if (roll < CallingStationRate)
			{
				GD.Print($"[MISTAKE] {Name}: Calling Station");
				return MistakeType.CallingStation;
			}
		}
		
		if (!facingBet && strength == HandStrength.Trash && street >= Street.Flop)
		{
			roll = GD.Randf();
			if (roll < BadBluffRate)
			{
				GD.Print($"[MISTAKE] {Name}: Bad Bluff");
				return MistakeType.BadBluff;
			}
		}
		
		if (facingBet && strength == HandStrength.Medium)
		{
			roll = GD.Randf();
			if (roll < OverfoldRate)
			{
				GD.Print($"[MISTAKE] {Name}: Overfold");
				return MistakeType.Overfold;
			}
		}
		
		if (!facingBet && strength == HandStrength.Strong && street >= Street.Turn)
		{
			roll = GD.Randf();
			if (roll < MissedValueRate)
			{
				GD.Print($"[MISTAKE] {Name}: Missed Value");
				return MistakeType.MissedValue;
			}
		}
		
		return MistakeType.None;
	}
}
