// PokerGame.AI.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	// AI System Variables (continued / completed here)
	private bool aiBluffedThisHand = false;
	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();
	private int playerTotalBetsThisHand = 0;

	private AIAction DecideAIAction()
	{
		float handStrength = EvaluateAIHandStrength();
		int toCall = currentBet - opponentBet;
		bool facingBet = toCall > 0;

		GD.Print($"AI Decision - Hand Strength: {handStrength:F2}, To Call: {toCall}");

		// Get thresholds from opponent profile
		float foldThreshold = currentOpponent.FoldThreshold;
		float callThreshold = currentOpponent.CallThreshold;
		float raiseThreshold = currentOpponent.RaiseThreshold;
		float bluffChance = currentOpponent.BluffChance;

		// Apply street-specific modifiers
		float streetMod = (currentStreet == Street.Preflop)
			? currentOpponent.PreflopAggression
			: currentOpponent.PostflopAggression;

		// Adjust aggression-based thresholds by street modifier
		// Low aggression increases threshold (harder to raise)
		// High aggression decreases threshold (easier to raise)
		raiseThreshold *= (2.0f - streetMod);
		
		// Clamp to prevent negative or excessive values
		raiseThreshold = Math.Clamp(raiseThreshold, 0.0f, 1.0f);

		GD.Print($"Thresholds - Fold: {foldThreshold:F2}, Call: {callThreshold:F2}, Raise: {raiseThreshold:F2}, Bluff: {bluffChance:F2}");

		// Adjust thresholds based on player betting patterns
		float playerStrength = EstimatePlayerStrength();
		GD.Print($"Estimated Player Strength: {playerStrength:F2}");

		// UPDATED: Only adjust thresholds if player is very aggressive AND we have weak hand
		if (playerStrength > 0.75f && handStrength < 0.55f)
		{
			// Only become more cautious with weak/medium hands
			foldThreshold += 0.05f;  // Reduced from 0.08
			raiseThreshold += 0.07f; // Reduced from 0.10
			raiseThreshold = Math.Clamp(raiseThreshold, 0.0f, 1.0f);
		}

		// Adjust based on bet size (pot odds consideration)
		if (facingBet && pot > 0)
		{
			float potOdds = (float)toCall / (pot + toCall);
			
			if (potOdds < 0.25f)
			{
				// Cheap to call; be more willing
				foldThreshold -= 0.08f;
				callThreshold -= 0.05f;
			}
			else if (potOdds >= 0.40f)  // Changed from 0.45
			{
				// Very expensive; need stronger hand
				foldThreshold += 0.20f;  // Increased from 0.12
				callThreshold += 0.15f;  // Increased from 0.10
				
				// Extra penalty if pot odds significantly exceed hand strength
				if (potOdds > handStrength + 0.10f)
				{
					foldThreshold += 0.10f;
					callThreshold += 0.10f;
				}
			}
			
			// Ensure fold threshold doesn't exceed call threshold
			foldThreshold = Math.Clamp(foldThreshold, 0.0f, callThreshold);
		}


		// Prevent infinite raising
		if (raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			// Can only call or fold at max raises
			if (facingBet)
			{
				return handStrength >= foldThreshold ? AIAction.Call : AIAction.Fold;
			}
			else
			{
				return AIAction.Check;
			}
		}

		// Decision logic
		if (facingBet)
		{
			// Facing a bet - decide fold/call/raise
			if (handStrength < foldThreshold)
			{
				// Check if we should bluff-raise instead
				if (GD.Randf() < bluffChance * 0.5f && raisesThisStreet < MAX_RAISES_PER_STREET)
				{
					aiBluffedThisHand = true;
					return AIAction.Raise;
				}
				return AIAction.Fold;
			}
			else if (handStrength < raiseThreshold)
			{
				// Occasionally bluff-raise with medium hands
				if (GD.Randf() < bluffChance * 0.3f && handStrength > callThreshold && raisesThisStreet < MAX_RAISES_PER_STREET)
				{
					aiBluffedThisHand = true;
					return AIAction.Raise;
				}
				return AIAction.Call;
			}
			else
			{
				// Strong hand - usually raise for value
				if (GD.Randf() < 0.7f && raisesThisStreet < MAX_RAISES_PER_STREET)
					return AIAction.Raise;
				else
					return AIAction.Call; // Slowplay occasionally
			}
		}
		else
		{
			// UPDATED: No bet to face - improved betting logic
			if (handStrength < callThreshold)
			{
				// Weak hand - mostly check, sometimes bluff
				if (GD.Randf() < bluffChance)
				{
					aiBluffedThisHand = true;
					return AIAction.Bet;
				}
				return AIAction.Check;
			}
			else if (handStrength < raiseThreshold)
			{
				// Medium hand - bet frequency scales with strength
				float betFrequency = (handStrength - callThreshold) / (raiseThreshold - callThreshold);
				betFrequency = Math.Clamp(betFrequency, 0.2f, 0.7f);
				return GD.Randf() < betFrequency ? AIAction.Bet : AIAction.Check;
			}
			else
			{
				// Strong hand - usually bet (increased from 0.8 to 0.85)
				return GD.Randf() < 0.85f ? AIAction.Bet : AIAction.Check;
			}
		}
	}

	private void ExecuteAIAction(AIAction action)
	{
		switch (action)
		{
			case AIAction.Fold:
				ShowMessage("Opponent folds");
				GD.Print("Opponent folds");
				playerChips += pot;
				pot = 0;
				handInProgress = false;
				waitingForNextGame = true;
				UpdateHud();
				break;

			case AIAction.Check:
				ShowMessage("Opponent checks");
				GD.Print("Opponent checks");
				break;

			case AIAction.Call:
				int toCall = currentBet - opponentBet;
				int actualCall = Math.Min(toCall, opponentChips);
				opponentChips -= actualCall;
				opponentBet += actualCall;
				pot += actualCall;

				if (opponentChips == 0)
				{
					opponentIsAllIn = true;
					ShowMessage($"Opponent calls {actualCall} chips (ALL-IN!)");
					GD.Print($"Opponent calls {actualCall} (ALL-IN)");
				}
				else
				{
					ShowMessage($"Opponent calls {actualCall} chips");
					GD.Print($"Opponent calls {actualCall}, Opponent stack: {opponentChips}, Pot: {pot}");
				}
				break;

			case AIAction.Bet:
				int betSize = CalculateAIBetSize();
				int actualBet = Math.Min(betSize, opponentChips);
				opponentChips -= actualBet;
				opponentBet += actualBet;
				pot += actualBet;
				currentBet = opponentBet;

				// DON'T increment raisesThisStreet for initial bet
				if (opponentChips == 0)
				{
					opponentIsAllIn = true;
					ShowMessage($"Opponent bets {actualBet} chips (ALL-IN!)");
					GD.Print($"Opponent bets {actualBet} (ALL-IN)");
				}
				else
				{
					ShowMessage($"Opponent bets {actualBet} chips");
					GD.Print($"Opponent bets {actualBet}");
				}
				break;

			case AIAction.Raise:
				int raiseSize = CalculateAIBetSize();
				int totalRaise = currentBet + raiseSize;
				int toAdd = totalRaise - opponentBet;
				int actualRaise = Math.Min(toAdd, opponentChips);
				opponentChips -= actualRaise;
				opponentBet += actualRaise;
				pot += actualRaise;
				currentBet = opponentBet;
				raisesThisStreet++; // Only increment on actual raise

				if (opponentChips == 0)
				{
					opponentIsAllIn = true;
					ShowMessage($"Opponent raises to {opponentBet} chips (ALL-IN!)");
					GD.Print($"Opponent raises to {opponentBet} (ALL-IN)");
				}
				else
				{
					ShowMessage($"Opponent raises to {opponentBet} chips");
					GD.Print($"Opponent raises to {opponentBet}");
				}
				break;
		}

		UpdateHud();
	}

	private int CalculateAIBetSize()
	{
		float handStrength = EvaluateAIHandStrength();
		int minBet = bigBlind;

		// Use opponent's bet sizing factor from profile
		float sizeFactor = currentOpponent.BetSizeFactor;

		// Reduce bluff sizes
		if (aiBluffedThisHand && handStrength < 0.4f)
			sizeFactor *= 0.65f;

		int betSize = (int)(pot * sizeFactor);

		// Ensure we have a valid bet size (at least minBet)
		betSize = Math.Max(betSize, minBet);

		// Cap at opponent's remaining chips
		int maxBet = Math.Max(minBet, opponentChips);
		return Math.Min(betSize, maxBet);
	}
	
	// UPDATED: Player strength estimation with softer adjustments and capped values
	private float EstimatePlayerStrength()
	{
		float strength = 0.5f;

		int bettingStreets = 0;
		foreach (var kvp in playerBetOnStreet)
			if (kvp.Value) bettingStreets++;

		// Softer multi-street bonus (was 0.12)
		strength += bettingStreets * 0.08f;

		if (playerBetOnStreet.ContainsKey(currentStreet) &&
			playerBetOnStreet[currentStreet] &&
			playerBetSizeOnStreet.ContainsKey(currentStreet))
		{
			int betSize = playerBetSizeOnStreet[currentStreet];
			int potBeforeBet = pot - playerBet - opponentBet;

			if (potBeforeBet > 0)
			{
				float betRatio = (float)betSize / potBeforeBet;

				// Reduced bonuses to account for bluff possibility
				if (betRatio > 1.0f) strength += 0.15f;       // Was 0.25
				else if (betRatio > 0.66f) strength += 0.10f; // Was 0.15
				else if (betRatio < 0.33f) strength -= 0.05f; // Was -0.08
			}
		}

		// Add randomness to prevent perfect exploitation
		strength += (GD.Randf() - 0.5f) * 0.1f; // ±0.05 variance

		return Math.Clamp(strength, 0.2f, 0.80f); // Cap at 0.80, not 1.0
	}
}
