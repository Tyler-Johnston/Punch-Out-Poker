using Godot;
using System;
using System.Collections.Generic;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[Export] public bool DevTestMode = true;
	public int circuitType = 0; // 0 = Minor, 1 = Major, 2 = World

	// game data
	public int PlayerMoney { get; set; } = 1000;
	
	// Track which AI opponents have been defeated
	private HashSet<string> _unlockedOpponents = new HashSet<string>();
	private HashSet<string> _defeatedOpponents = new HashSet<string>();
	
	// current match data
	public string CurrentOpponentName { get; set; }
	public int CurrentBuyIn { get; set; }

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
			UnlockOpponent("Steve");
		}
	}
	
	// circuitType 0 = Minor, 1 = Major, 2 = World
	public int GetCircuitType()
	{
		return circuitType;
	}
	
	public void SetCircuitType(int newCircuitType)
	{
		circuitType = newCircuitType;
	}
	
	/// <summary>
	/// Initialize dev mode with max money and all opponents unlocked
	/// </summary>
	private void InitializeDevMode()
	{
		PlayerMoney = 999999;
		GD.Print($"Dev Mode: Set money to ${PlayerMoney}");
		
		// Unlock all opponents
		UnlockOpponent("Steve");
		UnlockOpponent("Aryll");
		UnlockOpponent("Boy Wizard");
		UnlockOpponent("Apprentice");
		UnlockOpponent("Hippie");
		UnlockOpponent("Cowboy");
		UnlockOpponent("King");
		UnlockOpponent("Old Wizard");
		UnlockOpponent("Akalite");
				
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
	/// Check if player has already defeated this opponent
	/// </summary>
	public bool HasDefeatedOpponent(string opponentName)
	{
		return _defeatedOpponents.Contains(opponentName);
	}
	
	/// <summary>
	/// Check if player can afford this buy-in amount
	/// </summary>
	public bool CanAffordBuyIn(int buyIn)
	{
		return PlayerMoney >= buyIn;
	}
	
	/// <summary>
	/// Check if player can play against this opponent
	/// </summary>
	public bool CanPlayAgainst(string opponentName, int buyIn)
	{
		return IsOpponentUnlocked(opponentName) && CanAffordBuyIn(buyIn);
	}
	
	/// <summary>
	/// Deduct buy-in when starting a match
	/// </summary>
	public void StartMatch(string opponentName, int buyIn)
	{
		CurrentOpponentName = opponentName;
		CurrentBuyIn = buyIn;
		PlayerMoney -= buyIn;
		GD.Print($"Started match vs {opponentName}. Buy-in: ${buyIn}. Remaining: ${PlayerMoney}");
	}
	
	/// <summary>
	/// Award winnings when player wins
	/// </summary>
	public void OnMatchWon(string defeatedOpponent, int winnings)
	{
		PlayerMoney += winnings;
		
		// Mark as defeated
		if (!_defeatedOpponents.Contains(defeatedOpponent))
		{
			_defeatedOpponents.Add(defeatedOpponent);
			GD.Print($"Defeated {defeatedOpponent} for the first time!");
		}
		
		GD.Print($"Won ${winnings}! Total money: ${PlayerMoney}");
		
		// Unlock next opponent based on progression
		UnlockNextOpponent(defeatedOpponent);
	}
	
	/// <summary>
	/// Handle match loss
	/// </summary>
	public void OnMatchLost(string opponent)
	{
		GD.Print($"Lost to {opponent}. Money remaining: ${PlayerMoney}");
		
		// Check for game over
		if (PlayerMoney <= 0)
		{
			GD.Print("GAME OVER - No money left!");
		}
	}
	
	/// <summary>
	/// Unlock progression system
	/// </summary>
	private void UnlockNextOpponent(string defeatedOpponent)
	{
		if (DevTestMode)
		{
			GD.Print("Dev Mode: All opponents already unlocked");
			return;
		}
		
		// Define your progression order
		switch (defeatedOpponent)
		{
			case "Steve":
				UnlockOpponent("Aryll");
				break;
				
			case "Aryll":
				UnlockOpponent("Boy Wizard");
				break;
				
			case "Boy Wizard":
				UnlockOpponent("Apprentice");
				break;
				
			case "Apprentice":
				UnlockOpponent("Hippie");
				break;
				
			case "Hippie":
				UnlockOpponent("Cowboy");
				break;
				
			case "Cowboy":
				UnlockOpponent("King");
				break;
				
				
			case "King":
				UnlockOpponent("Old Wizard");
				break;
				
			case "Akalite":
				break;
		}
	}
	
	/// <summary>
	/// Get list of all unlocked opponent names
	/// </summary>
	public List<string> GetUnlockedOpponents()
	{
		return new List<string>(_unlockedOpponents);
	}
	
	/// <summary>
	/// Save/Load helpers
	/// </summary>
	public Dictionary<string, Variant> GetSaveData()
	{
		return new Dictionary<string, Variant>
		{
			{ "money", PlayerMoney },
			{ "unlocked", new Godot.Collections.Array<string>(_unlockedOpponents) },
			{ "defeated", new Godot.Collections.Array<string>(_defeatedOpponents) }
		};
	}
	
	public void LoadSaveData(Dictionary<string, Variant> data)
	{
		PlayerMoney = data["money"].AsInt32();
		
		_unlockedOpponents.Clear();
		foreach (var name in data["unlocked"].AsStringArray())
		{
			_unlockedOpponents.Add(name);
		}
		
		_defeatedOpponents.Clear();
		foreach (var name in data["defeated"].AsStringArray())
		{
			_defeatedOpponents.Add(name);
		}
		
		GD.Print($"Loaded save: ${PlayerMoney}, {_unlockedOpponents.Count} unlocked");
	}
}
