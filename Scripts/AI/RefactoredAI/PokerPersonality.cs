using Godot;
using System;
using Godot.Collections;

public partial class PokerPersonality : Resource
{
	// Base personality traits (0.0 to 1.0)
	[Export] public string CharacterName { get; set; }
	[Export] public float BaseAggression { get; set; }
	[Export] public float BaseBluffFrequency { get; set; }
	[Export] public float BaseFoldThreshold { get; set; }
	[Export] public float BaseRiskTolerance { get; set; }
	[Export] public float TiltSensitivity { get; set; }
	[Export] public float CallTendency { get; set; }
	
	// Current modified stats (affected by tilt)
	public float CurrentAggression { get; private set; }
	public float CurrentBluffFrequency { get; private set; }
	public float CurrentFoldThreshold { get; private set; }
	public float CurrentRiskTolerance { get; private set; }
	
	// Tilt tracking
	public float TiltMeter { get; private set; } = 0f;
	public int ConsecutiveLosses { get; set; } = 0;
	
	[Export] public Dictionary<string, Array<string>> Tells { get; set; }
	
	public PokerPersonality()
	{
		Tells = new Dictionary<string, Array<string>>();
		ResetCurrentStats();
	}
	
	public void ResetCurrentStats()
	{
		CurrentAggression = BaseAggression;
		CurrentBluffFrequency = BaseBluffFrequency;
		CurrentFoldThreshold = BaseFoldThreshold;
		CurrentRiskTolerance = BaseRiskTolerance;
	}
	
	public void UpdateTiltedStats()
	{
		float tiltMultiplier = TiltMeter * TiltSensitivity * 0.01f;
		
		CurrentAggression = Mathf.Clamp(BaseAggression + (tiltMultiplier), 0f, 1f);
		CurrentBluffFrequency = Mathf.Clamp(BaseBluffFrequency + (tiltMultiplier * 0.8f), 0f, 1f);
		CurrentFoldThreshold = Mathf.Clamp(BaseFoldThreshold - (tiltMultiplier * 0.5f), 0f, 1f);
		CurrentRiskTolerance = Mathf.Clamp(BaseRiskTolerance + (tiltMultiplier), 0f, 1f);
	}
	
	public void AddTilt(float amount)
	{
		TiltMeter = Mathf.Clamp(TiltMeter + amount, 0f, 100f);
		UpdateTiltedStats();
	}
	
	public void ReduceTilt(float amount)
	{
		TiltMeter = Mathf.Max(0f, TiltMeter - amount);
		UpdateTiltedStats();
	}
}
