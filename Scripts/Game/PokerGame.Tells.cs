using Godot;
using System;

public partial class PokerGame
{
	// --- TELL TIMER CALLBACK ---
	
	private void OnTellTimerTimeout()
	{
		if (!handInProgress || isShowdownInProgress || aiOpponent.IsFolded) return;
		if (isProcessingAIAction) return; 
		if (aiOpponent.IsAllIn || playerIsAllIn) return;
		if (currentStreet == Street.Preflop) return;

		if (isPlayerTurn)
		{
			ShowTell(false);
		}
	}

	// --- EXPRESSION ANIMATION ---
	
	private async void ShowMomentaryExpression(Expression expr, float duration)
	{
		SetExpression(expr);
		
		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
		
		if (handInProgress && !isShowdownInProgress && !isProcessingAIAction && !aiOpponent.IsFolded)
		{
			SetExpression(Expression.Neutral);
		}
	}

	// --- TELL SYSTEM ---
	
	private void ShowTell(bool forceTell = false)
	{
		GameState gameState = CreateGameState();
		HandStrength strength = aiOpponent.DetermineHandStrengthCategory(gameState);

		// Preflop tells (when cards are dealt)
		if (currentStreet == Street.Preflop && forceTell)
		{
			ShowPreflopTell(strength);
			return;
		}

		// Idle tells (during gameplay)
		ShowIdleTell(strength, forceTell);
	}

	// --- PREFLOP TELLS ---
	
	private void ShowPreflopTell(HandStrength strength)
	{
		float duration = 3.0f; 

		bool isTilted = aiOpponent.CurrentTiltState != TiltState.Zen;
		bool isSteaming = aiOpponent.CurrentTiltState == TiltState.Steaming || 
						  aiOpponent.CurrentTiltState == TiltState.Monkey;

		// --- Scenario A: Weak Hand ---
		if (strength == HandStrength.Weak)
		{
			bool hasHighCard = opponentHand.Exists(c => c.Rank >= Rank.King);
			bool isSuited = (opponentHand.Count == 2) && (opponentHand[0].Suit == opponentHand[1].Suit);
			
			if (hasHighCard || isSuited)
			{
				SetExpression(Expression.Neutral);
				string reason = hasHighCard ? "high card" : "suited";
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is NEUTRAL (weak but {reason})");
				return;
			}

			// Tilted reaction to genuine trash
			if (isSteaming || (isTilted && GD.Randf() < 0.7f))
			{
				ShowMomentaryExpression(Expression.Angry, duration);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is ANGRY (bad cards + tilt)");
				return;
			}

			// Normal disappointment for trash hands
			if (GD.Randf() < 0.6f)
			{
				ShowMomentaryExpression(Expression.Sad, duration);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is SAD (bad cards)");
				return;
			}
			
			SetExpression(Expression.Neutral);
			GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} maintained poker face (trash hand hidden)");
			return;
		}
		
		// --- Scenario B: Medium Hand ---
		if (strength == HandStrength.Medium)
		{
			float bluffExpressionChance = aiOpponent.Personality.BaseBluffFrequency;

			// Path 1: ACTING (Bluffing / Deception)
			if (GD.Randf() < bluffExpressionChance)
			{
				if (GD.Randf() > 0.5f)
				{
					ShowMomentaryExpression(Expression.Happy, duration);
					GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is ACTING HAPPY (Medium Hand)");
				}
				else
				{
					ShowMomentaryExpression(Expression.Sad, duration);
					GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is ACTING SAD (Medium Hand)");
				}
				return;
			}
			
			// Path 2: Genuine uncertainty
			if (GD.Randf() < 0.35f)
			{
				ShowMomentaryExpression(Expression.Worried, duration); 
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is WORRIED (Medium Hand - genuine uncertainty)");
			}
			else
			{
				SetExpression(Expression.Neutral);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is NEUTRAL (Medium Hand - stoic)");
			}
			return;
		}

		// --- Scenario C: Strong Hand (Premium) ---
		if (strength == HandStrength.Strong)
		{
			bool attemptsPokerFace = GD.Randf() > aiOpponent.Personality.BaseBluffFrequency;
			
			if (!attemptsPokerFace)
			{
				ShowMomentaryExpression(Expression.Happy, duration);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is HAPPY (premium hand leaked)");
				return;
			}
			else
			{
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} maintained poker face (premium hand hidden)");
				SetExpression(Expression.Neutral);
				return;
			}
		}
		
		SetExpression(Expression.Neutral);
	}

	// --- IDLE TELLS ---
	
	private void ShowIdleTell(HandStrength strength, bool forceTell)
	{
		float idleDuration = 1.5f; 
		float baseTellChance = 0.20f;
		float tiltModifier = (float)aiOpponent.CurrentTiltState * 0.15f; 
		
		// Random chance to show tell (increased when tilted)
		if (!forceTell && GD.Randf() > (baseTellChance + tiltModifier))
		{
			return;
		}

		// Determine base tell category from hand strength
		string tellCategory = DetermineTellCategory(strength);

		// Determine if opponent is acting (reverse psychology)
		bool isActing = DetermineActingBehavior(strength);

		// Apply acting logic (reverse the tell)
		if (isActing)
		{
			tellCategory = ReverseTellCategory(tellCategory);
		}
		
		// Display the expression
		DisplayTellExpression(tellCategory, idleDuration, isActing);
	}

	// --- TELL HELPERS ---
	
	private string DetermineTellCategory(HandStrength strength)
	{
		if (strength == HandStrength.Strong)
		{
			return "strong_hand";
		}
		else if (strength == HandStrength.Bluffing)
		{
			return "bluffing";
		}
		else if (strength == HandStrength.Medium)
		{
			return (GD.Randf() > 0.6f) ? "strong_hand" : "weak_hand";
		}
		else
		{
			return "weak_hand";
		}
	}

	private bool DetermineActingBehavior(HandStrength strength)
	{
		bool canAct = aiOpponent.CurrentTiltState < TiltState.Steaming;
		float actingChance = 1.0f - aiOpponent.Personality.TellReliability;
	
		if (strength == HandStrength.Bluffing)
			actingChance += 0.10f;
		
		return canAct && (GD.Randf() < actingChance);
	}

	private string ReverseTellCategory(string tellCategory)
	{
		// Acting logic: reverse the tell
		if (tellCategory == "strong_hand")
			return "weak_hand"; // Trapping
		else if (tellCategory == "weak_hand")
			return "strong_hand"; // Bluffing
		else if (tellCategory == "bluffing")
			return "strong_hand"; // Selling the bluff
		
		return tellCategory;
	}

	private void DisplayTellExpression(string tellCategory, float duration, bool isActing)
	{
		// Retrieve expression from personality map
		if (aiOpponent.Personality.Tells.ContainsKey(tellCategory))
		{
			var possibleTells = aiOpponent.Personality.Tells[tellCategory];
			if (possibleTells.Count > 0)
			{
				string tellString = possibleTells[GD.RandRange(0, possibleTells.Count - 1)];
				if (Enum.TryParse(tellString, true, out Expression expr))
				{
					ShowMomentaryExpression(expr, duration);
					// This is the ONE log we keep visible by default, 
					// because it's interesting to see what the AI is "projecting" to the user.
					GD.Print($"[TELL] {currentOpponentName} shows {tellString} ({tellCategory})");
					
					// Hidden details about whether they were acting
					GameManager.LogVerbose($"[TELL DEBUG] Acting={isActing}");
				}
			}
		}
	}
}
