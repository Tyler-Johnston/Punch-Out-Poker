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
	[Export] public TextureRect BackgroundImage { get; set; }
	
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

	private readonly List<List<OpponentData>> _circuitData = new List<List<OpponentData>>
	{
		// Index 0: Minor Circuit
		new List<OpponentData>
		{
			new OpponentData("Steve", 50),
			new OpponentData("Aryll", 125),
			new OpponentData("Boy Wizard", 250)
		},

		// Index 1: Major Circuit
		new List<OpponentData>
		{
			new OpponentData("Apprentice", 500),
			new OpponentData("Hippie", 750),
			new OpponentData("Cowboy", 1250)
		},

		// Index 2: World Circuit
		new List<OpponentData>
		{
			new OpponentData("King", 1500),
			new OpponentData("Old Wizard", 2000),
			new OpponentData("Akalite", 2500)
		}
	};

	private readonly Color[] _circuitColors = new Color[]
	{
		new Color("3a7d5e97"), // 0: Green (Minor)
		new Color("b7353797"), // 1: Red (Major)
		new Color("5f62cd97")  // 2: Blue (World)
	};

	private readonly string[] _circuitNames = new string[]
	{
		"MINOR CIRCUIT",
		"MAJOR CIRCUIT",
		"WORLD CIRCUIT"
	};

	private List<OpponentData> _currentOpponentsList;
	private int _currentIndex = 0;
	private int playerMoney = 0;
	private int _currentCircuit = 0;
	private string _lastFacedOpponent = null;
	
	public override void _Ready()
	{
		// Positioning code remains the same
		CenterPortrait.Position = new Vector2(440, 110);
		CenterFrame.Position = new Vector2(420, 90);
		
		LeftPortrait.Position = new Vector2(100, 170);
		LeftFrame.Position = new Vector2(90, 160);
		
		RightPortrait.Position = new Vector2(980, 170);
		RightFrame.Position = new Vector2(970, 160);
		
		playerMoney = GameManager.Instance.PlayerMoney;
		BalanceLabel.Text = $"Balance: ${playerMoney}";
		
		// make the background image more pixelated
		ShaderMaterial pixelMat = new ShaderMaterial();
		pixelMat.Shader = GD.Load<Shader>("res://Assets/Shaders/PixelateTexture.gdshader");
		pixelMat.SetShaderParameter("pixel_amount", 256.0f);
		BackgroundImage.Material = pixelMat;

		// check if we have a last faced opponent to resume selection
		_lastFacedOpponent = GameManager.Instance.CurrentOpponentName;
		if (!string.IsNullOrEmpty(_lastFacedOpponent))
		{
			LoadLastOpponent();
		}
		else
		{
			_currentCircuit = GameManager.Instance.GetCircuitType();
			
			// Safety clamp
			if (_currentCircuit < 0 || _currentCircuit >= _circuitData.Count) 
				_currentCircuit = 0;

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
		// Search through our List of Lists to find where the last opponent lives
		for (int circuit = 0; circuit < _circuitData.Count; circuit++)
		{
			var opponents = _circuitData[circuit];
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
		
	private void LoadCircuit(int circuitIndex)
	{
		// Safety check
		if (circuitIndex < 0 || circuitIndex >= _circuitData.Count)
		{
			GD.PrintErr($"Invalid circuit index: {circuitIndex}");
			circuitIndex = 0;
		}

		// 1. Set the Data
		_currentOpponentsList = _circuitData[circuitIndex];
		_currentIndex = 0;
		
		// 2. Set the UI Label
		if (CircuitLabel != null) 
		{
			CircuitLabel.Text = _circuitNames[circuitIndex];
		}

		// 3. Set the Background Tint (Tweened)
		if (BackgroundTint != null)
		{
			var tween = CreateTween();
			tween.TweenProperty(BackgroundTint, "color", _circuitColors[circuitIndex], 0.3f);
		}
	}

	private void OnNextCircuitPressed()
	{
		_currentCircuit++;
		
		if (_currentCircuit >= _circuitData.Count)
			_currentCircuit = 0;
		
		LoadCircuit(_currentCircuit);
		UpdateDisplay();
	}
	
	private void OnLeftPressed()
	{
		_currentIndex--;
		if (_currentIndex < 0)
			_currentIndex = _currentOpponentsList.Count - 1;
		UpdateDisplay();
	}
	
	private void OnRightPressed()
	{
		_currentIndex++;
		if (_currentIndex >= _currentOpponentsList.Count)
			_currentIndex = 0;
		UpdateDisplay();
	}
	
	private void UpdateDisplay()
	{
		if (_currentOpponentsList == null || _currentOpponentsList.Count == 0)
		{
			CenterName.Text = "Coming Soon";
			CenterBuyIn.Text = "";
			ConfirmButton.Disabled = true;
			return;
		}
	
		var current = _currentOpponentsList[_currentIndex];
		bool isCurrentLocked = !IsOpponentUnlocked(current.Name);
		
		CenterName.Text = isCurrentLocked ? "???" : current.Name;
		CenterBuyIn.Text = isCurrentLocked ? "Buy-In: ???" : $"Buy-In: ${current.BuyIn}";
		LoadPortrait(CenterPortrait, current.Name + " Large", 1.0f, new Vector2(1.0f, 1.0f), isCurrentLocked);
		
		// Left Portrait logic
		int leftIndex = _currentIndex - 1;
		if (leftIndex < 0) leftIndex = _currentOpponentsList.Count - 1;
		bool isLeftLocked = !IsOpponentUnlocked(_currentOpponentsList[leftIndex].Name);
		LoadPortrait(LeftPortrait, _currentOpponentsList[leftIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isLeftLocked);
		
		// Right Portrait logic
		int rightIndex = _currentIndex + 1;
		if (rightIndex >= _currentOpponentsList.Count) rightIndex = 0;
		bool isRightLocked = !IsOpponentUnlocked(_currentOpponentsList[rightIndex].Name);
		LoadPortrait(RightPortrait, _currentOpponentsList[rightIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isRightLocked);
		
		// Button Logic
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
	
	private bool IsOpponentUnlocked(string opponentName)
	{
		return GameManager.Instance.IsOpponentUnlocked(opponentName);
	}
	
	private void OnConfirmPressed()
	{
		var opponent = _currentOpponentsList[_currentIndex];
		
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
		
		GameManager.Instance.StartMatch(opponent.Name, opponent.BuyIn);
		GameManager.Instance.SetCircuitType(_currentCircuit);
		
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
