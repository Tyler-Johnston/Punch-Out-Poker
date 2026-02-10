using Godot;
using System;

public static class PokerRules 
{
	public struct RefundCalculation 
	{
		public int RefundAmount;
		public int FromStreet; // How much came from the current street buffer
		public int FromPot;    // How much needs to come from the main settled pot
	}

	/// <summary>
	/// Calculates refund breakdown between unsettled street bets and settled main pot.
	/// </summary>
	/// <param name="excessAmount">Positive integer: Amount player contributed over opponent</param>
	/// <param name="unsettledStreetBet">Positive integer: Chips currently in the street buffer for this player</param>
	public static RefundCalculation CalculateRefund(int excessAmount, int unsettledStreetBet)
	{
		// 1. Ensure strictly positive input handling
		int refundTotal = Math.Max(0, excessAmount);

		// 2. Take as much as possible from the unsettled street buffer
		int fromStreet = Math.Min(refundTotal, unsettledStreetBet);

		// 3. The rest must come from the settled pot
		int fromPot = refundTotal - fromStreet;

		return new RefundCalculation 
		{
			RefundAmount = refundTotal,
			FromStreet = fromStreet,
			FromPot = fromPot
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

		int increment = (lastFullRaiseIncrement > 0)
			? lastFullRaiseIncrement
			: Math.Max(currentBet - previousBet, bigBlind);

		return currentBet + increment;
	}
}
