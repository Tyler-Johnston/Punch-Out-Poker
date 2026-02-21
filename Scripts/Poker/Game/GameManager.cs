using Godot;
using System;
using System.Collections.Generic;
using Godot.Collections;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[Export] public bool DevTestMode = true;
	[Export] public bool ShowDetailedLogs { get; set; } = false;
	public int circuitType = 0; // 0 = Minor, 1 = Major, 2 = World

	// Game Data
	public int PlayerMoney { get; set; } = 1000;

	// Track which AI opponents have been defeated
	private HashSet<string> _unlockedOpponents = new HashSet<string>();
	private HashSet<string> _defeatedOpponents = new HashSet<string>();

	// Current match data
	public string CurrentOpponentName { get; set; }
	public int CurrentBuyIn { get; set; }

	private System.Collections.Generic.Dictionary<(Rank, Suit), Texture2D> _cardTextureCache = new System.Collections.Generic.Dictionary<(Rank, Suit), Texture2D>();
	private Texture2D _cardBackTexture;

	public override void _Ready()
	{
		Instance = this;

		LoadCardAssets();

		if (DevTestMode)
		{
			GD.Print("\n=== DEV TEST MODE ENABLED ===");
			InitializeDevMode();
		}
		else
		{
			UnlockOpponent("Steve");
		}
	}
	
	private void LoadCardAssets()
	{
		foreach (Suit suit in Enum.GetValues(typeof(Suit)))
		{
			foreach (Rank rank in Enum.GetValues(typeof(Rank)))
			{
				string filename = GetCardFilename(rank, suit);
				string path = $"res://Assets/Textures/Poker/card_pngs/card_faces/{filename}";

				Texture2D tex = GD.Load<Texture2D>(path);
				if (tex != null)
				{
					_cardTextureCache[(rank, suit)] = tex;
				}
				else
				{
					GD.PrintErr($"Failed to load: {path}");
				}
			}
		}

		UpdateCardBackTexture();
		
		GD.Print($"GameManager: Cached {_cardTextureCache.Count} card textures.");
	}

	/// <summary>
	/// Updates the card back texture based on current Circuit Type.
	/// Call this when changing circuits.
	/// </summary>
	public void UpdateCardBackTexture()
	{
		string path = $"res://Assets/Textures/Poker/card_pngs/card_backs/card_back_{circuitType + 1}.png";
		_cardBackTexture = GD.Load<Texture2D>(path);
		
		if (_cardBackTexture == null)
		{
			GD.PrintErr($"Failed to load card back: {path}");
		}
	}

	public Texture2D GetCardTexture(Rank rank, Suit suit)
	{
		if (_cardTextureCache.TryGetValue((rank, suit), out Texture2D tex))
		{
			return tex;
		}
		return null;
	}

	public Texture2D GetCardBackTexture()
	{
		return _cardBackTexture;
	}

	private string GetCardFilename(Rank rank, Suit suit)
	{
		string rankStr = rank switch
		{
			Rank.Two => "2", Rank.Three => "3", Rank.Four => "4", Rank.Five => "5",
			Rank.Six => "6", Rank.Seven => "7", Rank.Eight => "8", Rank.Nine => "9",
			Rank.Ten => "10", Rank.Jack => "jack", Rank.Queen => "queen", Rank.King => "king",
			Rank.Ace => "ace", _ => "2"
		};

		string suitStr = suit switch
		{
			Suit.Clubs => "clubs", Suit.Diamonds => "diamonds",
			Suit.Hearts => "hearts", Suit.Spades => "spades", _ => "clubs"
		};

		return $"{rankStr}_of_{suitStr}.png";
	}

	public int GetCircuitType() => circuitType;
	public void SetCircuitType(int newCircuitType)
	{
		circuitType = newCircuitType;
		UpdateCardBackTexture();
	}

	private void InitializeDevMode()
	{
		PlayerMoney = 999999;
		GD.Print($"Dev Mode: Set money to ${PlayerMoney}");

		UnlockOpponent("Steve");
		UnlockOpponent("Aryll");
		UnlockOpponent("Boy Wizard");
		UnlockOpponent("Apprentice");
		UnlockOpponent("Malandro");
		UnlockOpponent("Cowboy");
		UnlockOpponent("King");
		UnlockOpponent("Old Wizard");
		UnlockOpponent("Akalite");

		GD.Print($"Dev Mode: Unlocked {_unlockedOpponents.Count} opponents");
	}
	
   /// <summary>
	/// Use this for heavy technical logs (timers, math, state flags)
	/// </summary>
	public static void LogVerbose(string message)
	{
		if (Instance.ShowDetailedLogs)
		{
			GD.Print($"[VERBOSE] {message}");
		}
	}

	public bool IsOpponentUnlocked(string opponentName)
	{
		if (DevTestMode) return true;
		return _unlockedOpponents.Contains(opponentName);
	}

	public void UnlockOpponent(string opponentName)
	{
		if (!_unlockedOpponents.Contains(opponentName))
		{
			_unlockedOpponents.Add(opponentName);
			GD.Print($"Unlocked opponent: {opponentName}");
		}
	}

	public bool HasDefeatedOpponent(string opponentName) => _defeatedOpponents.Contains(opponentName);
	public bool CanAffordBuyIn(int buyIn) => PlayerMoney >= buyIn;
	public bool CanPlayAgainst(string opponentName, int buyIn) => IsOpponentUnlocked(opponentName) && CanAffordBuyIn(buyIn);

	public void StartMatch(string opponentName, int buyIn)
	{
		CurrentOpponentName = opponentName;
		CurrentBuyIn = buyIn;
		PlayerMoney -= buyIn;
		GD.Print($"Started match vs {opponentName}. Buy-in: ${buyIn}. Remaining: ${PlayerMoney}");
	}

	public void OnMatchWon(string defeatedOpponent, int winnings)
	{
		PlayerMoney += winnings;
		if (!_defeatedOpponents.Contains(defeatedOpponent))
		{
			_defeatedOpponents.Add(defeatedOpponent);
			GD.Print($"Defeated {defeatedOpponent} for the first time!");
		}
		GD.Print($"Won ${winnings}! Total money: ${PlayerMoney}");
		UnlockNextOpponent(defeatedOpponent);
	}

	public void OnMatchLost(string opponent)
	{
		GD.Print($"Lost to {opponent}. Money remaining: ${PlayerMoney}");
		if (PlayerMoney <= 0) GD.Print("GAME OVER - No money left!");
	}

	private void UnlockNextOpponent(string defeatedOpponent)
	{
		if (DevTestMode) return;

		switch (defeatedOpponent)
		{
			case "Steve": UnlockOpponent("Aryll"); break;
			case "Aryll": UnlockOpponent("Boy Wizard"); break;
			case "Boy Wizard": UnlockOpponent("Apprentice"); break;
			case "Apprentice": UnlockOpponent("Malandro"); break;
			case "Malandro": UnlockOpponent("Cowboy"); break;
			case "Cowboy": UnlockOpponent("King"); break;
			case "King": UnlockOpponent("Old Wizard"); break;
			case "Akalite": break;
		}
	}

	public System.Collections.Generic.List<string> GetUnlockedOpponents() => new System.Collections.Generic.List<string>(_unlockedOpponents);

	public System.Collections.Generic.Dictionary<string, Variant> GetSaveData()
	{
		return new System.Collections.Generic.Dictionary<string, Variant>
		{
			{ "money", PlayerMoney },
			{ "unlocked", new Godot.Collections.Array<string>(_unlockedOpponents) },
			{ "defeated", new Godot.Collections.Array<string>(_defeatedOpponents) }
		};
	}

	public void LoadSaveData(System.Collections.Generic.Dictionary<string, Variant> data)
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
