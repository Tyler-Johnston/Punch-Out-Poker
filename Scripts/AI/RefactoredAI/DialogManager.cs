// DialogueManager.cs
using Godot;
using System;
using System.Collections.Generic;

public enum DialogueContext
{
	// Action-based
	OnFold,
	OnCheck,
	OnCall,
	OnBet,
	OnRaise,
	OnAllIn,
	
	// Hand result
	OnWinPot,
	OnLosePot,
	OnBadBeat,
	
	// Situational
	WhileWaiting,
	OnTilt,
	BigPot,
	
	// Hand strength (tells)
	StrongHand,
	WeakHand,
	Bluffing,
	
	// Misc flavor
	GameStart,
	LowStack,
	Comeback
}

public partial class DialogueManager : Node
{
	private PokerPersonality personality;
	private Random rng = new Random();
	private float lastDialogueTime = 0f;
	private const float DIALOGUE_COOLDOWN = 3.0f; // Minimum seconds between lines
	
	// Track recent lines to avoid repetition
	private Queue<string> recentLines = new Queue<string>();
	private const int MAX_RECENT_LINES = 5;
	
	public void Initialize(PokerPersonality opponentPersonality)
	{
		personality = opponentPersonality;
	}
	
	/// <summary>
	/// Get a dialogue line for the given context, or null if none should be shown
	/// </summary>
	public string GetDialogue(DialogueContext context, bool forceTell = false)
	{
		// Check cooldown (unless it's a forced tell)
		float currentTime = Time.GetTicksMsec() / 1000f;
		if (!forceTell && currentTime - lastDialogueTime < DIALOGUE_COOLDOWN)
			return null;
		
		// Check if character wants to talk (chattiness roll)
		// Steaming/Monkey players ignore Chattiness stat and rant anyway
		bool isRanting = (context == DialogueContext.OnTilt); 
		if (!forceTell && !isRanting && rng.NextDouble() > personality.Chattiness)
			return null;
		
		// Get the appropriate line
		string line = SelectLine(context);
		
		if (!string.IsNullOrEmpty(line))
		{
			lastDialogueTime = currentTime;
			TrackRecentLine(line);
		}
		
		return line;
	}
	
	/// <summary>
	/// Get a tell-based dialogue (behavioral cue)
	/// May be misleading based on Composure
	/// </summary>
	public string GetTellDialogue(HandStrength actualStrength, bool isBluffing)
	{
		// Determine what tell to show
		DialogueContext tellContext;
		
		if (isBluffing)
		{
			// When bluffing, show bluffing tells OR fake strong tells
			if (rng.NextDouble() < personality.Composure)
			{
				tellContext = DialogueContext.Bluffing; // Honest tell
			}
			else
			{
				tellContext = DialogueContext.StrongHand; // Deceptive tell
			}
		}
		else if (actualStrength >= HandStrength.Strong)
		{
			// Strong hand: show strong tells OR reverse tell (act weak)
			if (rng.NextDouble() < personality.Composure)
			{
				tellContext = DialogueContext.StrongHand;
			}
			else
			{
				tellContext = DialogueContext.WeakHand; // Hollywood
			}
		}
		else
		{
			// Weak hand: show weak tells
			tellContext = DialogueContext.WeakHand;
		}
		
		return SelectLine(tellContext);
	}
	
	/// <summary>
	/// Get tilt-adjusted dialogue using the new TiltState
	/// </summary>
	public string GetTiltDialogue(TiltState tiltState)
	{
		// Zen players don't complain about tilt
		if (tiltState == TiltState.Zen) return null;
		
		// Probability to speak based on anger level
		float probability = tiltState switch
		{
			TiltState.Annoyed => 0.15f,   // 15% chance
			TiltState.Steaming => 0.40f,  // 40% chance
			TiltState.Monkey => 0.75f,    // 75% chance (ranting)
			_ => 0f
		};
		
		if (rng.NextDouble() > probability) return null;
		
		return SelectLine(DialogueContext.OnTilt);
	}
	
	private string SelectLine(DialogueContext context)
	{
		string key = context.ToString();
		
		// Check if we have lines for this context
		if (!personality.Dialogue.ContainsKey(key) || 
			personality.Dialogue[key].Count == 0)
		{
			// Fall back to existing Tells dictionary for backward compatibility
			return GetLegacyTell(context);
		}
		
		var lines = personality.Dialogue[key];
		
		// Pick a random line that wasn't used recently
		int attempts = 0;
		string selectedLine;
		do
		{
			selectedLine = lines[rng.Next(lines.Count)];
			attempts++;
		} while (recentLines.Contains(selectedLine) && attempts < 5);
		
		return selectedLine;
	}
	
	/// <summary>
	/// Backward compatibility with existing Tells dictionary
	/// </summary>
	private string GetLegacyTell(DialogueContext context)
	{
		string tellKey = context switch
		{
			DialogueContext.StrongHand => "strong_hand",
			DialogueContext.WeakHand => "weak_hand",
			DialogueContext.Bluffing => "bluffing",
			_ => null
		};
		
		if (tellKey == null || !personality.Tells.ContainsKey(tellKey))
			return null;
		
		var tells = personality.Tells[tellKey];
		if (tells.Count == 0) return null;
		
		return tells[rng.Next(tells.Count)];
	}
	
	private void TrackRecentLine(string line)
	{
		recentLines.Enqueue(line);
		while (recentLines.Count > MAX_RECENT_LINES)
			recentLines.Dequeue();
	}
}
