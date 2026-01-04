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
		// 1. Safety Check: If we have no chips, we can't do anything but Check (if allowed) or effectively "Call" (All-In previously handled)
		// But usually we shouldn't be here if All-In.
		if (opponentChips <= 0) return AIAction.Check; // Or return logic to just skip turn

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

		raiseThreshold *= (2.0f - streetMod);
		raiseThreshold = Math.Clamp(raiseThreshold, 0.0f, 1.0f);

		GD.Print($"Thresholds - Fold: {foldThreshold:F2}, Call: {callThreshold:F2}, Raise: {raiseThreshold:F2}, Bluff: {bluffChance:F2}");

		float playerStrength = EstimatePlayerStrength();
		GD.Print($"Estimated Player Strength: {playerStrength:F2}");

		if (playerStrength > 0.75f && handStrength < 0.55f)
		{
			foldThreshold += 0.05f;
			raiseThreshold += 0.07f;
			raiseThreshold = Math.Clamp(raiseThreshold, 0.0f, 1.0f);
		}

		if (facingBet && pot > 0)
		{
			int actualCallAmount = Math.Min(toCall, opponentChips);
			float potOdds = (float)actualCallAmount / (pot + actualCallAmount);
			
			float stackCommitment = (opponentChips > 0) ? (float)actualCallAmount / opponentChips : 1.0f;
			
			if (potOdds < 0.25f && stackCommitment < 0.30f)
			{
				foldThreshold -= 0.08f;
				callThreshold -= 0.05f;
			}
			else if (potOdds >= 0.35f || stackCommitment > 0.40f) 
			{
				foldThreshold += 0.15f; 
				callThreshold += 0.10f;
				
				if (potOdds >= 0.45f || stackCommitment > 0.80f) {
					 foldThreshold += 0.10f; 
				}

				if (potOdds > 0.60f && potOdds > handStrength + 0.15f)
				{
					 foldThreshold += 0.08f;
				}
			}
			
			foldThreshold = Math.Clamp(foldThreshold, 0.0f, callThreshold);
		}

		if (raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			if (facingBet)
			{
				return handStrength >= foldThreshold ? AIAction.Call : AIAction.Fold;
			}
			else
			{
				return AIAction.Check;
			}
		}

		if (facingBet)
		{
			if (handStrength < foldThreshold)
			{
				bool isBadSpotToBluff = false;
				if (playerStrength > 0.70f) isBadSpotToBluff = true;
				float betRatio = (pot > 0) ? (float)toCall / pot : 0f;
				if (betRatio > 1.2f) isBadSpotToBluff = true; 

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
				if (handStrength >= 0.85f) raiseChance = 1.0f; 
				else if (handStrength >= 0.70f) raiseChance = 0.85f; 
				else raiseChance = 0.70f; 
				
				if (GD.Randf() < raiseChance && raisesThisStreet < MAX_RAISES_PER_STREET)
					return AIAction.Raise;
				else
					return AIAction.Call; 
			}
		}
		else
		{
			if (handStrength < callThreshold)
			{
				float aggressionFactor = (currentStreet == Street.Preflop) 
					? currentOpponent.PreflopAggression 
					: currentOpponent.PostflopAggression;

				if (aggressionFactor > 1.1f && GD.Randf() < 0.4f) 
				{
					aiBluffedThisHand = true;
					return AIAction.Bet;
				}

				if (GD.Randf() < bluffChance)
				{
					aiBluffedThisHand = true;
					return AIAction.Bet;
				}
				return AIAction.Check;
			}
			else if (handStrength < raiseThreshold)
			{
				float betFrequency = (handStrength - callThreshold) / (raiseThreshold - callThreshold);
				betFrequency = Math.Clamp(betFrequency, 0.2f, 0.7f);
				return GD.Randf() < betFrequency ? AIAction.Bet : AIAction.Check;
			}
			else
			{
				float betChance;
				if (handStrength >= 0.85f) betChance = 0.95f; 
				else if (handStrength >= 0.70f) betChance = 0.90f; 
				else betChance = 0.85f; 
				return GD.Randf() < betChance ? AIAction.Bet : AIAction.Check;
			}
		}
	}

	private void ExecuteAIAction(AIAction action)
	{
		// Double check chips before executing any betting action
		if (opponentChips <= 0 && (action == AIAction.Bet || action == AIAction.Raise))
		{
			// Fallback: If AI tries to bet with 0 chips, treat as Check (if checking allowed) or Call (All-In)
			// But ideally logic above prevents this. 
			action = AIAction.Check; 
		}

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
				
				AddToPot(false, actualCall);

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
				
				AddToPot(false, actualBet);
				
				currentBet = opponentBet;

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
				
				AddToPot(false, actualRaise);
				
				currentBet = opponentBet;
				raisesThisStreet++; 

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
		if (opponentChips <= 0) return 0; // Fix for 0-chip betting crash

		float handStrength = EvaluateAIHandStrength();
		int minBet = bigBlind;

		float sizeFactor = currentOpponent.BetSizeFactor;

		if (handStrength >= 0.85f) sizeFactor = Math.Max(sizeFactor, 0.8f);
		else if (handStrength >= 0.70f) sizeFactor = Math.Max(sizeFactor, 0.6f);
		else if (handStrength >= 0.55f) sizeFactor = Math.Max(sizeFactor, 0.5f);

		if (aiBluffedThisHand && handStrength < 0.4f)
			sizeFactor *= 0.65f;

		int betSize = (int)(pot * sizeFactor);
		betSize = Math.Max(betSize, minBet);
		int maxBet = Math.Max(minBet, opponentChips); // Ensure maxBet is valid
		
		return Math.Min(betSize, maxBet);
	}

	private float EstimatePlayerStrength()
	{
		float strength = 0.5f;

		int bettingStreets = 0;
		foreach (var kvp in playerBetOnStreet)
			if (kvp.Value) bettingStreets++;

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
				if (betRatio > 1.0f) strength += 0.15f;     
				else if (betRatio > 0.66f) strength += 0.10f; 
				else if (betRatio < 0.33f) strength -= 0.05f; 
			}
		}

		strength += (GD.Randf() - 0.5f) * 0.1f; 
		return Math.Clamp(strength, 0.2f, 0.80f); 
	}
}
