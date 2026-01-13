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

	// ========== EQUITY CALCULATION SETTINGS ==========
	private const int DEFAULT_SIMULATIONS = 1000;
	private const int PREFLOP_SIMULATIONS = 500;  // Faster preflop
	private const int CRITICAL_SIMULATIONS = 1500; // Deep analysis for all-ins

	// ========== MISTAKE SYSTEM ==========
	private MistakeType currentMistake = MistakeType.None;

	private enum MistakeType
	{
		None,
		Overfold,      // Folding medium-strength hands under pressure
		Overcall,      // Calling with weak hands
		BadBluff,      // Bluffing in bad spots
		MissedValue    // Not betting strong hands
	}

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
		UpdateOpponentDialogue(equity * 100f, false);

		// ===== STAGE 3: Get GTO Baseline Threshold =====
		float spr = (pot > 0) ? (float)Math.Min(playerChips, opponentChips) / pot : 999f;
		float gtoThreshold = GetGTOThreshold(currentStreet, facingBet, spr);
		GD.Print($"[GTO] Base threshold: {gtoThreshold:P1} (SPR: {spr:F2})");

		// ===== STAGE 4: Profile Adjustment =====
		float profileAdjustment = (0.50f - currentOpponent.Looseness) * 0.60f;
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
			float potOddsThreshold = potOddsRequired * 1.2f;

			GD.Print($"[POT ODDS] Required: {potOddsRequired:P1}, Current threshold: {gtoThreshold:P1}");

			if (currentOpponent.Looseness < 0.50f)
			{
				potOddsThreshold = potOddsRequired * 1.5f; // Add 50% margin
				GD.Print($"[POT ODDS] Loose player bonus - extra forgiving");
			}
			// If pot odds are better than threshold, use pot odds instead
			if (potOddsThreshold < gtoThreshold)
			{
				gtoThreshold = potOddsThreshold;
				GD.Print($"[POT ODDS OVERRIDE] Lowering threshold to {gtoThreshold:P1} (getting good price)");
			}
		}

		// ===== STAGE 7: MISTAKEFACTOR ADJUSTMENTS =====
		float finalThreshold = gtoThreshold;
		MistakeType appliedMistake = MistakeType.None;

		if (currentOpponent.MistakeFactor > 1.0f)
		{
			(finalThreshold, appliedMistake) = ApplyMistakeAdjustments(
				gtoThreshold, 
				equity, 
				facingBet, 
				currentOpponent.MistakeFactor,
				spr
			);

			GD.Print($"[MISTAKE] Type: {appliedMistake}, Adjusted threshold: {gtoThreshold:P1} → {finalThreshold:P1}");
		}
		else
		{
			finalThreshold = gtoThreshold;
		}

		// Store mistake for use in MakeDecision
		currentMistake = appliedMistake;

		finalThreshold = Math.Clamp(finalThreshold, 0.15f, 0.90f);
		GD.Print($"[FINAL THRESHOLD] {finalThreshold:P1}");
		

		// ===== STAGE 8: All-In Decision Check =====
		if (facingBet && toCall > 0)
		{
			float commitment = (opponentChips > 0) ? (float)toCall / opponentChips : 1.0f;
			bool isAllInScenario = (commitment > 0.75f) || (spr < 2.5f) || (toCall >= opponentChips);

			if (isAllInScenario)
			{
				GD.Print($"[ALL-IN SCENARIO] Commitment: {commitment:P0}, SPR: {spr:F2}");
				return DecideAllInCall(equity, finalThreshold, spr);
			}
		}

		// ===== STAGE 9: Make Decision =====
		AIAction decision = MakeDecision(equity, finalThreshold, facingBet, currentMistake);
		GD.Print($"[DECISION] {decision} (Equity {equity:P1} vs Threshold {finalThreshold:P1})");
		GD.Print($"========== AI DECISION END ==========\n");
		UpdateOpponentDialogue(equity * 100f, aiBluffedThisHand);

		return decision;
	}

	// ===========================================================================================
	// MISTAKE FACTOR SYSTEM
	// ===========================================================================================

	private (float adjustedThreshold, MistakeType mistake) ApplyMistakeAdjustments(
		float baseThreshold, 
		float equity, 
		bool facingBet, 
		float mistakeFactor,
		float spr)
	{
		// Normalize mistakeFactor: 1.0 = no mistakes, 1.5 = heavy amateur
		float mistakeIntensity = Math.Clamp(mistakeFactor - 1.0f, 0.0f, 0.5f);

		GD.Print($"[MISTAKE DEBUG] MistakeFactor={mistakeFactor:F2}, Intensity={mistakeIntensity:F2}");
		// Premium hands are mostly protected from mistakes
		bool isPremiumHand = equity > 0.80f;
		if (isPremiumHand)
		{
			mistakeIntensity *= 0.25f; // Reduce mistake chance by 75%
			GD.Print($"[MISTAKE] Premium hand detected - mistake intensity reduced to {mistakeIntensity:F2}");
		}

		float equityRatio = equity / baseThreshold;
		float adjustedThreshold = baseThreshold;
		MistakeType mistake = MistakeType.None;
		
		GD.Print($"[MISTAKE DEBUG] Equity={equity:P1}, Threshold={baseThreshold:P1}, Ratio={equityRatio:F2}");
		GD.Print($"[MISTAKE DEBUG] FacingBet={facingBet}, SPR={spr:F2}");

		if (facingBet)
		{
			// DEFENSIVE MISTAKES when facing aggression

			// Pattern 1: OVERFOLD medium-strength hands (scared money)
			// Trigger: equity is 0.8x to 1.15x threshold (close to break-even)
			if (equityRatio >= 0.80f && equityRatio <= 1.15f)
			{
				float overfoldChance = mistakeIntensity * 0.60f; // Max 30% at MF=1.5

				// More likely on turn/river (pressure builds)
				if (currentStreet == Street.Turn) overfoldChance *= 1.3f;
				if (currentStreet == Street.River) overfoldChance *= 1.5f;

				// More likely with larger bets (intimidation factor)
				int toCall = currentBet - opponentBet;
				float betSizeRatio = (pot > 0) ? (float)toCall / pot : 0f;
				if (betSizeRatio > 0.75f) overfoldChance *= 1.4f;
				
				float randomRoll = GD.Randf();
				GD.Print($"[MISTAKE DEBUG] OVERFOLD: Chance={overfoldChance:P1}, Roll={randomRoll:F3}, Trigger={randomRoll < overfoldChance}");
			

				if (randomRoll < overfoldChance)
				{
					// Increase threshold to make folding more likely
					float increase = mistakeIntensity * 0.15f; // Max +7.5% at MF=1.5
					adjustedThreshold = baseThreshold * (1.0f + increase);
					mistake = MistakeType.Overfold;
					GD.Print($"[MISTAKE] Overfold pattern - scared money (chance: {overfoldChance:P1})");
				}
			}

			// Pattern 2: OVERCALL weak hands (calling station / curious)
			// Trigger: equity is < 0.75x threshold (clearly bad call)
			else if (equityRatio < 0.85f)
			{
				float overcallChance = mistakeIntensity * 0.55f;

				// More likely on flop (wants to "see what happens")
				if (currentStreet == Street.Flop) overcallChance *= 2.0f;
				
				if (currentStreet == Street.Turn) overcallChance *= 2.0f; // NEW

				// More likely with smaller bets (cheaper to be curious)
				int toCall = currentBet - opponentBet;
				float betSizeRatio = (pot > 0) ? (float)toCall / pot : 0f;
				if (betSizeRatio < 0.50f) overcallChance *= 1.6f;

				// Less likely if very deep stacked (too expensive)
				if (spr > 8.0f) overcallChance *= 0.6f;
				
				float randomRoll = GD.Randf();
				GD.Print($"[MISTAKE DEBUG] OVERCALL: Chance={overcallChance:P1}, Roll={randomRoll:F3}, Trigger={randomRoll < overcallChance}");

				if (randomRoll < overcallChance)
				{
					// Decrease threshold to make calling more likely
					float decrease = mistakeIntensity * 0.20f; // Max -10% at MF=1.5
					adjustedThreshold = baseThreshold * (1.0f - decrease);
					mistake = MistakeType.Overcall;
					GD.Print($"[MISTAKE] Overcall pattern - calling station (chance: {overcallChance:P1})");
				}
			}
		}
		else
		{
			// AGGRESSIVE MISTAKES when not facing bet

			// Pattern 3: BAD BLUFF with weak hands
			// Trigger: equity is < 0.80x threshold (clearly not strong enough)
			if (equityRatio < 0.80f)
			{
				float badBluffChance = mistakeIntensity * 0.30f;

				// More likely on flop/turn (trying to "take it down")
				if (currentStreet == Street.Flop) badBluffChance *= 1.4f;
				if (currentStreet == Street.Turn) badBluffChance *= 1.2f;

				// Less likely on river (even amateurs get scared)
				if (currentStreet == Street.River) badBluffChance *= 0.5f;

				// More likely with shorter stacks (less commitment)
				if (spr < 5.0f) badBluffChance *= 1.3f;
				
				float randomRoll = GD.Randf();
				GD.Print($"[MISTAKE DEBUG] BAD BLUFF: Chance={badBluffChance:P1}, Roll={randomRoll:F3}, Trigger={randomRoll < badBluffChance}");

				if (randomRoll < badBluffChance)
				{
					float decrease = mistakeIntensity * 0.15f;
					adjustedThreshold = baseThreshold * (1.0f - decrease);
					mistake = MistakeType.BadBluff;
					GD.Print($"[MISTAKE] Bad bluff setup - wannabe pro (chance: {badBluffChance:P1})");
				}
			}

			// Pattern 4: MISSED VALUE with strong hands
			// Trigger: equity is > 1.10x threshold but not premium (0.65-0.80)
			else if (equityRatio > 1.10f && equity >= 0.65f && equity < 0.80f)
			{
				float missedValueChance = mistakeIntensity * 0.45f; // Max 22.5% at MF=1.5

				// More likely on turn/river (afraid of "monsters under bed")
				if (currentStreet == Street.Turn) missedValueChance *= 1.3f;
				if (currentStreet == Street.River) missedValueChance *= 1.5f;

				// More likely on scary boards (this would need board texture analysis)
				// For now, use random factor to simulate board fear
				if (GD.Randf() < 0.35f) missedValueChance *= 1.4f; // 35% of boards are "scary"
			
				float randomRoll = GD.Randf();
				GD.Print($"[MISTAKE DEBUG] MISSED VALUE: Chance={missedValueChance:P1}, Roll={randomRoll:F3}, Trigger={randomRoll < missedValueChance}");

				if (randomRoll < missedValueChance)
				{
					// This will cause passive play in MakeDecision
					mistake = MistakeType.MissedValue;
					GD.Print($"[MISTAKE] Missed value setup - too passive (chance: {missedValueChance:P1})");
				}
			}
		}

		// Safety rails: clamp adjustments to prevent absurd thresholds
		float maxAdjustment = 0.15f; // Never move threshold more than 15%
		float adjustmentDelta = Math.Abs(adjustedThreshold - baseThreshold);
		if (adjustmentDelta > maxAdjustment)
		{
			adjustedThreshold = baseThreshold + Math.Sign(adjustedThreshold - baseThreshold) * maxAdjustment;
			GD.Print($"[SAFETY RAIL] Capping threshold adjustment at ±{maxAdjustment:P0}");
		}

		return (adjustedThreshold, mistake);
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
				if (spr < 3.0f) return 0.36f; // Short stack: wider calls
				if (spr < 7.0f) return 0.42f; // Medium stack
				return 0.46f; // Deep stack
			}
			else
			{
				return 0.38f; // Can open 58% of hands
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

	private AIAction MakeDecision(float equity, float threshold, bool facingBet, MistakeType mistake)
	{
		if (raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			if (facingBet)
				return equity >= threshold ? AIAction.Call : AIAction.Fold;
			else
				return AIAction.Check;
		}

		float mistakeFactor = currentOpponent.MistakeFactor;
		float mistakeIntensity = Math.Clamp(mistakeFactor - 1.0f, 0.0f, 0.5f);

		if (facingBet)
		{
			// === FACING BET LOGIC ===
			
			float equityGap = Math.Abs(equity - threshold);
			if (equityGap < 0.05f)
			{
				float coinFlip = GD.Randf();
				if (coinFlip < 0.60f) // 60% chance to call in close spots
				{
					GD.Print($"[CLOSE DECISION] Within {equityGap:P1} of threshold - calling anyway");
					return AIAction.Call;
				}
			}
	
			// Apply overcall mistake
			if (mistake == MistakeType.Overcall && equity < threshold * 0.75f)
			{
				// Force call with weak hand
				float overcallChance = 0.70f + (mistakeIntensity * 0.60f); // 70-100% chance
				if (GD.Randf() < overcallChance)
				{
					GD.Print($"[MISTAKE EXECUTED] Overcalling with weak hand ({equity:P1} equity)");
					return AIAction.Call;
				}
			}

			// Apply overfold mistake
			if (mistake == MistakeType.Overfold && equity >= threshold * 0.80f && equity < threshold * 1.15f)
			{
				// Force fold with medium hand
				float overfoldChance = 0.65f + (mistakeIntensity * 0.70f); // 65-100% chance
				if (GD.Randf() < overfoldChance)
				{
					GD.Print($"[MISTAKE EXECUTED] Overfolding medium-strength hand ({equity:P1} equity)");
					return AIAction.Fold;
				}
			}

			// Normal logic with adjusted threshold
			if (equity < threshold * 0.75f)
			{
				// Safety: even bad players fold pure trash sometimes
				float foldChance = 0.80f - (mistakeIntensity * 0.30f); // 80% down to 65%
				if (GD.Randf() < foldChance)
					return AIAction.Fold;

				// Occasional bad bluff-raise
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
				if (GD.Randf() < currentOpponent.BluffChance * 0.25f && raisesThisStreet < MAX_RAISES_PER_STREET)
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
				// Strong hand - raise frequently (slightly reduced by mistakes)
				float raiseChance = equity >= 0.80f ? 0.90f : 0.75f;
				raiseChance -= (mistakeIntensity * 0.15f); // Slight reduction for amateurs
				if (GD.Randf() < raiseChance && raisesThisStreet < MAX_RAISES_PER_STREET)
					return AIAction.Raise;
				return AIAction.Call;
			}
		}
		else
		{
			// === NOT FACING BET LOGIC ===

			// Apply bad bluff mistake
			if (mistake == MistakeType.BadBluff && equity < threshold * 0.80f)
			{
				// Force bet with weak hand
				float badBluffChance = 0.60f + (mistakeIntensity * 0.80f); // 60-100% chance
				if (GD.Randf() < badBluffChance)
				{
					aiBluffedThisHand = true;
					GD.Print($"[MISTAKE EXECUTED] Bad bluff with trash hand ({equity:P1} equity)");
					return AIAction.Bet;
				}
			}

			// Apply missed value mistake
			if (mistake == MistakeType.MissedValue && equity >= 0.65f && equity < 0.80f)
			{
				// Force check with strong hand
				float missedValueChance = 0.70f + (mistakeIntensity * 0.60f); // 70-100% chance
				if (GD.Randf() < missedValueChance)
				{
					GD.Print($"[MISTAKE EXECUTED] Missed value with strong hand ({equity:P1} equity)");
					return AIAction.Check;
				}
			}

			// Normal logic
			if (equity < threshold * 0.85f)
			{
				// Allow more bluffs for amateurs (but still capped)
				float bluffMultiplier = 1.0f + (mistakeIntensity * 0.8f); // Up to 1.4x
				if (equity >= threshold * 0.75f && GD.Randf() < currentOpponent.BluffChance * 0.5f * bluffMultiplier)
				{
					aiBluffedThisHand = true;
					GD.Print("[BLUFF] Bluff-betting marginal hand");
					return AIAction.Bet;
				}
				return AIAction.Check;
			}
			else if (equity < threshold + 0.10f)
			{
				// Medium strength - mixed strategy
				float betFreq = (equity - threshold * 0.85f) / (threshold + 0.10f - threshold * 0.85f);
				betFreq -= (mistakeIntensity * 0.15f); // Amateurs miss more value
				return GD.Randf() < betFreq * 0.65f ? AIAction.Bet : AIAction.Check;
			}
			else
			{
				// Strong hand - bet frequently (slightly reduced by mistakes)
				float equityMargin = equity - threshold;
				float betChance;

				if (equityMargin > 0.20f)
				{
					betChance = 0.98f - (mistakeIntensity * 0.10f); // Slight reduction
					GD.Print($"[VALUE] Huge equity advantage ({equityMargin:P0}) -> betting {betChance:P0}");
				}
				else if (equity >= 0.80f)
				{
					betChance = 0.95f - (mistakeIntensity * 0.12f);
				}
				else
				{
					betChance = 0.85f - (mistakeIntensity * 0.15f);
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

		// Maniac discount
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
		currentMistake = MistakeType.None; // Reset mistake tracking
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
