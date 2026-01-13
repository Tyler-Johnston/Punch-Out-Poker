using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	// --- DEV TEST MODE ---
	public bool DevTestMode = false;

	// --- GAME DATA ---
	public int PlayerMoney { get; set; } = 1000;
	
	public OpponentProfile SelectedOpponent { get; set; }
	public OpponentProfile LastFacedOpponent { get; set; }
	
	private HashSet<string> _unlockedOpponents = new HashSet<string>();

	public override void _Ready()
	{
		Instance = this;
		if (DevTestMode)
		{
			GD.Print("=== DEV TEST MODE ENABLED ===");
			InitializeDevMode();
		}
		else
		{
			UnlockOpponent(OpponentProfiles.CircuitAOpponents()[0].Name);
		}
	}
	
	/// <summary>
	/// Initialize dev mode with max money and all opponents unlocked
	/// </summary>
	private void InitializeDevMode()
	{
		// Give max money
		PlayerMoney = 999999;
		GD.Print($"Dev Mode: Set money to ${PlayerMoney}");
		
		// Unlock all Circuit A opponents
		foreach (var opponent in OpponentProfiles.CircuitAOpponents())
		{
			UnlockOpponent(opponent.Name);
		}
		
		// Unlock all Circuit B opponents
		foreach (var opponent in OpponentProfiles.CircuitBOpponents())
		{
			UnlockOpponent(opponent.Name);
		}
		
		 //Unlock Circuit C
		 foreach (var opponent in OpponentProfiles.CircuitCOpponents())
		 {
			 UnlockOpponent(opponent.Name);
		 }
		
		GD.Print($"Dev Mode: Unlocked {_unlockedOpponents.Count} opponents");
	}
	
	/// <summary>
	/// Check if an opponent is unlocked
	/// </summary>
	public bool IsOpponentUnlocked(string opponentName)
	{
		if (DevTestMode)
			return true;
			
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
		if (DevTestMode)
		{
			GD.Print($"Dev Mode: Skipping unlock logic (all unlocked)");
			return;
		}
			
		var circuitA = OpponentProfiles.CircuitAOpponents();
		if (TryUnlockNextInCircuit(circuitA, defeatedOpponent, out bool circuitAComplete))
		{
			if (circuitAComplete)
			{
				// Unlock first opponent of Circuit B
				var nextCircuit = OpponentProfiles.CircuitBOpponents();
				UnlockOpponent(nextCircuit[0].Name);
				GD.Print($"Circuit A complete! Unlocked Circuit B: {nextCircuit[0].Name}");
			}
			return;
		}
		
		var circuitB = OpponentProfiles.CircuitBOpponents();
		if (TryUnlockNextInCircuit(circuitB, defeatedOpponent, out bool circuitBComplete))
		{
			if (circuitBComplete)
			{
				// Unlock first opponent of Circuit C
				var nextCircuit = OpponentProfiles.CircuitCOpponents();
				UnlockOpponent(nextCircuit[0].Name);
				GD.Print($"Circuit B complete! Unlocked Circuit C: {nextCircuit[0].Name}");
			}
			return;
		}
		
		var circuitC = OpponentProfiles.CircuitCOpponents();
		if (TryUnlockNextInCircuit(circuitC, defeatedOpponent, out bool circuitCComplete))
		{
			if (circuitCComplete)
			{
				GD.Print($"🎉 ALL CIRCUITS COMPLETE! You've beaten everyone!");
			}
			return;
		}
	}

	
	/// <summary>
	/// Helper method to unlock next opponent in a circuit
	/// </summary>
	private bool TryUnlockNextInCircuit(OpponentProfile[] circuit, OpponentProfile defeatedOpponent, out bool circuitComplete)
	{
		circuitComplete = false;
		
		for (int i = 0; i < circuit.Length; i++)
		{
			if (circuit[i].Name == defeatedOpponent.Name)
			{
				if (i + 1 < circuit.Length)
				{
					// Unlock next in same circuit
					UnlockOpponent(circuit[i + 1].Name);
					GD.Print($"Unlocked next opponent: {circuit[i + 1].Name}");
				}
				else
				{
					circuitComplete = true;
				}
				return true;
			}
		}
		return false;
	}
}
