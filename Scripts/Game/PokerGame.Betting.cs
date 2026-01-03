// PokerGame.Betting.cs
using Godot;
using System;

public partial class PokerGame
{
	// Betting-related fields
	private int pot = 0;
	private int betAmount = 20;
	private int currentBet = 0;
	private int playerBet = 0;
	private int opponentBet = 0;
	private int smallBlind = 5;
	private int bigBlind = 10;

	private int raisesThisStreet = 0;
	private const int MAX_RAISES_PER_STREET = 4;

	// Action tracking
	private bool playerHasActedThisStreet = false;
	private bool opponentHasActedThisStreet = false;

	private (int minBet, int maxBet) GetLegalBetRange()
	{
		int maxBet = playerChips;
		if (maxBet <= 0) return (0, 0);

		bool opening = currentBet == 0;
		int minBet;

		if (opening)
		{
			// Opening: Min bet is Big Blind (clamped to stack)
			minBet = Math.Min(bigBlind, maxBet);
		}
		else
		{
			// === THE FIX ===
			// The minimum you must RAISE BY is the amount you are facing to call.
			// Example: Opponent bet 28 total. You have 10 in.
			// Amount to call = 18.
			// Therefore, Min Raise Increment = 18.
			
			int amountToCall = currentBet - playerBet;
			
			// Standard Rule: Raise must match the previous bet/raise size.
			// If checking (0 to call), we default to Big Blind.
			int minRaiseIncrement = (amountToCall == 0) ? bigBlind : amountToCall;
			
			// Edge Case: Tiny bets must still be raised by at least 1 BB
			minRaiseIncrement = Math.Max(minRaiseIncrement, bigBlind);

			// The Slider controls the "Raise Amount" (the amount ON TOP of the call)
			minBet = minRaiseIncrement;
			
			// Cap at stack size
			if (minBet > maxBet)
			{
				minBet = maxBet; 
			}
		}

		// Safety clamp
		if (minBet > maxBet) minBet = maxBet;

		return (minBet, maxBet);
	}

	private void ResetBettingRound()
	{
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		raisesThisStreet = 0;
		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;
	}
}
