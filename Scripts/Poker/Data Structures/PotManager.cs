using System;

public class PotManager
{
	// The main settled pot in the middle of the table
	public int MainPot { get; private set; }

	// Chips placed in front of players on the CURRENT street
	public int PlayerStreetBet { get; private set; }
	public int OpponentStreetBet { get; private set; }

	// Betting logic trackers
	public int CurrentBet { get; private set; }
	public int PreviousBet { get; private set; }
	public int LastRaiseAmount { get; private set; }

	// Total contributed to the pot over the whole hand (useful for refunds/stats)
	public int PlayerTotalContributed { get; private set; }
	public int OpponentTotalContributed { get; private set; }

	public void ResetForNewHand()
	{
		MainPot = 0;
		PlayerTotalContributed = 0;
		OpponentTotalContributed = 0;
		ResetForNewStreet();
	}

	public void ResetForNewStreet()
	{
		PlayerStreetBet = 0;
		OpponentStreetBet = 0;
		CurrentBet = 0;
		PreviousBet = 0;
		LastRaiseAmount = 0;
	}

	/// <summary>
	/// Records a bet made by a player on the current street.
	/// </summary>
	public void AddBet(bool isPlayer, int amount)
	{
		if (isPlayer)
		{
			PlayerStreetBet += amount;
			PlayerTotalContributed += amount;
		}
		else
		{
			OpponentStreetBet += amount;
			OpponentTotalContributed += amount;
		}

		UpdateBetTrackers(isPlayer ? PlayerStreetBet : OpponentStreetBet);
	}
	
	public void RemoveBet(bool isPlayer, int amount)
	{
		if (amount <= 0) return;
		if (isPlayer)
		{
			PlayerStreetBet = Math.Max(0, PlayerStreetBet - amount);
			PlayerTotalContributed = Math.Max(0, PlayerTotalContributed - amount);
		}
		else
		{
			OpponentStreetBet = Math.Max(0, OpponentStreetBet - amount);
			OpponentTotalContributed = Math.Max(0, OpponentTotalContributed - amount);
		}
	}

	private void UpdateBetTrackers(int totalStreetBetForActor)
	{
		if (totalStreetBetForActor > CurrentBet)
		{
			PreviousBet = CurrentBet;
			LastRaiseAmount = totalStreetBetForActor - CurrentBet;
			CurrentBet = totalStreetBetForActor;
		}
	}

	/// <summary>
	/// Sweeps the current street bets into the main pot. Call this when advancing the street.
	/// </summary>
	public void SettleStreetIntoPot()
	{
		MainPot += PlayerStreetBet + OpponentStreetBet;
		PlayerStreetBet = 0;
		OpponentStreetBet = 0;
		CurrentBet = 0;
		PreviousBet = 0;
	}

	/// <summary>
	/// Calculates the total effective pot (Main Pot + Unsettled Street Bets)
	/// </summary>
	public int GetEffectivePot()
	{
		return MainPot + PlayerStreetBet + OpponentStreetBet;
	}
	
	public void SetLastRaiseAmount(int amount)
	{
		LastRaiseAmount = amount;
	}

	/// <summary>
	/// Calculates uncalled chips based on TOTAL contributions and removes them from the pot/street.
	/// Returns a tuple of (playerRefund, opponentRefund).
	/// </summary>
	public (int playerRefund, int opponentRefund) CalculateAndProcessRefunds()
	{
		int playerRefund = 0;
		int opponentRefund = 0;

		int diff = PlayerTotalContributed - OpponentTotalContributed;

		if (diff > 0) // Player contributed more
		{
			var result = PokerRules.CalculateRefund(diff, PlayerStreetBet);
			
			PlayerStreetBet -= result.FromStreet;
			MainPot -= result.FromPot;
			PlayerTotalContributed -= result.RefundAmount;
			
			playerRefund = result.RefundAmount;
		}
		else if (diff < 0) // Opponent contributed more
		{
			int opponentDiff = Math.Abs(diff);
			var result = PokerRules.CalculateRefund(opponentDiff, OpponentStreetBet);

			OpponentStreetBet -= result.FromStreet;
			MainPot -= result.FromPot;
			OpponentTotalContributed -= result.RefundAmount;
			
			opponentRefund = result.RefundAmount;
		}

		return (playerRefund, opponentRefund);
	}
}
