using Godot;
using System;

public partial class PokerGame
{	
	// --- TELL TRACKING ---
	
	public void UpdatePlayerWaitTracking(float delta)
	{
		if (!handInProgress || !isPlayerTurn || isProcessingAIAction) return;
		
		playerWaitTime += delta;
		
		if (playerWaitTime > BOREDOM_THRESHOLD && !hasShownBoredomTell)
		{
			GD.Print($"[TELL] {currentOpponentName} is BORED (Player wait time > {BOREDOM_THRESHOLD}s)");
			SetExpression(Expression.Bored);
			hasShownBoredomTell = true;
			PlayWaitingDialogue();
		}
	}
	
	public void ResetPlayerWaitTime()
	{
		playerWaitTime = 0f;
		hasShownBoredomTell = false;

		// If they were bored, snap back to neutral immediately when player acts
		if (faceSprite != null && faceSprite.Frame == (int)Expression.Bored)
		{
			SetExpression(Expression.Neutral);
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

	// --- PREFLOP TELLS ---
	
	public void ShowPreflopTell()
	{
		GameState gameState = CreateGameState();
		HandStrength strength = aiOpponent.DetermineHandStrengthCategory(gameState);
		
		float duration = 3.0f;
		bool isTilted = aiOpponent.CurrentTiltState != TiltState.Zen;
		bool isSteaming = aiOpponent.CurrentTiltState == TiltState.Steaming || 
						  aiOpponent.CurrentTiltState == TiltState.Monkey;

		// --- Weak Hand (Trash) ---
		if (strength == HandStrength.Weak)
		{
			bool hasHighCard = opponentHand.Exists(c => c.Rank >= Rank.King);
			bool isSuited = (opponentHand.Count == 2) && (opponentHand[0].Suit == opponentHand[1].Suit);
			
			if (hasHighCard || isSuited)
			{
				SetExpression(Expression.Neutral);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is NEUTRAL (weak but playable)");
				return;
			}

			// Tilted reaction to genuine trash
			if (isSteaming || (isTilted && GD.Randf() < 0.7f))
			{
				ShowMomentaryExpression(Expression.Annoyed, duration);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is ANNOYED (bad cards + tilt)");
				return;
			}

			// Normal disappointment
			if (GD.Randf() < 0.6f)
			{
				ShowMomentaryExpression(Expression.Sad, duration);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is SAD (trash hand)");
				return;
			}
			
			SetExpression(Expression.Neutral);
			GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} poker face (trash hidden)");
			return;
		}
		
		// --- Medium Hand (Speculative) ---
		if (strength == HandStrength.Medium)
		{
			float actingChance = aiOpponent.Personality.BaseBluffFrequency;

			// Acting uncertain or confident
			if (GD.Randf() < actingChance)
			{
				if (GD.Randf() > 0.5f)
				{
					ShowMomentaryExpression(Expression.Smirk, duration);  // Acting strong
					GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} ACTING STRONG (medium hand)");
				}
				else
				{
					ShowMomentaryExpression(Expression.Worried, duration);  // Acting weak
					GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} ACTING WEAK (medium hand)");
				}
				return;
			}
			
			// Genuine uncertainty
			if (GD.Randf() < 0.35f)
			{
				ShowMomentaryExpression(Expression.Worried, duration);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is WORRIED (medium hand - genuine)");
			}
			else
			{
				SetExpression(Expression.Neutral);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is NEUTRAL (medium hand)");
			}
			return;
		}

