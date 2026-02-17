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
	MediumHand,
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

	// Use one RNG for this manager (fine for debug + determinism isn't critical here)
	private readonly Random rng = new Random();

	private float lastDialogueTime = 0f;
	private const float DIALOGUE_COOLDOWN = 3.0f; // Minimum seconds between spoken lines

	// Track recent lines to avoid repetition
	private readonly Queue<string> recentLines = new Queue<string>();
	private const int MAX_RECENT_LINES = 5;

	// Debug toggle
	public bool DebugDialogue { get; set; } = true;

	// Optional: street tracking + per-street "honest vs deceptive" cache for tell dialogue
	private Street currentStreet = Street.Preflop;
	private Street cachedTellStreet = Street.Preflop;
	private bool? cachedHonestTellThisStreet = null; // true=honest, false=deceptive, null=not rolled yet

	public void Initialize(PokerPersonality opponentPersonality)
	{
		personality = opponentPersonality;
	}

	/// <summary>
	/// Call from PokerGame whenever street changes (and at hand start).
	/// This prevents per-street tell mode from flip-flopping during raise wars.
	/// </summary>
	public void SetCurrentStreet(Street street)
	{
		if (street == currentStreet) return;

		currentStreet = street;

		// Street changed => clear tell-mode cache so we re-roll honesty/deception for the new street.
		cachedTellStreet = street;
		cachedHonestTellThisStreet = null;
	}

	/// <summary>
	/// Call at the beginning of each new hand.
	/// </summary>
	public void ResetForNewHand()
	{
		lastDialogueTime = 0f;
		recentLines.Clear();

		currentStreet = Street.Preflop;
		cachedTellStreet = Street.Preflop;
		cachedHonestTellThisStreet = null;
	}

	/// <summary>
	/// Get a dialogue line for the given context, or null if none should be shown.
	/// Uses cooldown + chattiness gating (unless forced / ranting).
	/// </summary>
	public string GetDialogue(DialogueContext context, bool forceTell = false)
	{
		if (personality == null)
		{
			Log($"[DIALOGUE] ERROR: personality is null in GetDialogue({context})");
			return null;
		}

		float now = Time.GetTicksMsec() / 1000f;

		// Cooldown (unless forced)
		if (!forceTell && now - lastDialogueTime < DIALOGUE_COOLDOWN)
		{
			Log($"[DIALOGUE] Blocked by cooldown ({now - lastDialogueTime:F2}s < {DIALOGUE_COOLDOWN:F2}s) ctx={context}");
			return null;
		}

		// Chattiness roll (ranting ignores chattiness; forced ignores everything)
		bool isRanting = (context == DialogueContext.OnTilt);
		if (!forceTell && !isRanting)
		{
			double roll = rng.NextDouble();
			if (roll > personality.Chattiness)
			{
				Log($"[DIALOGUE] Quiet (roll {roll:F2} > chat {personality.Chattiness:F2}) ctx={context}");
				return null;
			}
		}

		string line = SelectLine(context);
		if (string.IsNullOrEmpty(line))
			return null;

		CommitSpokenLine(now, line, context.ToString());
		return line;
	}

	/// <summary>
	/// Get a tell-based dialogue (behavioral cue).
	/// May be misleading based on Composure.
	/// Now respects cooldown + chattiness (so it doesn't spam during raise wars).
	/// Also supports MediumHand and fixes enum ordering bugs.
	/// </summary>
	public string GetTellDialogue(HandStrength actualStrength, bool isBluffing, bool forceTell = false)
	{
		if (personality == null)
		{
			Log("[DIALOGUE] ERROR: personality is null in GetTellDialogue");
			return null;
		}

		float now = Time.GetTicksMsec() / 1000f;

		// Cooldown gate (unless forced)
		if (!forceTell && now - lastDialogueTime < DIALOGUE_COOLDOWN)
		{
			Log($"[DIALOGUE] (TELL) Blocked by cooldown ({now - lastDialogueTime:F2}s < {DIALOGUE_COOLDOWN:F2}s)");
			return null;
		}

		// Chattiness gate (unless forced)
		if (!forceTell)
		{
			double chatRoll = rng.NextDouble();
			if (chatRoll > personality.Chattiness)
			{
				Log($"[DIALOGUE] (TELL) Quiet (roll {chatRoll:F2} > chat {personality.Chattiness:F2})");
				return null;
			}
		}

		// Decide honesty/deception once per street to avoid flip-flopping within the same street
		bool honestThisStreet;
		if (cachedHonestTellThisStreet.HasValue && cachedTellStreet == currentStreet)
		{
			honestThisStreet = cachedHonestTellThisStreet.Value;
			Log($"[DIALOGUE] (TELL) Using cached street tell-mode: {(honestThisStreet ? "HONEST" : "DECEPTIVE")} (street={currentStreet})");
		}
		else
		{
			double compRoll = rng.NextDouble();
			// Corrected Logic: High roll > Composure means they FAIL to keep composure (HONEST)
			honestThisStreet = compRoll > personality.Composure;
			cachedTellStreet = currentStreet;
			cachedHonestTellThisStreet = honestThisStreet;

			Log($"[DIALOGUE] (TELL) Composure roll {compRoll:F2} vs {personality.Composure:F2} => {(honestThisStreet ? "HONEST" : "DECEPTIVE")} (street={currentStreet})");
		}

		DialogueContext tellContext;

		if (isBluffing)
		{
			// Bluffing: honest -> Bluffing tell, deceptive -> StrongHand tell (selling it)
			if (honestThisStreet)
			{
				tellContext = DialogueContext.Bluffing;
				Log("[DIALOGUE] → Showing BLUFFING tell (honest - lacks confidence)");
			}
			else
			{
				tellContext = DialogueContext.StrongHand;
				Log("[DIALOGUE] → Showing STRONG HAND tell (deceptive - selling the bluff!)");
			}
		}
		else if (actualStrength == HandStrength.Strong) // FIXED: Explicit check, not >=
		{
			// Strong hand: honest -> StrongHand, deceptive -> WeakHand (Hollywood/Trap)
			if (honestThisStreet)
			{
				tellContext = DialogueContext.StrongHand;
				Log("[DIALOGUE] → Showing STRONG HAND tell (honest confidence)");
			}
			else
			{
				tellContext = DialogueContext.WeakHand;
				Log("[DIALOGUE] → Showing WEAK HAND tell (Hollywood/slowplay!)");
			}
		}
		else if (actualStrength == HandStrength.Medium) // FIXED: Explicit check
		{
			// Medium hand: honest -> MediumHand, deceptive -> StrongHand (act confident/protection)
			if (honestThisStreet)
			{
				tellContext = DialogueContext.MediumHand;
				Log("[DIALOGUE] → Showing MEDIUM HAND tell (uncertain/speculative)");
			}
			else
			{
				tellContext = DialogueContext.StrongHand;
				Log("[DIALOGUE] → Showing STRONG HAND tell (medium hand acting confident)");
			}
		}
		else // Weak Hand
		{
			// Weak hand: usually shows genuine weakness unless trying to act tough (rare)
			// For now, keep simple: Weak -> WeakHand tell
			tellContext = DialogueContext.WeakHand;
			Log("[DIALOGUE] → Showing WEAK HAND tell (genuinely weak)");
		}

		string line = SelectLine(tellContext);
		if (string.IsNullOrEmpty(line))
			return null;

		CommitSpokenLine(now, line, tellContext.ToString());
		return line;
	}


	/// <summary>
	/// Get tilt-adjusted dialogue using TiltState.
	/// This ignores chattiness (ranting), but still respects cooldown to avoid spam.
	/// </summary>
	public string GetTiltDialogue(TiltState tiltState)
	{
		if (personality == null)
		{
			Log("[DIALOGUE] ERROR: personality is null in GetTiltDialogue");
			return null;
		}

		// Zen players don't complain about tilt
		if (tiltState == TiltState.Zen) return null;

		// Probability to speak based on anger level
		float probability = tiltState switch
		{
			TiltState.Annoyed => 0.15f,
			TiltState.Steaming => 0.40f,
			TiltState.Monkey => 0.75f,
			_ => 0f
		};

		if (rng.NextDouble() > probability) return null;

		// Use GetDialogue so cooldown + tracking are consistent; mark it as "ranting" by forcing.
		return GetDialogue(DialogueContext.OnTilt, forceTell: true);
	}

	private string SelectLine(DialogueContext context)
	{
		if (personality == null || personality.Dialogue == null)
			return null;

		string key = context.ToString();

		// Check if we have lines for this context
		if (!personality.Dialogue.ContainsKey(key) || personality.Dialogue[key].Count == 0)
			return GetLegacyTell(context);

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
	/// Backward compatibility with existing Tells dictionary.
	/// </summary>
	private string GetLegacyTell(DialogueContext context)
	{
		if (personality == null || personality.Tells == null)
			return null;

		string tellKey = context switch
		{
			DialogueContext.StrongHand => "strong_hand",
			DialogueContext.MediumHand => "medium_hand",
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

	private void CommitSpokenLine(float now, string line, string reasonKey)
	{
		lastDialogueTime = now;
		TrackRecentLine(line);
		Log($"[DIALOGUE] ✓ Selected ({reasonKey}): \"{line}\"");
	}

	private void TrackRecentLine(string line)
	{
		recentLines.Enqueue(line);
		while (recentLines.Count > MAX_RECENT_LINES)
			recentLines.Dequeue();
	}

	private void Log(string msg)
	{
		if (DebugDialogue)
			GD.Print(msg);
	}
}
