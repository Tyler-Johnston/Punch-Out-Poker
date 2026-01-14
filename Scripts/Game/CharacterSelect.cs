using Godot;
using System;
using System.Collections.Generic;

public partial class CharacterSelect : Control
{
	[Export] public TextureRect CenterPortrait { get; set; }
	[Export] public TextureRect LeftPortrait { get; set; }
	[Export] public TextureRect RightPortrait { get; set; }
	
	[Export] public TextureRect CenterFrame { get; set; }
	[Export] public TextureRect LeftFrame { get; set; }
	[Export] public TextureRect RightFrame { get; set; }
	
	[Export] public Label CenterName { get; set; }
	[Export] public Label CenterBuyIn { get; set; }
	
	[Export] public Button LeftArrow { get; set; }
	[Export] public Button RightArrow { get; set; }
	[Export] public Button ConfirmButton { get; set; }
	[Export] public Button NextCircuitButton { get; set; }
	
	[Export] public Label BalanceLabel { get; set; }
	[Export] public Label CircuitLabel { get; set; }
	[Export] public ColorRect BackgroundTint { get; set; }
	
	private class OpponentData
	{
		public string Name { get; set; }
		public int BuyIn { get; set; }
		public string PersonalityPreset { get; set; }
		
		public OpponentData(string name, int buyIn, string preset = null)
		{
			Name = name;
			BuyIn = buyIn;
			PersonalityPreset = preset ?? name;
		}
	}
	
	private List<OpponentData> _opponents;
	private int _currentIndex = 0;
	private int playerMoney = 0;
	private int _currentCircuit = 0;
	private string _lastFacedOpponent = null;
	
	public override void _Ready()
	{
		CenterPortrait.Position = new Vector2(440, 110);
		CenterFrame.Position = new Vector2(420, 90);
		
		LeftPortrait.Position = new Vector2(100, 170);
		LeftFrame.Position = new Vector2(90, 160);
		
		RightPortrait.Position = new Vector2(980, 170);
		RightFrame.Position = new Vector2(970, 160);
		
		playerMoney = GameManager.Instance.PlayerMoney;
		BalanceLabel.Text = $"Balance: ${playerMoney}";
	
		// Check if we have a last faced opponent
		_lastFacedOpponent = GameManager.Instance.CurrentOpponentName;
		
		if (!string.IsNullOrEmpty(_lastFacedOpponent))
		{
			LoadLastOpponent();
		}
		else
		{
			LoadCircuit(_currentCircuit);
		}
		
		LeftArrow.Pressed += OnLeftPressed;
		RightArrow.Pressed += OnRightPressed;
		ConfirmButton.Pressed += OnConfirmPressed;
		
		if (NextCircuitButton != null)
		{
			NextCircuitButton.Pressed += OnNextCircuitPressed;
		}
		
		UpdateDisplay();
	}
	
	private void LoadLastOpponent()
	{
		// Try to find the opponent across all circuits
		for (int circuit = 0; circuit <= 2; circuit++)
		{
			var opponents = GetCircuitOpponents(circuit);
			for (int i = 0; i < opponents.Count; i++)
			{
				if (opponents[i].Name == _lastFacedOpponent)
				{
					_currentCircuit = circuit;
					LoadCircuit(circuit);
					_currentIndex = i;
					return;
				}
			}
		}
		
		// If not found, default to Circuit 0
		LoadCircuit(0);
	}
	
	/// <summary>
	/// Define opponents for each circuit
	/// </summary>
	private List<OpponentData> GetCircuitOpponents(int circuitIndex)
	{
		switch (circuitIndex)
		{
			case 0: // Minor Circuit (Circuit A)
				return new List<OpponentData>
				{
					new OpponentData("Steve", 50),
					new OpponentData("Aryll", 125),
					new OpponentData("Boy Wizard", 250)
				};
				
			case 1: // Major Circuit (Circuit B)
				return new List<OpponentData>
				{
					new OpponentData("Cowboy", 500),
					new OpponentData("Hippie", 750),
					new OpponentData("Rumi", 1250)
				};
				
			case 2: // World Circuit (Circuit C)
				return new List<OpponentData>
				{
					new OpponentData("King", 1500),
					new OpponentData("Old Wizard", 2000),
					new OpponentData("Spade", 2500)
				};
				
			default:
				return new List<OpponentData>
				{
					new OpponentData("Steve", 50)
				};
		}
	}
		
	private void LoadCircuit(int circuitIndex)
	{
		_opponents = GetCircuitOpponents(circuitIndex);
		_currentIndex = 0;
		
		switch (circuitIndex)
		{
			case 0:
				if (CircuitLabel != null) CircuitLabel.Text = "MINOR CIRCUIT";
				
				if (BackgroundTint != null)
				{
					var tween = CreateTween();
					tween.TweenProperty(BackgroundTint, "color", new Color("3a7d5e97"), 0.3f);
				}
				break;
				
			case 1:
				if (CircuitLabel != null) CircuitLabel.Text = "MAJOR CIRCUIT";
				
				if (BackgroundTint != null)
				{
					var tween = CreateTween();
					tween.TweenProperty(BackgroundTint, "color", new Color("ed676397"), 0.3f);
				}
				break;
				
			case 2:
				if (CircuitLabel != null) CircuitLabel.Text = "WORLD CIRCUIT";
				if (BackgroundTint != null)
				{
					var tween = CreateTween();
					tween.TweenProperty(BackgroundTint, "color", new Color("#5f62cd97"), 0.3f);
				}
				break;
			
			default:
				if (CircuitLabel != null) CircuitLabel.Text = "MINOR CIRCUIT";
				if (BackgroundTint != null)
				{
					var tween = CreateTween();
					tween.TweenProperty(BackgroundTint, "color", new Color("3a7d5e97"), 0.3f);
				}
				break;
		}
	}

	
	private void OnNextCircuitPressed()
	{
		_currentCircuit++;
		
		if (_currentCircuit > 2)
			_currentCircuit = 0;
		
		LoadCircuit(_currentCircuit);
		UpdateDisplay();
	}
	