		// --- Strong Hand (Premium) ---
		if (strength == HandStrength.Strong)
		{
			bool leaksExcitement = GD.Randf() > aiOpponent.Personality.Composure;
			
			if (leaksExcitement)
			{
				ShowMomentaryExpression(Expression.Happy, duration);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} is HAPPY (premium leaked)");
			}
			else
			{
				SetExpression(Expression.Neutral);
				GameManager.LogVerbose($"[TELL-PREFLOP] {currentOpponentName} poker face (premium hidden)");
			}
			return;
		}
		
		SetExpression(Expression.Neutral);
	}

	// --- ACTION-BASED TELLS (Called from AI decision) ---
	
	public void ShowActionTell(PlayerAction action, bool isBluffing, float handStrength)
	{
		// 1. TILT OVERRIDE (Always shows if angry)
		if (aiOpponent.CurrentTiltState == TiltState.Steaming || aiOpponent.CurrentTiltState == TiltState.Monkey)
		{
			SetExpression(Expression.Angry);
			GD.Print($"[TELL] {currentOpponentName} is ANGRY (tilted)");
			return;
		}

		// 2. BLUFF TELLS (Fake Strong / Fake Weak)
		if (isBluffing)
		{
			// Check if they are smart enough to hide it!
			float reliability = aiOpponent.Personality.Composure;
			
			// Adjust reliability based on Tilt (Tilt makes them worse actors)
			if (aiOpponent.CurrentTiltState != TiltState.Zen)
				reliability -= 0.15f; 

			// Roll the die: If roll > reliability, they "slip up" and show the fake tell.
			// Example: Reliability 0.9 (Smart) -> 10% chance to slip up.
			// Example: Reliability 0.4 (Dumb)  -> 60% chance to slip up.
			bool slipsUp = (GD.Randf() > reliability);

			if (slipsUp)
			{
				if (action == PlayerAction.Raise || action == PlayerAction.AllIn)
				{
					SetExpression(Expression.FakeStrong);
					GD.Print($"[TELL] {currentOpponentName} SLIPPED UP! Shows FAKE STRENGTH (bluffing)");
					return;
				}
				else if (action == PlayerAction.Check || action == PlayerAction.Call)
				{
					SetExpression(Expression.FakeWeak);
					GD.Print($"[TELL] {currentOpponentName} SLIPPED UP! Shows FAKE WEAKNESS (trap)");
					return;
				}
			}
			else
			{
				// They successfully hid the bluff!
				// Optional: Show "Confident" (Smirk) to really sell the lie?
				// For now, let's just go Neutral (Poker Face)
				SetExpression(Expression.Neutral);
				GD.Print($"[TELL] {currentOpponentName} kept a Poker Face (Hidden Bluff)");
				return;
			}
		}

		// 3. GENUINE TELLS (Value Betting / Weak Calls)
		// These also need reliability! Good players don't look "Worried" when betting thin value.
		
		switch (action)
		{
			case PlayerAction.Fold:
				SetExpression(Expression.Sad);
				GD.Print($"[TELL] {currentOpponentName} is SAD (folded)");
				break;
				
			case PlayerAction.Check:
				SetExpression(Expression.Neutral);
				break;
				
			case PlayerAction.Call:
				if (handStrength < 0.5f)
				{
					// Only show "Worried" if they are bad actors (Low Reliability)
					if (GD.Randf() > aiOpponent.Personality.Composure)
					{
						SetExpression(Expression.Worried);
						GD.Print($"[TELL] {currentOpponentName} is WORRIED (weak call)");
					}
					else
					{
						SetExpression(Expression.Neutral);
						GD.Print($"[TELL] {currentOpponentName} poker face (hid weak call)");
					}
				}
				else
				{
					SetExpression(Expression.Neutral);
				}
				break;
				
			case PlayerAction.Raise:
			case PlayerAction.AllIn:
				if (handStrength > 0.7f)
				{
					// Strong hand! Do they smirk?
					// Smart players might hide it (Neutral).
					// Arrogant players (low reliability) might smirk.
					if (GD.Randf() > aiOpponent.Personality.Composure)
					{
						SetExpression(Expression.Smirk);
						GD.Print($"[TELL] {currentOpponentName} is CONFIDENT (strong raise)");
					}
					else
					{
						SetExpression(Expression.Neutral);
						GD.Print($"[TELL] {currentOpponentName} poker face (hid strong hand)");
					}
				}
				else if (handStrength < 0.5f) // Thin Value / Semi-Bluff
				{
					// They are betting with a mediocre hand. Do they look worried?
					if (GD.Randf() > aiOpponent.Personality.Composure)
					{
						SetExpression(Expression.Worried);
						GD.Print($"[TELL] {currentOpponentName} is WORRIED (thin value)");
					}
					else
					{
						SetExpression(Expression.Neutral);
						GD.Print($"[TELL] {currentOpponentName} poker face (hid thin value)");
					}
				}
				else
				{
					SetExpression(Expression.Neutral);
				}
				break;
		}
	}


	// --- POST-HAND TELLS (Called from ShowDown/EndHand) ---
	
	public void ShowResultTell(bool won, int tiltDelta)
	{
		if (won)
		{
			SetExpression(Expression.Happy);
			GD.Print($"[TELL] {currentOpponentName} is HAPPY (won pot)");
		}
		else
		{
			if (tiltDelta > 10)
			{
				SetExpression(Expression.Sob);
				GD.Print($"[TELL] {currentOpponentName} is SOBBING (bad beat!)");
			}
			else
			{
				SetExpression(Expression.Sad);
				GD.Print($"[TELL] {currentOpponentName} is SAD (lost pot)");
			}
		}
		
		// Return to neutral after 3 seconds
		//GetTree().CreateTimer(3.0f).Timeout += () => 
		//{
			//if (!handInProgress) SetExpression(Expression.Neutral);
		//};
	}
}
