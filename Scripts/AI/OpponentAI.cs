// PokerGame.AI.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
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

		// Only adjust if player is very aggressive AND we have weak hand
		if (playerStrength > 0.75f && handStrength < 0.55f)
		{
			// Only become more cautious with weak/medium hands
			foldThreshold += 0.05f;
			raiseThreshold += 0.07f;
			raiseThreshold = Math.Clamp(raiseThreshold, 0.0f, 1.0f);
		}

		if (facingBet && pot > 0)
		{
			// FIX: Clamp 'toCall' to the actual chips we have. 
			// Calling 1000 chips when we only have 10 is mathematically cheap (100% of our stack, but tiny vs pot).
			int actualCallAmount = Math.Min(toCall, opponentChips);
			float potOdds = (float)actualCallAmount / (pot + actualCallAmount);
			
			// Safety Check: Commitment
			// If this call represents > 40% of our remaining stack (tournament life risk), treat it seriously
			// regardless of the pot odds math (e.g., calling an all-in for 10 chips into a 5 pot is "expensive" for the stack)
			float stackCommitment = (opponentChips > 0) ? (float)actualCallAmount / opponentChips : 1.0f;
			
			if (potOdds < 0.25f && stackCommitment < 0.30f)
			{
				// Cheap to call AND not risking our life; be more willing
				foldThreshold -= 0.08f;
				callThreshold -= 0.05f;
			}
			else if (potOdds >= 0.35f || stackCommitment > 0.40f) 
			{
				// Expensive (bad odds OR risks >40% of stack); need stronger hand
				foldThreshold += 0.15f; 
				callThreshold += 0.10f;
				
				// Gradient: If it's REALLY expensive (close to 0.50 odds or All-In), add more
				if (potOdds >= 0.45f || stackCommitment > 0.80f) {
					 foldThreshold += 0.10f; // Cumulative penalty
				}

				// Extreme penalty for terrible odds
				if (potOdds > 0.60f && potOdds > handStrength + 0.15f)
				{
					 foldThreshold += 0.08f;
				}
			}
			
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
				// === SMART BLUFF LOGIC ===
				bool isBadSpotToBluff = false;

				// 1. Don't bluff if the player looks terrified strong
				if (playerStrength > 0.70f) isBadSpotToBluff = true;

				// 2. Don't bluff if we have to call a massive overbet just to try it
				// (e.g. Player bets 190 into 80)
				float betRatio = (pot > 0) ? (float)toCall / pot : 0f;
				if (betRatio > 1.2f) isBadSpotToBluff = true; 

				// 3. The Logic
				if (!isBadSpotToBluff && 
					GD.Randf() < bluffChance * 0.5f && 
					raisesThisStreet < MAX_RAISES_PER_STREET)
				{
					aiBluffedThisHand = true;
					GD.Print("AI attempting bluff raise!"); 
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
				float raiseChance;
				
				if (handStrength >= 0.85f)
				{
					raiseChance = 1.0f; // Always raise with near-nuts (full house, quads, straight flush)
				}
				else if (handStrength >= 0.70f)
				{
					raiseChance = 0.85f; // Usually raise with very strong hands (trips, strong two pair)
				}
				else
				{
					raiseChance = 0.70f; // Often raise with strong hands (two pair, weaker trips)
				}
				
				if (GD.Randf() < raiseChance && raisesThisStreet < MAX_RAISES_PER_STREET)
				{
					return AIAction.Raise;
				}
				else
				{
					return AIAction.Call; // Slowplay occasionally with medium-strong hands
				}
			}
		}
		else
		{
			if (handStrength < callThreshold)
			{
				// Weak hand (Air)
				// === STAB LOGIC ===
				// If I'm aggressive and checked to, sometimes bet just to steal
				float aggressionFactor = (currentStreet == Street.Preflop) 
					? currentOpponent.PreflopAggression 
					: currentOpponent.PostflopAggression;

				// If I'm aggressive (> 1.0) and random roll hits, BET anyway
				if (aggressionFactor > 1.1f && GD.Randf() < 0.4f) 
				{
					aiBluffedThisHand = true;
					return AIAction.Bet;
				}

				// Normal Bluff logic
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
				float betChance;
				
				if (handStrength >= 0.85f)
				{
					betChance = 0.95f; // Almost always bet with monsters
				}
				else if (handStrength >= 0.70f)
				{
					betChance = 0.90f; // Usually bet with very strong hands
				}
				else
				{
					betChance = 0.85f; // Often bet with strong hands
				}
				
				return GD.Randf() < betChance ? AIAction.Bet : AIAction.Check;
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
				EndHand(); 
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

		// Use opponent's bet sizing factor from profile as BASE
		float sizeFactor = currentOpponent.BetSizeFactor;

		//  Scale up bet size with hand strength
		if (handStrength >= 0.85f)
		{
			// Monster hands: bet 80-100% of pot regardless of profile
			sizeFactor = Math.Max(sizeFactor, 0.8f);
		}
		else if (handStrength >= 0.70f)
		{
			// Very strong hands: bet 60-80% of pot
			sizeFactor = Math.Max(sizeFactor, 0.6f);
		}
		else if (handStrength >= 0.55f)
		{
			// Strong hands: bet at least 50% of pot
			sizeFactor = Math.Max(sizeFactor, 0.5f);
		}
		
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
