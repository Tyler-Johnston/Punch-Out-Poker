using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class PokerDecisionMaker : Node
{
	// Adjust these to tune overall AI difficulty
	private const float BASE_HAND_STRENGTH_WEIGHT = 0.6f;
	private const float PERSONALITY_WEIGHT = 0.4f;
	
	public PlayerAction DecideAction(AIPokerPlayer player, GameState gameState)
	{
		// Calculate base hand strength (0.0 to 1.0)
		float handStrength = EvaluateHandStrength(player.Hand, gameState.CommunityCards, gameState.Street);
		
		// Get personality-modified decision
		return MakePersonalityBasedDecision(player, gameState, handStrength);
	}
	
	private PlayerAction MakePersonalityBasedDecision(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		var personality = player.Personality;
		float currentBet = gameState.CurrentBet;
		float potSize = gameState.PotSize;
		float toCall = currentBet - gameState.GetPlayerCurrentBet(player);
		
		// Calculate pot odds
		float potOdds = toCall > 0 ? toCall / (potSize + toCall) : 0f;
		
		// Decision thresholds modified by personality
		float raiseThreshold = 0.65f - (personality.CurrentAggression * 0.3f);
		float callThreshold = 0.35f - (personality.CallTendency * 0.2f);
		float bluffChance = personality.CurrentBluffFrequency;
		
		// Check if we can check (no bet to call)
		if (toCall <= 0)
		{
			return DecideCheckOrBet(player, gameState, handStrength);
		}
		
		// Anti-calling station logic - fold to big bets with weak hands
		float betRatio = toCall / potSize;
		if (handStrength < 0.40f && betRatio > 1.5f)
		{
			// Facing a pot-sized bet or bigger with weak hand - fold more often
			float foldChance = 0.75f - (personality.CallTendency * 0.3f);
			if (GD.Randf() < foldChance)
			{
				GD.Print($"[{player.PlayerName}] Folding to large bet (strength: {handStrength:F2}, bet: {betRatio:F1}x pot)");
				return PlayerAction.Fold;
			}
		}
		
		// Only for VERY weak hands facing HUGE bets
		if (handStrength < 0.30f && betRatio > 2.0f) // Jack-high facing 2x pot+
		{
			GD.Print($"[{player.PlayerName}] Too weak to call huge bet");
			return PlayerAction.Fold;
		}

		
		// Can't call if we don't have enough chips - must fold or all-in
		if (toCall >= player.ChipStack)
		{
			// All-in decision based on hand strength and risk tolerance
			float allInThreshold = 0.7f - (personality.CurrentRiskTolerance * 0.3f);
			if (handStrength > allInThreshold || (handStrength > 0.4f && GD.Randf() < bluffChance))
			{
				return PlayerAction.AllIn;
			}
			return PlayerAction.Fold;
		}
		
		// Facing a bet - decide fold/call/raise
		
		// Bluffing logic - sometimes raise with weak hands
		if (handStrength < 0.35f && GD.Randf() < bluffChance) // ✅ Changed from 0.4f to 0.35f
		{
			// Bluff raise - need enough chips
			if (player.ChipStack > toCall * 2)
			{
				GD.Print($"[{player.PlayerName}] Bluffing with weak hand (strength: {handStrength:F2})");
				return PlayerAction.Raise;
			}
		}
		
		// Strong hand - usually raise
		if (handStrength > raiseThreshold)
		{
			// Sometimes slow play (call instead of raise) with very strong hands
			if (handStrength > 0.85f && GD.Randf() < 0.25f)
			{
				GD.Print($"[{player.PlayerName}] Slow-playing strong hand (strength: {handStrength:F2})");
				return PlayerAction.Call;
			}
			
			// Check if we have chips to raise
			if (player.ChipStack > toCall * 2)
			{
				// More aggressive players raise more often
				if (GD.Randf() < personality.CurrentAggression)
				{
					return PlayerAction.Raise;
				}
			}
			
			return PlayerAction.Call;
		}
		
		// Medium hand - usually call
		if (handStrength > callThreshold)
		{
			if (handStrength > potOdds * 1.5f || GD.Randf() < personality.CallTendency)
			{
				return PlayerAction.Call;
			}
		}
		
		// Weak hand - usually fold
		// But calling stations (high CallTendency) call too much
		if (GD.Randf() < personality.CallTendency * 0.3f)
		{
			GD.Print($"[{player.PlayerName}] Calling station behavior - calling with weak hand");
			return PlayerAction.Call;
		}
		
		// Fold threshold check - how easily do they give up?
		if (handStrength < personality.CurrentFoldThreshold)
		{
			return PlayerAction.Fold;
		}
		
		// Default to fold (conservative choice when uncertain)
		return PlayerAction.Fold;
	}
	
	private PlayerAction DecideCheckOrBet(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		var personality = player.Personality;
		
		// Bluff bet with weak hand
		if (handStrength < 0.35f && GD.Randf() < personality.CurrentBluffFrequency)
		{
			GD.Print($"[{player.PlayerName}] Bluff betting (strength: {handStrength:F2})");
			return PlayerAction.Raise; // "Raise" here means opening bet
		}
		
		// Strong hand - bet for value
		if (handStrength > 0.6f)
		{
			// Aggressive players bet more often
			if (GD.Randf() < personality.CurrentAggression)
			{
				return PlayerAction.Raise;
			}
			
			// Sometimes check strong hands to trap
			if (handStrength > 0.8f && GD.Randf() < 0.3f)
			{
				GD.Print($"[{player.PlayerName}] Check-trapping (strength: {handStrength:F2})");
				return PlayerAction.Check;
			}
			
			return PlayerAction.Raise;
		}
		
		// Medium hand - usually check
		if (handStrength > 0.4f && GD.Randf() < 0.3f)
		{
			return PlayerAction.Raise;
		}
		
		return PlayerAction.Check;
	}
	
	public int CalculateBetSize(AIPokerPlayer player, GameState gameState, float handStrength)
	{
		var personality = player.Personality;
		float potSize = gameState.PotSize;
		float currentBet = gameState.CurrentBet;
		float toCall = currentBet - gameState.GetPlayerCurrentBet(player);
		
		// ✅ IMPROVED: Strength-based sizing
		float baseBetMultiplier;
		
		if (handStrength >= 0.80f) // Premium hands (straights, flushes, full houses)
		{
			baseBetMultiplier = 0.70f + (GD.Randf() * 0.30f); // 0.70-1.00x pot (VALUE BET)
		}
		else if (handStrength >= 0.65f) // Strong hands (top pair good kicker, two pair)
		{
			baseBetMultiplier = 0.55f + (GD.Randf() * 0.25f); // 0.55-0.80x pot
		}
		else if (handStrength >= 0.45f) // Medium hands (middle pair, weak two pair)
		{
			baseBetMultiplier = 0.40f + (GD.Randf() * 0.20f); // 0.40-0.60x pot
		}
		else if (handStrength >= 0.35f) // Weak hands (bottom pair, draws)
		{
			baseBetMultiplier = 0.25f + (GD.Randf() * 0.20f); // 0.25-0.45x pot (MIN BET)
		}
		else // Bluffs (<0.35)
		{
			baseBetMultiplier = 0.50f + (GD.Randf() * 0.30f); // 0.50-0.80x pot (LOOK STRONG!)
		}
		
		// Personality modifications
		float aggressionMultiplier = 0.7f + (personality.CurrentAggression * 0.6f);
		float betSize = potSize * baseBetMultiplier * aggressionMultiplier;
		
		// Tilt adjustments (tilted = bigger bets)
		if (personality.TiltMeter > 20f)
		{
			betSize *= 1.15f; // 15% bigger when tilted
			GD.Print($"[{player.PlayerName}] Tilted betting ({personality.TiltMeter:F0} tilt) - increased bet size");
		}
		
		// Street-based adjustments
		if (gameState.Street == Street.River && handStrength > 0.70f)
		{
			betSize *= 1.2f; // Go for value on river
			GD.Print($"[{player.PlayerName}] River value bet");
		}
		
		// Ensure minimum raise is valid (at least double the current bet or one big blind)
		float minRaise = toCall > 0 ? (currentBet - gameState.GetPlayerCurrentBet(player)) + currentBet : gameState.BigBlind;
		betSize = Mathf.Max(betSize, minRaise);
		
		// Check for all-in situations
		if (betSize >= player.ChipStack * 0.9f)
		{
			// Risk tolerance affects all-in decisions
			if (GD.Randf() < personality.CurrentRiskTolerance || handStrength > 0.80f)
			{
				GD.Print($"[{player.PlayerName}] Going all-in! ({player.ChipStack} chips)");
				return player.ChipStack; // All-in
			}
			else
			{
				// Scale back to smaller bet
				betSize = player.ChipStack * 0.6f; // ✅ Changed from 0.5f to 0.6f
			}
		}
		
		// Cap at chip stack
		int finalBet = (int)Mathf.Min(betSize, player.ChipStack);
		
		// Ensure bet is at least 1 chip if we're betting
		finalBet = Mathf.Max(1, finalBet);
		
		GD.Print($"[{player.PlayerName}] Bet size: {finalBet} (pot: {potSize}, strength: {handStrength:F2})");
		
		return finalBet;
	}
	
	private float EvaluateHandStrength(List<Card> holeCards, List<Card> communityCards, Street street)
	{
		// Validate input
		if (holeCards == null || holeCards.Count != 2)
		{
			GD.PrintErr("Invalid hole cards in EvaluateHandStrength");
			return 0.2f;
		}
		
		// Pre-flop evaluation
		if (street == Street.Preflop || communityCards == null || communityCards.Count == 0)
		{
			return EvaluatePreflopHand(holeCards);
		}
		
		// Use pheval directly with power curve
		int phevalRank = HandEvaluator.EvaluateHand(holeCards, communityCards);
		
		// Convert pheval rank (1-7462) to strength (0.0-1.0)
		// Lower rank = better hand in pheval
		float strength = 1.0f - ((phevalRank - 1) / 7461.0f);
		
		// Apply power curve for better differentiation
		strength = (float)Math.Pow(strength, 0.75);
		
		// Minimum floor for pairs
		if (phevalRank <= 6185) // Any pair or better
		{
			strength = Math.Max(strength, 0.38f); // Minimum 0.38 for any pair
		}
		
		// Adjust for draw potential on flop/turn
		if (street == Street.Flop || street == Street.Turn)
		{
			List<Card> allCards = new List<Card>(holeCards);
			allCards.AddRange(communityCards);
			strength += EvaluateDrawPotential(allCards) * 0.10f;
		}
		
		// Add randomness so AI doesn't play perfectly
		float randomness = (GD.Randf() - 0.5f) * 0.08f;
		
		return Mathf.Clamp(strength + randomness, 0.10f, 1.0f);
	}
	
	private float EvaluatePreflopHand(List<Card> holeCards)
	{
		if (holeCards.Count != 2) return 0.2f;
		
		Card card1 = holeCards[0];
		Card card2 = holeCards[1];
		
		bool isPair = card1.Rank == card2.Rank;
		bool isSuited = card1.Suit == card2.Suit;
		int rankDiff = Mathf.Abs((int)card1.Rank - (int)card2.Rank);
		int highCard = Mathf.Max((int)card1.Rank, (int)card2.Rank);
		
		float strength = 0.2f; // Base strength
		
		// Pocket pairs
		if (isPair)
		{
			// Pairs are strong - scale from 0.5 (deuces) to 0.95 (aces)
			strength = 0.5f + (highCard / 28f);
		}
		else
		{
			// High cards
			strength += (highCard / 40f);
			
			// Suited bonus
			if (isSuited) strength += 0.1f;
			
			// Connected cards (potential straights)
			if (rankDiff <= 2) strength += 0.05f;
			
			// Premium hands (AK, AQ, KQ, etc.)
			if (highCard >= 12 && rankDiff <= 2) strength += 0.15f;
		}
		
		return Mathf.Clamp(strength, 0.1f, 1.0f);
	}
	
	private float EvaluateDrawPotential(List<Card> cards)
	{
		// Check for flush draws (4 of same suit)
		var suitCounts = cards.GroupBy(c => c.Suit).Select(g => g.Count());
		if (suitCounts.Any(count => count == 4)) return 0.35f; // Flush draw
		
		// Check for straight draws (simplified)
		var ranks = cards.Select(c => (int)c.Rank).OrderBy(r => r).Distinct().ToList();
		for (int i = 0; i < ranks.Count - 3; i++)
		{
			if (ranks[i + 3] - ranks[i] == 3) return 0.30f; // Open-ended straight draw
		}
		
		return 0f;
	}
}

public partial class GameState : RefCounted
{
	public List<Card> CommunityCards { get; set; } = new List<Card>();
	public float PotSize { get; set; }
	public float CurrentBet { get; set; }
	public Street Street { get; set; }
	public float BigBlind { get; set; }
	public int OpponentChipStack { get; set; }
	
	private Dictionary<AIPokerPlayer, float> playerBets = new Dictionary<AIPokerPlayer, float>();
	
	public float GetPlayerCurrentBet(AIPokerPlayer player)
	{
		return playerBets.ContainsKey(player) ? playerBets[player] : 0f;
	}
	
	public void SetPlayerBet(AIPokerPlayer player, float amount)
	{
		playerBets[player] = amount;
	}
	
	/// <summary>
	/// Reset bets for new betting round
	/// </summary>
	public void ResetBetsForNewStreet()
	{
		playerBets.Clear();
	}
}
