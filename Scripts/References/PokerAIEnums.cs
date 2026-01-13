using Godot;
using System;
using System.Collections.Generic;

public enum AIAction
{
	Fold,
	Check,
	Call,
	Bet,
	Raise
}

public enum HandStrength
{
	Nuts,      // 70%+ equity - dominanting hand
	Strong,    // 55-70% equity - solid hand
	Medium,    // 40-55% equity - playable hand
	Weak,      // 25-40% equity - marginal hand
	Trash      // <25% equity - should fold
}

public enum MistakeType
{
	None,
	CallingStation,  // Refuses to fold weak hands
	BadBluff,        // Bluffs with trash
	Overfold,        // Folds too easily
	MissedValue,     // Too passive with strong hands
	BadFold          // Folds strong hands to pressure
}

public class ActionWeights
{
	public float Fold { get; set; }
	public float Call { get; set; }
	public float Raise { get; set; }
	
	public ActionWeights(float fold, float call, float raise)
	{
		Fold = fold;
		Call = call;
		Raise = raise;
	}
	
	public ActionWeights Clone()
	{
		return new ActionWeights(Fold, Call, Raise);
	}
	
	public void Normalize()
	{
		float total = Fold + Call + Raise;
		if (total > 0)
		{
			Fold /= total;
			Call /= total;
			Raise /= total;
		}
	}
	
	public override string ToString()
	{
		return $"Fold: {Fold:P0}, Call: {Call:P0}, Raise: {Raise:P0}";
	}
}
