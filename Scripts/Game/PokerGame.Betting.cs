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
		int amountToCall = currentBet - playerBet;
		int maxBet = playerChips - amountToCall; 
		
		if (maxBet <= 0) return (0, 0);

		bool opening = currentBet == 0;
		int minBet;

		if (opening)
		{
			minBet = Math.Min(bigBlind, maxBet);
		}
		else
		{
			int minRaiseIncrement = (amountToCall == 0) ? bigBlind : amountToCall;
			minRaiseIncrement = Math.Max(minRaiseIncrement, bigBlind);

			minBet = minRaiseIncrement;
			
			// Cap at calculated maxBet
			if (minBet > maxBet)
			{
				minBet = maxBet; 
			}
		}

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
