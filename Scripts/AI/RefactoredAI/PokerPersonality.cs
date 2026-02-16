using Godot;
using System;
using Godot.Collections;

public partial class PokerPersonality : Resource
{
	[Export] public string CharacterName { get; set; }
	[Export] public float BaseAggression { get; set; }
	[Export] public float BaseBluffFrequency { get; set; }
	[Export] public float BaseFoldThreshold { get; set; }
	[Export] public float BaseRiskTolerance { get; set; }
	[Export] public float TiltSensitivity { get; set; }
	[Export] public float CallTendency { get; set; }
	[Export] public float Chattiness { get; set; }
	[Export] public float Composure { get; set; }
	[Export] public float SurrenderChipPercent { get; set; } = 0.50f; 
	[Export] public float RageQuitThreshold { get; set; } = 90.0f;
	[Export] public float VoicePitch { get; set; }

	// Current modified stats (affected by tilt)
	public float CurrentAggression { get; private set; }
	public float CurrentBluffFrequency { get; private set; }
	public float CurrentFoldThreshold { get; private set; }
	public float CurrentRiskTolerance { get; private set; }

	// Tilt tracking
	public float TiltMeter { get; set; } = 0f;
	public int ConsecutiveLosses { get; set; } = 0;

	// Behavioral tells: key = "strong_hand", "weak_hand", "bluffing"
	[Export] public Dictionary<string, Array<string>> Tells { get; set; }

	// Dialogue lines: key = "OnFold", "OnBet", "StrongHand", "WhileWaiting", etc.
	[Export] public Dictionary<string, Array<string>> Dialogue { get; set; }

	public PokerPersonality()
	{
		Tells = new Dictionary<string, Array<string>>();
		Dialogue = new Dictionary<string, Array<string>>();
		ResetCurrentStats();
	}

	public void ResetCurrentStats()
	{
		CurrentAggression = BaseAggression;
		CurrentBluffFrequency = BaseBluffFrequency;
		CurrentFoldThreshold = BaseFoldThreshold;
		CurrentRiskTolerance = BaseRiskTolerance;
	}

	/// <summary>
	/// Add tilt when player loses/suffers bad beat
	/// consecutive losses build up anger
	/// </summary>
	public void AddTilt(float baseAmount)
	{
		// TiltSensitivity scales how much they tilt (0.20 = tough, 0.60 = emotional)
		float scaledTilt = baseAmount * TiltSensitivity;
		TiltMeter = Mathf.Clamp(TiltMeter + scaledTilt, 0f, 100f);

		UpdateStatsFromTilt();

		GD.Print($"[TILT] {CharacterName} +{scaledTilt:F1} tilt (total: {TiltMeter:F1}) | " +
				 $"Agg: {CurrentAggression:F3}, Bluff: {CurrentBluffFrequency:F3}");
	}

	/// <summary>
	/// Reduce tilt (only on wins, not every hand!)
	/// </summary>
	public void ReduceTilt(float baseAmount)
	{
		float previousTilt = TiltMeter;
		TiltMeter = Mathf.Max(0f, TiltMeter - baseAmount);

		if (previousTilt != TiltMeter)
		{
			UpdateStatsFromTilt();

			if (TiltMeter == 0f)
			{
				GD.Print($"[TILT] {CharacterName} fully recovered (calm)");
			}
		}
	}

	/// <summary>
	/// Update all personality stats based on current tilt level
	/// Higher tilt = more aggressive, more bluffs, less folding, more risk-taking
	/// </summary>
	private void UpdateStatsFromTilt()
	{
		// Tilt factor: 0 tilt = 1.0x, 20 tilt = 1.2x, 50 tilt = 1.5x, 100 tilt = 2.0x
		float tiltFactor = 1f + (TiltMeter / 100f);

		// Increase aggression (bet/raise more when tilted)
		CurrentAggression = Mathf.Clamp(BaseAggression * tiltFactor, 0f, 1f);

		// Increase bluff frequency (try to win back losses)
		CurrentBluffFrequency = Mathf.Clamp(BaseBluffFrequency * tiltFactor, 0f, 1f);

		// Decrease fold threshold (call more marginal spots when tilted = worse defense)
		// If FoldThreshold = 0.50, at 20 tilt becomes 0.40 (calls 10% wider)
		float foldReduction = (TiltMeter / 100f) * 0.20f; // Max -20% at 100 tilt
		CurrentFoldThreshold = Mathf.Clamp(BaseFoldThreshold - foldReduction, 0.20f, 1f);

		// Increase risk tolerance (call all-ins lighter when tilted)
		CurrentRiskTolerance = Mathf.Clamp(BaseRiskTolerance * tiltFactor, 0f, 1f);
	}
}
