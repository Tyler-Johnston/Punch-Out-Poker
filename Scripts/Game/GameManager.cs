using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	// --- GAME DATA ---
	public int PlayerMoney { get; set; } = 1000; // Starting money

	// The opponent selected for the NEXT match
	public OpponentProfile SelectedOpponent { get; set; }
	
	// Track which opponents are unlocked
	private HashSet<string> _unlockedOpponents = new HashSet<string>();

	public override void _Ready()
	{
		// Set the static instance to this node
		Instance = this;
		
		// Start with first opponent unlocked
		UnlockOpponent("Bro. Goldn");
	}
	
	/// <summary>
	/// Check if an opponent is unlocked
	/// </summary>
	public bool IsOpponentUnlocked(string opponentName)
	{
		return _unlockedOpponents.Contains(opponentName);
	}
	
	/// <summary>
	/// Unlock an opponent by name
	/// </summary>
	public void UnlockOpponent(string opponentName)
	{
		if (!_unlockedOpponents.Contains(opponentName))
		{
			_unlockedOpponents.Add(opponentName);
			GD.Print($"Unlocked opponent: {opponentName}");
		}
	}
	
	/// <summary>
	/// Check if player can afford AND has unlocked this opponent
	/// </summary>
	public bool CanPlayAgainst(OpponentProfile opponent)
	{
		return IsOpponentUnlocked(opponent.Name) && PlayerMoney >= opponent.BuyIn;
	}
	
	/// <summary>
	/// Call this when player wins a match to unlock the next opponent
	/// </summary>
	public void OnMatchWon(OpponentProfile defeatedOpponent)
	{
		// Get the circuit opponents
		var opponents = OpponentProfiles.CircuitAOpponents();
		
		// Find the defeated opponent's index
		for (int i = 0; i < opponents.Length; i++)
		{
			if (opponents[i].Name == defeatedOpponent.Name)
			{
				// Unlock the next opponent if there is one
				if (i + 1 < opponents.Length)
				{
					UnlockOpponent(opponents[i + 1].Name);
				}
				break;
			}
		}
	}
}
