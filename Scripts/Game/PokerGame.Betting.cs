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
		if (maxBet <= 0)
			return (0, 0);

		bool opening = currentBet == 0;
		int minBet;

		if (opening)
		{
			// Opening the betting: at least big blind, but you can always go all-in if shorter
			minBet = Math.Min(Math.Max(bigBlind, 1), maxBet);
		}
		else
		{
			// There is an existing bet (currentBet)
			int toCall = currentBet - playerBet;

			// Minimum legal *raise* size approximation: +big blind over currentBet
			int minRaiseSize = bigBlind;
			int minTotalBet = currentBet + minRaiseSize;
			int minToAdd = minTotalBet - playerBet;

			// Base minimum: at least call, or a full raise if stack allows
			int fullMin = Math.Max(toCall, minToAdd);

			if (fullMin <= maxBet)
			{
				// You have enough to make a full legal raise
				minBet = fullMin;
			}
			else
			{
				// Short stack: you can only go all-in as a raise, or call if you can afford it
				// From slider perspective, this is effectively "ALL IN"
				minBet = maxBet;
			}
		}

		if (minBet > maxBet)
			minBet = maxBet;

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
