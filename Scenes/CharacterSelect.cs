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
	
	[Export] public Label BalanceLabel { get; set; }
	
	private OpponentProfile[] _opponents;
	private int _currentIndex = 0;
	private int playerMoney = 0;
	
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
	
		// Load opponents from your existing profiles
		_opponents = OpponentProfiles.CircuitAOpponents();
		
		// Connect button signals
		LeftArrow.Pressed += OnLeftPressed;
		RightArrow.Pressed += OnRightPressed;
		ConfirmButton.Pressed += OnConfirmPressed;
		
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
		
		// Update center (main) opponent
		CenterName.Text = isCurrentLocked ? "???" : current.Name;
		CenterBuyIn.Text = isCurrentLocked ? "Buy-In: ???" : $"Buy-In: ${current.BuyIn}";
		LoadPortrait(CenterPortrait, current.Name + " Large", 1.0f, new Vector2(1.0f, 1.0f), isCurrentLocked);
		
		// Calculate left index (wrap around)
		int leftIndex = _currentIndex - 1;
		if (leftIndex < 0) leftIndex = _opponents.Length - 1;
		bool isLeftLocked = !IsOpponentUnlocked(_opponents[leftIndex]);
		LoadPortrait(LeftPortrait, _opponents[leftIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isLeftLocked);
		
		// Calculate right index (wrap around)
		int rightIndex = _currentIndex + 1;
		if (rightIndex >= _opponents.Length) rightIndex = 0;
		bool isRightLocked = !IsOpponentUnlocked(_opponents[rightIndex]);
		LoadPortrait(RightPortrait, _opponents[rightIndex].Name + " Small", 0.5f, new Vector2(0.7f, 0.7f), isRightLocked);
		
		// Check if player can afford AND has unlocked
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
			
			// Apply silhouette effect if locked
			if (isLocked)
			{
				ShaderMaterial silhouetteMat = new ShaderMaterial();
				silhouetteMat.Shader = GD.Load<Shader>("res://Assets/Shaders/Locked.gdshader");
				portraitNode.Material = silhouetteMat;
			}
			else
			{
				// Remove shader material when unlocked
				portraitNode.Material = null;
			}
		}
		else
		{
			GD.PrintErr($"Portrait not found: {portraitPath}");
		}
		
		// Apply fade effect to side portraits
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
		
		// Check if opponent is unlocked and player can afford
		if (!GameManager.Instance.CanPlayAgainst(opponent))
		{
			GD.Print($"Cannot play! Opponent locked or insufficient funds.");
			return;
		}
		
		// Set selected opponent and load game
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
		}
	}
}