	private void OnLeftPressed()
	{
		_currentIndex--;
		if (_currentIndex < 0)
			_currentIndex = _opponents.Count - 1;
		UpdateDisplay();
	}
	
	private void OnRightPressed()
	{
		_currentIndex++;
		if (_currentIndex >= _opponents.Count)
			_currentIndex = 0;
		UpdateDisplay();
	}
	
	private void UpdateDisplay()
	{
		if (_opponents == null || _opponents.Count == 0)
		{
			CenterName.Text = "Coming Soon";
			CenterBuyIn.Text = "";
			ConfirmButton.Disabled = true;
			return;
		}
	
		var current = _opponents[_currentIndex];
		bool isCurrentLocked = !IsOpponentUnlocked(current.Name);
		
		CenterName.Text = isCurrentLocked ? "???" : current.Name;
		CenterBuyIn.Text = isCurrentLocked ? "Buy-In: ???" : $"Buy-In: ${current.BuyIn}";
		LoadPortrait(CenterPortrait, current.Name + " Large", 1.0f, new Vector2(1.0f, 1.0f), isCurrentLocked);
		
		int leftIndex = _currentIndex - 1;
		if (leftIndex < 0) leftIndex = _opponents.Count - 1;
		bool isLeftLocked = !IsOpponentUnlocked(_opponents[leftIndex].Name);
		LoadPortrait(LeftPortrait, _opponents[leftIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isLeftLocked);
		
		int rightIndex = _currentIndex + 1;
		if (rightIndex >= _opponents.Count) rightIndex = 0;
		bool isRightLocked = !IsOpponentUnlocked(_opponents[rightIndex].Name);
		LoadPortrait(RightPortrait, _opponents[rightIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isRightLocked);
		
		bool canPlay = GameManager.Instance.CanPlayAgainst(current.Name, current.BuyIn);
		ConfirmButton.Disabled = !canPlay || isCurrentLocked;
		
		if (isCurrentLocked)
		{
			ConfirmButton.Text = "LOCKED";
		}
		else if (canPlay)
		{
			ConfirmButton.Text = "PLAY!";
		}
		else
		{
			ConfirmButton.Text = $"Need ${current.BuyIn}";
		}
	}
	
	private void LoadPortrait(TextureRect portraitNode, string opponentName, float alpha, Vector2 scale, bool isLocked = false)
	{
		string portraitPath = $"res://Assets/Textures/Portraits/{opponentName}.png";
		
		if (ResourceLoader.Exists(portraitPath))
		{
			portraitNode.Texture = GD.Load<Texture2D>(portraitPath);
			
			if (isLocked)
			{
				ShaderMaterial silhouetteMat = new ShaderMaterial();
				silhouetteMat.Shader = GD.Load<Shader>("res://Assets/Shaders/Locked.gdshader");
				portraitNode.Material = silhouetteMat;
			}
			else
			{
				portraitNode.Material = null;
			}
		}
		else
		{
			GD.PrintErr($"Portrait not found: {portraitPath}");
		}
		
		portraitNode.Modulate = new Color(1, 1, 1, alpha);
		portraitNode.Scale = scale;
	}
	
	/// <summary>
	/// Check if opponent is unlocked using GameManager
	/// </summary>
	private bool IsOpponentUnlocked(string opponentName)
	{
		return GameManager.Instance.IsOpponentUnlocked(opponentName);
	}
	
	private void OnConfirmPressed()
	{
		var opponent = _opponents[_currentIndex];
		
		if (!IsOpponentUnlocked(opponent.Name))
		{
			GD.Print($"Opponent {opponent.Name} is locked!");
			return;
		}
		
		if (!GameManager.Instance.CanPlayAgainst(opponent.Name, opponent.BuyIn))
		{
			GD.Print($"Cannot play! Insufficient funds. Need ${opponent.BuyIn}, have ${playerMoney}");
			return;
		}
		
		// Set opponent in GameManager and start match
		GameManager.Instance.StartMatch(opponent.Name, opponent.BuyIn);
		
		GD.Print($"Starting match vs {opponent.Name} (Buy-in: ${opponent.BuyIn})");
		GetTree().ChangeSceneToFile("res://Scenes/PokerGame.tscn");
	}
	
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			if (keyEvent.Keycode == Key.Left || keyEvent.Keycode == Key.A)
				OnLeftPressed();
			else if (keyEvent.Keycode == Key.Right || keyEvent.Keycode == Key.D)
				OnRightPressed();
			else if (keyEvent.Keycode == Key.Enter || keyEvent.Keycode == Key.Space)
				OnConfirmPressed();
			else if (keyEvent.Keycode == Key.Tab)
				OnNextCircuitPressed();
		}
	}
}
