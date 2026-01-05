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
		if (effectiveBB <= 12f && currentStreet == Street.Preflop && facingBet)
		{
			string handString = GetHandString(opponentHand);
			if (nashPushThresholds.ContainsKey(handString))
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
		
		// FIXED: Start exploiting at 65% instead of 70%, larger adjustment
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
	
	private static Dictionary<string, float> nashPushThresholds = new Dictionary<string, float>
	{
		// Premium pairs
		{"AA", 50f}, {"KK", 50f}, {"QQ", 50f}, {"JJ", 50f}, {"TT", 50f},
		{"99", 50f}, {"88", 50f}, {"77", 50f}, {"66", 11f}, {"55", 9f},
		{"44", 7f}, {"33", 6f}, {"22", 5f},
		
		// Suited aces
		{"AKs", 50f}, {"AQs", 50f}, {"AJs", 40f}, {"ATs", 50f}, {"A9s", 45f},
		{"A8s", 37f}, {"A7s", 32f}, {"A6s", 28f}, {"A5s", 37f}, {"A4s", 28f},
		{"A3s", 24f}, {"A2s", 24f},
		
		// Offsuit aces
		{"AKo", 50f}, {"AQo", 50f}, {"AJo", 35f}, {"ATo", 28f}, {"A9o", 22f},
		{"A8o", 18f}, {"A7o", 14f}, {"A6o", 11f}, {"A5o", 16f},
		
		// Suited kings
		{"KQs", 50f}, {"KJs", 45f}, {"KTs", 38f}, {"K9s", 30f}, {"K8s", 24f},
		{"K7s", 16f}, {"K6s", 14f}, {"K5s", 13f},
		
		// Offsuit kings
		{"KQo", 35f}, {"KJo", 28f}, {"KTo", 22f}, {"K9o", 16f},
		
		// Other broadway
		{"QJs", 45f}, {"QTs", 35f}, {"Q9s", 28f}, {"Q8s", 22f},
		{"JTs", 38f}, {"J9s", 28f}, {"J8s", 22f},
		{"T9s", 30f}, {"T8s", 24f},
		{"QJo", 30f}, {"QTo", 24f}, {"JTo", 24f},
		
		// Suited connectors
		{"98s", 24f}, {"87s", 20f}, {"76s", 18f}, {"65s", 16f}, {"54s", 14f}
	};

	private AIAction DecideWithNash(float effectiveBB, string handString)
	{
		float pushThreshold = nashPushThresholds[handString];
		
		if (effectiveBB <= pushThreshold)
		{
			GD.Print($"[NASH] ✓ CALL {handString} at {effectiveBB:F1}BB (threshold: {pushThreshold}BB)");
			return AIAction.Call;
		}
		else
		{
			GD.Print($"[NASH] ✗ FOLD {handString} at {effectiveBB:F1}BB (needs ≤{pushThreshold}BB)");
			return AIAction.Fold;
		}
	}

	// ===========================================================================================
	// MONTE CARLO EQUITY CALCULATOR (USES YOUR HandEvaluator)
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
			List<Card> deck = CreateFullDeck();
			RemoveCardsFromDeck(deck, heroHand);
			RemoveCardsFromDeck(deck, board);

			// Sample villain hand from their range
			List<Card> villainHand = SampleHandFromRange(deck, villainRange);
			if (villainHand == null || villainHand.Count != 2) continue;
			
			RemoveCardsFromDeck(deck, villainHand);

			// Complete the board to 5 cards
			List<Card> simulatedBoard = new List<Card>(board);
			while (simulatedBoard.Count < 5)
			{
				int randomIndex = (int)(GD.Randf() * deck.Count);
				simulatedBoard.Add(deck[randomIndex]);
				deck.RemoveAt(randomIndex);
			}

			// Evaluate both hands using YOUR HandEvaluator (lower = better)
			int heroScore = HandEvaluator.Evaluate7Cards(heroHand, simulatedBoard);
			int villainScore = HandEvaluator.Evaluate7Cards(villainHand, simulatedBoard);

			// IMPORTANT: Lower rank = better hand in pheval
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
		if (facingBet && effectiveBB < 10f) return CRITICAL_SIMULATIONS; // Critical spot
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


	private List<Card> SampleHandFromRange(List<Card> availableDeck, List<string> range)
	{
		if (range.Count == 0) return null;

		// Try up to 50 times to find a valid hand
		for (int attempt = 0; attempt < 50; attempt++)
		{
			string handStr = range[(int)(GD.Randf() * range.Count)];
			List<Card> hand = ConvertHandStringToCards(handStr, availableDeck);
			
			if (hand != null && hand.Count == 2)
				return hand;
		}

		// Fallback: return any two random cards
		if (availableDeck.Count >= 2)
		{
			return new List<Card> { availableDeck[0], availableDeck[1] };
		}

		return null;
	}

	private List<Card> ConvertHandStringToCards(string handStr, List<Card> availableDeck)
	{
		// Parse hand string like "AKs", "77", "Q9o"
		if (handStr.Length < 2) return null;

		char rank1Char = handStr[0];
		char rank2Char = handStr[1];
		bool isSuited = handStr.EndsWith("s");
		bool isPair = rank1Char == rank2Char;

		Rank rank1 = CharToRank(rank1Char);
		Rank rank2 = CharToRank(rank2Char);

		// Find matching cards in available deck
		var card1Options = availableDeck.Where(c => c.Rank == rank1).ToList();
		var card2Options = availableDeck.Where(c => c.Rank == rank2 && c != card1Options.FirstOrDefault()).ToList();

		if (card1Options.Count == 0 || card2Options.Count == 0) return null;

		Card card1;
		Card card2;

		if (isPair)
		{
			// Randomly pick 2 cards of same rank
			if (card1Options.Count < 2) return null;
			card1 = card1Options[(int)(GD.Randf() * card1Options.Count)];
			card2Options = card1Options.Where(c => c != card1).ToList();
			if (card2Options.Count == 0) return null;
			card2 = card2Options[(int)(GD.Randf() * card2Options.Count)];
		}
		else if (isSuited)
		{
			// Must be same suit
			foreach (var c1 in card1Options)
			{
				var matching = card2Options.Where(c2 => c2.Suit == c1.Suit).ToList();
				if (matching.Count > 0)
				{
					card1 = c1;
					card2 = matching[(int)(GD.Randf() * matching.Count)];
					return new List<Card> { card1, card2 };
				}
			}
			return null;
		}
		else // Offsuit
		{
			// Must be different suits
			foreach (var c1 in card1Options)
			{
				var matching = card2Options.Where(c2 => c2.Suit != c1.Suit).ToList();
				if (matching.Count > 0)
				{
					card1 = c1;
					card2 = matching[(int)(GD.Randf() * matching.Count)];
					return new List<Card> { card1, card2 };
				}
			}
			return null;
		}

		return new List<Card> { card1, card2 };
	}

	private Rank CharToRank(char c)
	{
		switch (c)
		{
			case 'A': return Rank.Ace;
			case 'K': return Rank.King;
			case 'Q': return Rank.Queen;
			case 'J': return Rank.Jack;
			case 'T': return Rank.Ten;
			case '9': return Rank.Nine;
			case '8': return Rank.Eight;
			case '7': return Rank.Seven;
			case '6': return Rank.Six;
			case '5': return Rank.Five;
			case '4': return Rank.Four;
			case '3': return Rank.Three;
			case '2': return Rank.Two;
			default: return Rank.Two;
		}
	}

	private string GetHandString(List<Card> hand)
	{
		if (hand == null || hand.Count != 2) return "XX";

		char rank1 = RankToChar(hand[0].Rank);
		char rank2 = RankToChar(hand[1].Rank);

		// Ensure higher rank comes first
		if ((int)hand[1].Rank > (int)hand[0].Rank)
		{
			(rank1, rank2) = (rank2, rank1);
		}

		if (hand[0].Rank == hand[1].Rank)
			return $"{rank1}{rank2}"; // Pair: "AA", "KK"
		else if (hand[0].Suit == hand[1].Suit)
			return $"{rank1}{rank2}s"; // Suited: "AKs"
		else
			return $"{rank1}{rank2}o"; // Offsuit: "AKo"
	}

	private char RankToChar(Rank rank)
	{
		switch (rank)
		{
			case Rank.Ace: return 'A';
			case Rank.King: return 'K';
			case Rank.Queen: return 'Q';
			case Rank.Jack: return 'J';
			case Rank.Ten: return 'T';
			case Rank.Nine: return '9';
			case Rank.Eight: return '8';
			case Rank.Seven: return '7';
			case Rank.Six: return '6';
			case Rank.Five: return '5';
			case Rank.Four: return '4';
			case Rank.Three: return '3';
			case Rank.Two: return '2';
			default: return '?';
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
				if (spr < 7.0f) return 0.48f; // Medium stack (was 0.52 - MAJOR FIX)
				return 0.52f; // Deep stack (was 0.58 - still disciplined but playable)
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
				// ENHANCED: Bet even more often when equity way above threshold
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
	// DECK & CARD UTILITIES
	// ===========================================================================================
	
	private List<Card> CreateFullDeck()
	{
		List<Card> deck = new List<Card>();
		
		foreach (Suit suit in Enum.GetValues(typeof(Suit)))
		{
			foreach (Rank rank in Enum.GetValues(typeof(Rank)))
			{
				deck.Add(new Card(rank, suit));
			}
		}
		
		return deck;
	}

	private void RemoveCardsFromDeck(List<Card> deck, List<Card> cardsToRemove)
	{
		if (cardsToRemove == null) return;
		
		foreach (var card in cardsToRemove)
		{
			deck.RemoveAll(c => c.Suit == card.Suit && c.Rank == card.Rank);
		}
	}

	// ===========================================================================================
	// OPPONENT MODELING (PRESERVED FROM ORIGINAL)
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
	// EXECUTION & BET SIZING (PRESERVED FROM ORIGINAL)
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
