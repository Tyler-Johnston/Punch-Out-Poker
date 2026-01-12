using Godot;
using System;

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
	
	private OpponentProfile[] _opponents;
	private int _currentIndex = 0;
	private int playerMoney = 0;
	private int _currentCircuit = 0;
	
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
	
		// Check if returning from a match
		if (GameManager.Instance.LastFacedOpponent != null)
		{
			LoadLastOpponent();
		}
		else
		{
			// Load initial circuit
			LoadCircuit(_currentCircuit);
		}
		
		// Connect button signals
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
		var lastOpponent = GameManager.Instance.LastFacedOpponent;
		
		// Check Circuit A
		var circuitA = OpponentProfiles.CircuitAOpponents();
		for (int i = 0; i < circuitA.Length; i++)
		{
			if (circuitA[i].Name == lastOpponent.Name)
			{
				_currentCircuit = 0;
				LoadCircuit(0);
				_currentIndex = i;
				return;
			}
		}
		
		// Check Circuit B
		var circuitB = OpponentProfiles.CircuitBOpponents();
		for (int i = 0; i < circuitB.Length; i++)
		{
			if (circuitB[i].Name == lastOpponent.Name)
			{
				_currentCircuit = 1;
				LoadCircuit(1);
				_currentIndex = i;
				return;
			}
		}
		
		// Check Circuit C
		var circuitC = OpponentProfiles.CircuitCOpponents();
		for (int i = 0; i < circuitC.Length; i++)
		{
			if (circuitC[i].Name == lastOpponent.Name)
			{
				_currentCircuit = 2;
				LoadCircuit(2);
				_currentIndex = i;
				return;
			}
		}
		
		LoadCircuit(0);
	}
		
	private void LoadCircuit(int circuitIndex)
	{
		switch (circuitIndex)
		{
			case 0:
				_opponents = OpponentProfiles.CircuitAOpponents();
				if (CircuitLabel != null) CircuitLabel.Text = "MINOR CIRCUIT";
				
				if (BackgroundTint != null)
				{
					var tween = CreateTween();
					tween.TweenProperty(BackgroundTint, "color", new Color("3a7d5e97"), 0.3f);
				}
				break;
				
			case 1:
				_opponents = OpponentProfiles.CircuitBOpponents();
				if (CircuitLabel != null) CircuitLabel.Text = "MAJOR CIRCUIT";
				
				if (BackgroundTint != null)
				{
					var tween = CreateTween();
					tween.TweenProperty(BackgroundTint, "color", new Color("ed676397"), 0.3f);
				}
				break;
				
			 case 2:
				 _opponents = OpponentProfiles.CircuitCOpponents();
				 if (CircuitLabel != null) CircuitLabel.Text = "WORLD CIRCUIT";
				 if (BackgroundTint != null)
				 {
					 var tween = CreateTween();
					 tween.TweenProperty(BackgroundTint, "color", new Color("#5f62cd97"), 0.3f);
				 }
				 break;
			
			default:
				_opponents = OpponentProfiles.CircuitAOpponents();
				if (CircuitLabel != null) CircuitLabel.Text = "MINOR CIRCUIT";
				if (BackgroundTint != null)
				{
					var tween = CreateTween();
					tween.TweenProperty(BackgroundTint, "color", new Color("3a7d5e97"), 0.3f);
				}
				break;
		}
		
		_currentIndex = 0;
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
			_currentIndex = _opponents.Length - 1;
		UpdateDisplay();
	}
	
	private void OnRightPressed()
	{
		_currentIndex++;
		if (_currentIndex >= _opponents.Length)
			_currentIndex = 0;
		UpdateDisplay();
	}
	
	private void UpdateDisplay()
	{
		var current = _opponents[_currentIndex];
		bool isCurrentLocked = !IsOpponentUnlocked(current);
		
		CenterName.Text = isCurrentLocked ? "???" : current.Name;
		CenterBuyIn.Text = isCurrentLocked ? "Buy-In: ???" : $"Buy-In: ${current.BuyIn}";
		LoadPortrait(CenterPortrait, current.Name + " Large", 1.0f, new Vector2(1.0f, 1.0f), isCurrentLocked);
		
		int leftIndex = _currentIndex - 1;
		if (leftIndex < 0) leftIndex = _opponents.Length - 1;
		bool isLeftLocked = !IsOpponentUnlocked(_opponents[leftIndex]);
		LoadPortrait(LeftPortrait, _opponents[leftIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isLeftLocked);
		
		int rightIndex = _currentIndex + 1;
		if (rightIndex >= _opponents.Length) rightIndex = 0;
		bool isRightLocked = !IsOpponentUnlocked(_opponents[rightIndex]);
		LoadPortrait(RightPortrait, _opponents[rightIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isRightLocked);
		
		bool canPlay = GameManager.Instance.CanPlayAgainst(current);
		ConfirmButton.Disabled = !canPlay;
		ConfirmButton.Text = isCurrentLocked ? "LOCKED" : (canPlay ? "PLAY!" : $"Need ${current.BuyIn}");
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
	
	private bool IsOpponentUnlocked(OpponentProfile opponent)
	{
		return GameManager.Instance.IsOpponentUnlocked(opponent.Name);
	}
	
	private void OnConfirmPressed()
	{
		var opponent = _opponents[_currentIndex];
		
		if (!GameManager.Instance.CanPlayAgainst(opponent))
		{
			GD.Print($"Cannot play! Opponent locked or insufficient funds.");
			return;
		}
		
		GameManager.Instance.SelectedOpponent = opponent;
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
