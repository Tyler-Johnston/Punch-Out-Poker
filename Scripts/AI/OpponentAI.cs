// PokerGame.AI.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PokerGame
{
	private bool aiBluffedThisHand = false;
	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();
	private int playerTotalBetsThisHand = 0;

	// ========== ACTION HISTORY & TRACKING ==========
	private List<PlayerAction> actionHistory = new List<PlayerAction>();
	
	private struct PlayerAction
	{
		public Street Street;
		public string ActionType;
		public int Amount;
		public int PotSize;
		public bool WasAllIn;
		public int HandNumber;
	}
	
	private int currentHandNumber = 0;
	
	// ========== FREQUENCY EXPLOITATION TRACKING ==========
	private int handsPlayed = 0;
	private int playerPreflopRaises = 0;
	private int playerPreflopFolds = 0;

	// ==========  EQUITY CALCULATION SETTINGS ==========
	private const int DEFAULT_SIMULATIONS = 1000;
	private const int PREFLOP_SIMULATIONS = 500;  // Faster preflop
	private const int CRITICAL_SIMULATIONS = 1500; // Deep analysis for all-ins

	// ===========================================================================================
	// MAIN DECISION METHOD
	// ===========================================================================================
	
	private AIAction DecideAIAction()
	{
		if (opponentChips <= 0) return AIAction.Check;

		float effectiveBB = (float)opponentChips / bigBlind;
		int toCall = currentBet - opponentBet;
		bool facingBet = toCall > 0;

		GD.Print($"\n========== AI DECISION START ==========");
		GD.Print($"Street: {currentStreet}, Facing Bet: {facingBet}, To Call: {toCall}");
		GD.Print($"Effective Stack: {effectiveBB:F1}BB, Pot: {pot}");

		// ===== STAGE 1: Ultra-Short Stack Nash Equilibrium =====
		if (NashEquilibrium.ShouldUseNash(effectiveBB, currentStreet == Street.Preflop, facingBet))
		{
			string handString = HandNotation.GetHandString(opponentHand);
			if (NashEquilibrium.HandExistsInTable(handString))
			{
				GD.Print($"[NASH MODE] Stack {effectiveBB:F1}BB ≤ 12BB - Using Nash equilibrium");
				return DecideWithNash(effectiveBB, handString);
			}
		}

		// ===== STAGE 2: Calculate True Equity via Monte Carlo =====
		List<string> villainRange = EstimateVillainRange();
		int simCount = GetSimulationCount(currentStreet, effectiveBB, facingBet);
		float equity = CalculateEquity(opponentHand, communityCards, villainRange, simCount);
		
		GD.Print($"[EQUITY] {equity:P1} equity vs estimated range ({villainRange.Count} hands, {simCount} sims)");

		// ===== STAGE 3: Get GTO Baseline Threshold =====
		float spr = (pot > 0) ? (float)Math.Min(playerChips, opponentChips) / pot : 999f;
		float gtoThreshold = GetGTOThreshold(currentStreet, facingBet, spr);
		GD.Print($"[GTO] Base threshold: {gtoThreshold:P1} (SPR: {spr:F2})");

		// ===== STAGE 4: Profile Adjustment =====
		float profileAdjustment = (0.50f - currentOpponent.Looseness) * 0.40f;
		gtoThreshold += profileAdjustment;
		GD.Print($"[PROFILE] {currentOpponent.Name} looseness {currentOpponent.Looseness:F2} -> Threshold {(profileAdjustment >= 0 ? "+" : "")}{profileAdjustment:F3}");

		// ===== STAGE 5: Frequency Exploitation =====
		float raiseFrequency = (handsPlayed > 5) ? (float)playerPreflopRaises / handsPlayed : 0.5f;
		
		if (raiseFrequency > 0.65f && currentStreet == Street.Preflop)
		{
			float exploit = (raiseFrequency - 0.65f) * 1.0f; // Max -35% for 100% raiser
			gtoThreshold -= exploit;
			GD.Print($"[EXPLOIT] Player raises {raiseFrequency:P0} -> Threshold -{exploit:F3} (MANIAC DETECTED)");
		}
		
		// ===== STAGE 5.5: Consecutive Aggression Detection =====
		if (currentStreet == Street.Preflop && facingBet)
		{
			// Count player raises in last 5 hands
			var recentHands = actionHistory
				.Where(a => a.Street == Street.Preflop && 
							a.HandNumber > currentHandNumber - 5 && 
							a.HandNumber < currentHandNumber)
				.ToList();
			
			int consecutiveRaises = recentHands.Count(a => a.ActionType == "Raise" || a.ActionType == "Bet");
			
			if (consecutiveRaises >= 3)
			{
				// Player is running a 3-bet blitz - widen calling range dramatically
				float blitzExploit = Math.Min(consecutiveRaises * 0.07f, 0.25f); // Max -25%
				gtoThreshold -= blitzExploit;
				GD.Print($"[AGGRESSION BLITZ] {consecutiveRaises} raises in last 5 hands -> Threshold -{blitzExploit:F3}");
				GD.Print($"[EXPLOITATION] Player likely overbluffing - widening defense range");
			}
		}

		// ===== STAGE 6: Opponent Strength Adjustment =====
		float playerStrength = EstimatePlayerStrength();
		if (playerStrength > 0.75f && facingBet)
		{
			float strengthAdj = (playerStrength - 0.75f) * 0.40f;
			gtoThreshold += strengthAdj;
			GD.Print($"[OPPONENT] Strong action detected ({playerStrength:P1}) -> Threshold +{strengthAdj:F3}");
		}
		
		// ===== STAGE 6.5: POT ODDS OVERRIDE =====
		if (facingBet && toCall > 0)
		{
			int potAfterCall = pot + toCall;
			float potOddsRequired = (float)toCall / potAfterCall;
			float potOddsThreshold = potOddsRequired + 0.05f; // Add 5% margin for implied odds
			
			GD.Print($"[POT ODDS] Required: {potOddsRequired:P1}, Current threshold: {gtoThreshold:P1}");
			
			// If pot odds are better than threshold, use pot odds instead
			if (potOddsThreshold < gtoThreshold)
			{
				gtoThreshold = potOddsThreshold;
				GD.Print($"[POT ODDS OVERRIDE] Lowering threshold to {gtoThreshold:P1} (getting good price)");
			}
		}

		gtoThreshold = Math.Clamp(gtoThreshold, 0.15f, 0.90f);
		GD.Print($"[FINAL THRESHOLD] {gtoThreshold:P1}");

		// ===== STAGE 7: All-In Decision Check =====
		if (facingBet && toCall > 0)
		{
			float commitment = (opponentChips > 0) ? (float)toCall / opponentChips : 1.0f;
			bool isAllInScenario = (commitment > 0.75f) || (spr < 2.5f) || (toCall >= opponentChips);
			
			if (isAllInScenario)
			{
				GD.Print($"[ALL-IN SCENARIO] Commitment: {commitment:P0}, SPR: {spr:F2}");
				return DecideAllInCall(equity, gtoThreshold, spr);
			}
		}

		// ===== STAGE 8: Make Decision =====
		AIAction decision = MakeDecision(equity, gtoThreshold, facingBet);
		GD.Print($"[DECISION] {decision} (Equity {equity:P1} vs Threshold {gtoThreshold:P1})");
		GD.Print($"========== AI DECISION END ==========\n");
		
		return decision;
	}

	// ===========================================================================================
	// NASH EQUILIBRIUM PUSH/FOLD
	// ===========================================================================================

	private AIAction DecideWithNash(float effectiveBB, string handString)
	{
		bool shouldCall = NashEquilibrium.ShouldCallPush(handString, effectiveBB);
		return shouldCall ? AIAction.Call : AIAction.Fold;
	}

	// ===========================================================================================
	// MONTE CARLO EQUITY CALCULATOR
	// ===========================================================================================
	
	private float CalculateEquity(List<Card> heroHand, List<Card> board, List<string> villainRange, int simulations)
	{
		if (heroHand == null || heroHand.Count != 2)
		{
			GD.PrintErr("[Equity] Invalid hero hand");
			return 0.35f;
		}

		int wins = 0;
		int ties = 0;
		int validSims = 0;

		for (int i = 0; i < simulations; i++)
		{
			// Create deck and remove known cards
			List<Card> deck = Deck.CreateCardList();
			Deck.RemoveCardsFromDeck(deck, heroHand);
			Deck.RemoveCardsFromDeck(deck, board);

			// Sample villain hand from their range using RangeSampler
			List<Card> villainHand = RangeSampler.SampleHandFromRange(deck, villainRange);
			if (villainHand == null || villainHand.Count != 2) continue;
			
			Deck.RemoveCardsFromDeck(deck, villainHand);

			// Complete the board to 5 cards
			List<Card> simulatedBoard = new List<Card>(board);
			while (simulatedBoard.Count < 5)
			{
				int randomIndex = (int)(GD.Randf() * deck.Count);
				simulatedBoard.Add(deck[randomIndex]);
				deck.RemoveAt(randomIndex);
			}

			// Evaluate both hands using HandEvaluator (lower = better)
			int heroScore = HandEvaluator.Evaluate7Cards(heroHand, simulatedBoard);
			int villainScore = HandEvaluator.Evaluate7Cards(villainHand, simulatedBoard);

			// Lower rank = better hand in pheval
			if (heroScore < villainScore) wins++;
			else if (heroScore == villainScore) ties++;
			
			validSims++;
		}

		if (validSims == 0)
		{
			GD.PrintErr("[Equity] No valid simulations!");
			return 0.35f;
		}

		float equity = (wins + (ties * 0.5f)) / validSims;
		return Math.Clamp(equity, 0.01f, 0.99f);
	}

	private int GetSimulationCount(Street street, float effectiveBB, bool facingBet)
	{
		if (street == Street.Preflop) return PREFLOP_SIMULATIONS;
		if (facingBet && effectiveBB < 10f) return CRITICAL_SIMULATIONS;
		return DEFAULT_SIMULATIONS;
	}

	private List<string> EstimateVillainRange()
	{
		float playerStrength = EstimatePlayerStrength();
		float raiseFreq = (handsPlayed > 5) ? (float)playerPreflopRaises / handsPlayed : 0.5f;
		int toCall = currentBet - opponentBet;

		if (currentStreet == Street.Preflop)
		{
			if (raiseFreq > 0.85f)
			{
				GD.Print($"[Range] Maniac detected ({raiseFreq:P0}) -> 85% range");
				return new List<string>(HandRanges.ManiacRange);
			}
			else if (raiseFreq > 0.60f)
			{
				GD.Print($"[Range] Loose player ({raiseFreq:P0}) -> 50% range");
				return new List<string>(HandRanges.LooseRange);
			}
			else if (raiseFreq < 0.30f)
			{
				GD.Print($"[Range] Tight player ({raiseFreq:P0}) -> 15% range");
				return new List<string>(HandRanges.TightRange);
			}
			else
			{
				GD.Print($"[Range] Balanced player ({raiseFreq:P0}) -> 35% range");
				return new List<string>(HandRanges.BalancedRange);
			}
		}
		else // Postflop
		{
			bool isPlayerAllIn = (toCall >= playerChips); // Player has no chips left
			bool isLargeRiverBet = (toCall >= pot * 0.75f); // Overbet or pot-sized river bet
			bool isRiverShove = (currentStreet == Street.River && toCall > 0 && 
								 (isPlayerAllIn || isLargeRiverBet));
			
			if (isRiverShove)
			{
				// Apply profile-based adjustment: tight players' river shoves are EXTRA credible
				float tightnessMultiplier = 1.0f - (currentOpponent.Looseness * 0.5f);
				
				GD.Print($"[Range] River shove detected (player all-in: {isPlayerAllIn}, large bet: {isLargeRiverBet})");
				GD.Print($"[Range] Profile tightness factor: {tightnessMultiplier:F2}x");
				
				// For very tight players (< 0.45 looseness), use ONLY the ultra-narrow range
				if (currentOpponent.Looseness < 0.45f)
				{
					GD.Print($"[Range] Tight opponent river shove -> ultra-narrow range (~10% hands)");
					return new List<string>(HandRanges.RiverShoveRange);
				}
				else
				{
					// For looser players, still use narrow range but not ultra-narrow
					GD.Print($"[Range] Standard river shove -> narrow range (~25% hands)");
					return new List<string>(HandRanges.StrongPostflopRange);
				}
			}
			
			// Regular postflop logic
			if (playerStrength > 0.75f)
			{
				GD.Print($"[Range] Strong postflop action ({playerStrength:P0}) -> narrow range");
				return new List<string>(HandRanges.StrongPostflopRange);
			}
			else if (playerStrength > 0.55f)
			{
				GD.Print($"[Range] Medium postflop action ({playerStrength:P0}) -> medium range");
				return new List<string>(HandRanges.MediumPostflopRange);
			}
			else
			{
				GD.Print($"[Range] Weak postflop action ({playerStrength:P0}) -> wide range");
				return new List<string>(HandRanges.WeakPostflopRange);
			}
		}
	}

	// ===========================================================================================
	// GTO THRESHOLD SYSTEM 
	// ===========================================================================================
	
	private float GetGTOThreshold(Street street, bool facingBet, float spr)
	{
		if (street == Street.Preflop)
		{
			if (facingBet)
			{
				if (spr < 3.0f) return 0.42f; // Short stack: wider calls
				if (spr < 7.0f) return 0.48f; // Medium stack
				return 0.52f; // Deep stack
			}
			else
			{
				return 0.42f; // Can open 58% of hands
			}
		}
		else if (street == Street.Flop)
		{
			return facingBet ? 0.55f : 0.48f;
		}
		else if (street == Street.Turn)
		{
			return facingBet ? 0.60f : 0.52f;
		}
		else // River
		{
			return facingBet ? 0.50f : 0.48f; // Pot odds driven
		}
	}

	// ===========================================================================================
	// DECISION LOGIC
	// ===========================================================================================
	
	private AIAction DecideAllInCall(float equity, float threshold, float spr)
	{
		// SPR adjustments
		if (spr < 2.0f)
		{
			threshold -= 0.05f; // More willing to commit
			GD.Print($"[ALL-IN] Low SPR adjustment: -{0.05f}");
		}
		else if (spr > 6.0f)
		{
			threshold += 0.10f; // Need stronger hand deep
			GD.Print($"[ALL-IN] High SPR adjustment: +{0.10f}");
		}

		threshold = Math.Clamp(threshold, 0.15f, 0.85f);

		if (equity >= threshold)
		{
			GD.Print($"[ALL-IN] ✓ CALLING - Equity {equity:P1} ≥ Threshold {threshold:P1}");
			return AIAction.Call;
		}
		else
		{
			GD.Print($"[ALL-IN] ✗ FOLDING - Equity {equity:P1} < Threshold {threshold:P1}");
			return AIAction.Fold;
		}
	}

	private AIAction MakeDecision(float equity, float threshold, bool facingBet)
	{
		if (raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			if (facingBet)
				return equity >= threshold ? AIAction.Call : AIAction.Fold;
			else
				return AIAction.Check;
		}

		if (facingBet)
		{
			// Facing a bet
			if (equity < threshold * 0.75f)
			{
				// Weak hand - very rarely bluff (only if close to threshold)
				if (equity >= threshold * 0.70f && GD.Randf() < currentOpponent.BluffChance * 0.3f && raisesThisStreet < MAX_RAISES_PER_STREET)
				{
					aiBluffedThisHand = true;
					GD.Print("[BLUFF] Bluff-raising with weak hand");
					return AIAction.Raise;
				}
				return AIAction.Fold;
			}
			else if (equity < threshold)
			{
				// Below threshold but close - mostly fold, occasionally bluff
				if (GD.Randf() < currentOpponent.BluffChance * 0.25f)
				{
					aiBluffedThisHand = true;
					GD.Print("[BLUFF] Bluff-raising near threshold");
					return AIAction.Raise;
				}
				return AIAction.Fold;
			}
			else if (equity < threshold + 0.15f)
			{
				// Above threshold but not strong - mostly call
				if (GD.Randf() < 0.30f && raisesThisStreet < MAX_RAISES_PER_STREET)
					return AIAction.Raise;
				return AIAction.Call;
			}
			else
			{
				// Strong hand - raise frequently
				float raiseChance = equity >= 0.80f ? 0.90f : 0.75f;
				if (GD.Randf() < raiseChance && raisesThisStreet < MAX_RAISES_PER_STREET)
					return AIAction.Raise;
				return AIAction.Call;
			}
		}
		else
		{
			// No bet facing - decide bet/check
			if (equity < threshold * 0.85f)
			{
				// FIXED: Only bluff if equity is reasonably close to threshold
				if (equity >= threshold * 0.75f && GD.Randf() < currentOpponent.BluffChance * 0.5f)
				{
					aiBluffedThisHand = true;
					GD.Print("[BLUFF] Bluff-betting marginal hand");
					return AIAction.Bet;
				}
				return AIAction.Check; // Don't bet with air
			}
			else if (equity < threshold + 0.10f)
			{
				// Medium strength - mixed strategy
				float betFreq = (equity - threshold * 0.85f) / (threshold + 0.10f - threshold * 0.85f);
				return GD.Randf() < betFreq * 0.65f ? AIAction.Bet : AIAction.Check;
			}
			else
			{
				// Strong hand - bet frequently
				float equityMargin = equity - threshold;
				float betChance;
				
				if (equityMargin > 0.20f) // 20%+ above threshold
				{
					betChance = 0.98f; // Almost always bet
					GD.Print($"[VALUE] Huge equity advantage ({equityMargin:P0}) -> betting {betChance:P0}");
				}
				else if (equity >= 0.80f)
				{
					betChance = 0.95f;
				}
				else
				{
					betChance = 0.85f;
				}
					
				return GD.Randf() < betChance ? AIAction.Bet : AIAction.Check;
			}
		}
	}

	// ===========================================================================================
	// OPPONENT MODELING
	// ===========================================================================================
	
	private float EstimatePlayerStrength()
	{
		var currentHandActions = actionHistory.Where(a => a.HandNumber == currentHandNumber).ToList();
		
		if (currentHandActions.Count < 1)
		{
			if (actionHistory.Count > 10)
				return EstimatePlayerStrengthFromHistory();
			return 0.40f;
		}
		
		float totalWeight = 0f;
		float weightedSum = 0f;
		
		for (int i = 0; i < currentHandActions.Count; i++)
		{
			var action = currentHandActions[i];
			float weight = Mathf.Pow(0.85f, currentHandActions.Count - i - 1);
			float actionStrength = EvaluateActionStrength(action);
			weightedSum += actionStrength * weight;
			totalWeight += weight;
		}
		
		float strength = weightedSum / totalWeight;
		
		var currentStreetActions = currentHandActions.Where(a => a.Street == currentStreet).ToList();
		if (currentStreetActions.Count > 0)
		{
			float currentStreetStrength = 0f;
			foreach (var action in currentStreetActions)
			{
				currentStreetStrength += EvaluateActionStrength(action);
			}
			currentStreetStrength /= currentStreetActions.Count;
			strength = (strength * 0.4f) + (currentStreetStrength * 0.6f);
		}
		
		strength += (GD.Randf() - 0.5f) * 0.08f;
		
		// Maniac discount (enhanced)
		float raiseFrequency = (handsPlayed > 5) ? (float)playerPreflopRaises / handsPlayed : 0.5f;
		if (raiseFrequency > 0.85f && currentStreet == Street.Preflop)
		{
			float discount = (raiseFrequency - 0.85f) * 1.2f;
			strength -= discount;
			GD.Print($"[Maniac Discount] {raiseFrequency:P0} -> Strength -{discount:F2}");
		}
		
		return Math.Clamp(strength, 0.20f, 0.85f);
	}

	private float EvaluateActionStrength(PlayerAction action)
	{
		float betRatio = (action.PotSize > 0) ? (float)action.Amount / action.PotSize : 0f;
		
		switch (action.ActionType)
		{
			case "Fold": return 0.20f;
			case "Check": return action.Street == Street.River ? 0.35f : 0.42f;
			case "Call":
				if (action.WasAllIn) return 0.75f;
				if (betRatio > 0.8f) return 0.65f;
				if (betRatio > 0.4f) return 0.58f;
				return 0.50f;
			case "Bet":
				if (action.WasAllIn) return 0.88f;
				if (betRatio > 1.2f) return 0.82f;
				if (betRatio > 0.75f) return 0.78f;
				if (betRatio > 0.50f) return 0.70f;
				return 0.62f;
			case "Raise":
			case "AllIn":
				if (action.WasAllIn) return 0.92f;
				if (betRatio > 1.5f) return 0.88f;
				if (betRatio > 0.8f) return 0.80f;
				return 0.75f;
			default: return 0.50f;
		}
	}

	private float EstimatePlayerStrengthFromHistory()
	{
		if (actionHistory.Count == 0) return 0.40f;
		
		var recentActions = actionHistory.Skip(Math.Max(0, actionHistory.Count - 30)).ToList();
		float totalStrength = 0f;
		foreach (var action in recentActions)
		{
			totalStrength += EvaluateActionStrength(action);
		}
		
		float avgStrength = totalStrength / recentActions.Count;
		avgStrength = (avgStrength * 0.7f) + (0.45f * 0.3f);
		return Math.Clamp(avgStrength, 0.30f, 0.70f);
	}

	public void TrackPlayerAction(string actionType, int amount, bool wasAllIn)
	{
		actionHistory.Add(new PlayerAction
		{
			Street = currentStreet,
			ActionType = actionType,
			Amount = amount,
			PotSize = pot,
			WasAllIn = wasAllIn,
			HandNumber = currentHandNumber
		});
		
		if (currentStreet == Street.Preflop)
		{
			if (actionType == "Raise") playerPreflopRaises++;
			else if (actionType == "Fold") playerPreflopFolds++;
		}
		
		if (actionHistory.Count > 150)
		{
			actionHistory.RemoveRange(0, 50);
		}
	}

	public void StartNewHandTracking()
	{
		currentHandNumber++;
		handsPlayed++;
		aiBluffedThisHand = false;
		GD.Print($"\n[NEW HAND] #{currentHandNumber} (Total played: {handsPlayed})");
	}

	// ===========================================================================================
	// EXECUTION & BET SIZING
	// ===========================================================================================
	
	private void ExecuteAIAction(AIAction action)
	{
		if (opponentChips <= 0 && (action == AIAction.Bet || action == AIAction.Raise))
		{
			action = AIAction.Check;
		}

		switch (action)
		{
			case AIAction.Fold:
				ShowMessage("Opponent folds");
				playerChips += pot;
				EndHand();
				break;

			case AIAction.Check:
				ShowMessage("Opponent checks");
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
				}
				else
				{
					ShowMessage($"Opponent calls {actualCall} chips");
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
				}
				else
				{
					ShowMessage($"Opponent bets {actualBet} chips");
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
				}
				else
				{
					ShowMessage($"Opponent raises to {opponentBet} chips");
				}
				break;
		}

		UpdateHud();
	}

	private int CalculateAIBetSize()
	{
		if (opponentChips <= 0) return 0;

		int minBet = bigBlind;
		float sizeFactor = currentOpponent.BetSizeFactor;

		// Adjust based on street and situation
		if (currentStreet == Street.River && aiBluffedThisHand)
			sizeFactor *= 0.65f;

		int betSize = (int)(pot * sizeFactor);
		betSize = Math.Max(betSize, minBet);
		int maxBet = Math.Max(minBet, opponentChips);
		
		return Math.Min(betSize, maxBet);
	}
}
