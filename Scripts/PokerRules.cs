using Godot;
using System;

/// <summary>
/// Pure logic class for Poker mechanics.
/// Stateless: Input -> Math -> Output.
/// </summary>
public static class PokerRules 
{
	public struct RefundCalculation 
	{
		public int RefundAmount;
		public int FromStreet; // How much to pull from current street bets
		// public int FromPot; // (Reserved for future side-pot logic)
	}

	/// <summary>
	/// Calculates how much to refund a player if they are over-committed (e.g. uncalled bet).
	/// </summary>
	public static RefundCalculation CalculateRefund(int amountToCallNegative, int currentActorStreetBet)
	{
		int rawRefund = -amountToCallNegative;
		
		// You cannot be refunded more than you put in this street 
		// (unless we are handling complex side-pot settlements, but for standard uncalled bets: limit to street bet)
		int actualRefund = Math.Min(rawRefund, currentActorStreetBet);

		return new RefundCalculation 
		{
			RefundAmount = actualRefund,
			FromStreet = actualRefund
		};
	}

	/// <summary>
	/// Determines if an action constitutes a "Full Raise" which reopens betting.
	/// </summary>
	public static bool IsFullRaise(int raiseIncrement, int minRaiseIncrement)
	{
		return raiseIncrement >= minRaiseIncrement;
	}

	/// <summary>
	/// Calculates the minimum legal Total Bet amount for a raise.
	/// </summary>
	public static int CalculateMinRaiseTotal(int currentBet, int previousBet, int lastFullRaiseIncrement, int bigBlind)
	{
		// If it's an opening bet (currentBet is 0)
		if (currentBet <= 0) return bigBlind;

		// Use the last FULL raise increment. 
		// If lastFullRaiseIncrement is 0 (first raise of street), calculate difference from previous.
		// Fallback to BigBlind if math is weird.
		int increment = (lastFullRaiseIncrement > 0)
			? lastFullRaiseIncrement
			: Math.Max(currentBet - previousBet, bigBlind);

		return currentBet + increment;
	}
}
