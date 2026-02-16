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
			bool leaksExcitement = GD.Randf() > aiOpponent.Personality.TellReliability;
			
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
		// Tilt override
		if (aiOpponent.CurrentTiltState == TiltState.Steaming || aiOpponent.CurrentTiltState == TiltState.Monkey)
		{
			SetExpression(Expression.Angry);
			GD.Print($"[TELL] {currentOpponentName} is ANGRY (tilted)");
			return;
		}

		// Bluff tells
		if (isBluffing)
		{
			if (action == PlayerAction.Raise || action == PlayerAction.AllIn)
			{
				SetExpression(Expression.FakeStrong);
				GD.Print($"[TELL] {currentOpponentName} shows FAKE STRENGTH (bluffing)");
				return;
			}
			else if (action == PlayerAction.Check || action == PlayerAction.Call)
			{
				SetExpression(Expression.FakeWeak);
				GD.Print($"[TELL] {currentOpponentName} shows FAKE WEAKNESS (trap)");
				return;
			}
		}

		// Action-based expressions
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
					SetExpression(Expression.Worried);
					GD.Print($"[TELL] {currentOpponentName} is WORRIED (weak call)");
				}
				else
				{
					SetExpression(Expression.Neutral);
				}
				break;
				
			case PlayerAction.Raise:
				if (handStrength > 0.7f)
				{
					SetExpression(Expression.Smirk);
					GD.Print($"[TELL] {currentOpponentName} is CONFIDENT (strong raise)");
				}
				else if (handStrength < 0.5f)
				{
					SetExpression(Expression.Worried);
					GD.Print($"[TELL] {currentOpponentName} is WORRIED (thin value)");
				}
				else
				{
					SetExpression(Expression.Neutral);
				}
				break;
				
			case PlayerAction.AllIn:
				SetExpression(Expression.Smirk);
				GD.Print($"[TELL] {currentOpponentName} is CONFIDENT (all-in)");
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
