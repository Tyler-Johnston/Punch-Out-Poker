// PokerGame.AI.cs
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PokerGame
{
	// ===== TRACKING FIELDS (Keep for stats/debugging) =====
	private bool aiBluffedThisHand = false;
	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();
	private int playerTotalBetsThisHand = 0;
	private int currentHandNumber = 0;
	private int handsPlayed = 0;

	// ===== EQUITY CALCULATION SETTINGS =====
	private const int DEFAULT_SIMULATIONS = 1000;
	private const int PREFLOP_SIMULATIONS = 500;
	private const int CRITICAL_SIMULATIONS = 1500;

	// ===========================================================================================
	// ✅ NEW: MAIN DECISION METHOD (Calls PokerAIv2)
	// ===========================================================================================

	private AIAction DecideAIAction()
	{
		if (opponentChips <= 0) return AIAction.Check;

		// Calculate equity using Monte Carlo
		List<string> villainRange = EstimateVillainRange();
		int simCount = GetSimulationCount(currentStreet, (float)opponentChips / bigBlind, playerBet > opponentBet);
		float equity = CalculateEquity(opponentHand, communityCards, villainRange, simCount);

		GD.Print($"[EQUITY] {equity:P1} vs estimated range ({villainRange.Count} hands, {simCount} sims)");
		
		// Update UI with equity
		UpdateOpponentDialogue(equity * 100f, false);

		// Get game state variables
		bool facingBet = playerBet > opponentBet;
		int toCall = playerBet - opponentBet;
		int effectiveStack = Mathf.Min(playerChips, opponentChips);

		// ✅ Call new AI system
		AIAction decision = aiSystem.MakeDecision(
			equity: equity,  // Already 0.0-1.0 from CalculateEquity
			street: currentStreet,
			facingBet: facingBet,
			toCall: toCall,
			pot: pot,
			effectiveStack: effectiveStack,
			currentBet: opponentBet,
			opponentBet: playerBet
		);

		return decision;
	}

	// ===========================================================================================
	// MONTE CARLO EQUITY CALCULATOR (Keep - still needed!)
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
			List<Card> deck = Deck.CreateCardList();
			Deck.RemoveCardsFromDeck(deck, heroHand);
			Deck.RemoveCardsFromDeck(deck, board);

			List<Card> villainHand = RangeSampler.SampleHandFromRange(deck, villainRange);
			if (villainHand == null || villainHand.Count != 2) continue;

			Deck.RemoveCardsFromDeck(deck, villainHand);

			List<Card> simulatedBoard = new List<Card>(board);
			while (simulatedBoard.Count < 5)
			{
				int randomIndex = (int)(GD.Randf() * deck.Count);
				simulatedBoard.Add(deck[randomIndex]);
				deck.RemoveAt(randomIndex);
			}

			int heroScore = HandEvaluator.Evaluate7Cards(heroHand, simulatedBoard);
			int villainScore = HandEvaluator.Evaluate7Cards(villainHand, simulatedBoard);

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

	// ===========================================================================================
	// VILLAIN RANGE ESTIMATION (Keep - useful for equity calculation)
	// ===========================================================================================

	private List<string> EstimateVillainRange()
	{
		// Simple range estimation based on street
		// You can enhance this later with action tracking
		
		if (currentStreet == Street.Preflop)
		{
			// Use standard balanced range for now
			return new List<string>(HandRanges.BalancedRange);
		}
		else // Postflop
		{
			int toCall = playerBet - opponentBet;
			
			// Check for river shove scenario
			bool isPlayerAllIn = (toCall >= playerChips);
			bool isLargeRiverBet = (toCall >= pot * 0.75f);
			bool isRiverShove = (currentStreet == Street.River && toCall > 0 && 
								 (isPlayerAllIn || isLargeRiverBet));

			if (isRiverShove)
			{
				GD.Print($"[Range] River shove detected - narrow range");
				return new List<string>(HandRanges.StrongPostflopRange);
			}
			else if (toCall > pot * 0.6f)
			{
				GD.Print($"[Range] Large bet - strong range");
				return new List<string>(HandRanges.StrongPostflopRange);
			}
			else if (toCall > pot * 0.3f)
			{
				GD.Print($"[Range] Medium bet - medium range");
				return new List<string>(HandRanges.MediumPostflopRange);
			}
			else
			{
				GD.Print($"[Range] Small/no bet - wide range");
				return new List<string>(HandRanges.WeakPostflopRange);
			}
		}
	}

	// ===========================================================================================
	// ACTION TRACKING (Keep for future enhancements)
	// ===========================================================================================

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

		if (actionHistory.Count > 150)
		{
			actionHistory.RemoveRange(0, 50);
		}

		GD.Print($"[TRACKING] Player {actionType} {amount} on {currentStreet}");
	}

	public void StartNewHandTracking()
	{
		currentHandNumber++;
		handsPlayed++;
		aiBluffedThisHand = false;
		GD.Print($"\n[NEW HAND] #{currentHandNumber} (Total played: {handsPlayed})");
	}

	// ===========================================================================================
	// EXECUTION & BET SIZING (Keep - still needed!)
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
		
		// Use profile-based bet sizing (if you have BetSizeFactor in profile)
		// Otherwise use default
		float sizeFactor = 0.65f; // Default: 65% pot bet
		
		// Adjust based on street
		if (currentStreet == Street.Preflop)
			sizeFactor = 0.50f; // 50% pot (2.5-3x BB)
		else if (currentStreet == Street.River)
			sizeFactor = 0.75f; // 75% pot on river

		int betSize = (int)(pot * sizeFactor);
		betSize = Math.Max(betSize, minBet);
		int maxBet = Math.Max(minBet, opponentChips);

		return Math.Min(betSize, maxBet);
	}
}
