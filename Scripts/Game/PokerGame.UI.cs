using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class PokerGame
{
	private Tween foldButtonTween;
	private Tween checkCallButtonTween;
	private Tween betRaiseButtonTween;
	private Tween cashOutButtonTween;
	private Tween foldIdleTween;
	private Tween checkCallIdleTween;
	private Tween betRaiseIdleTween;
	private Tween opponentViewIdleTween;
	private Tween potLabelTween;

	private const float HOVER_SCALE = 1.04f;
	private const float PRESS_SCALE = 0.97f;
	private const float HOVER_DURATION = 0.25f;         
	private const float PRESS_DURATION = 0.1f;          
	private const float RELEASE_DURATION = 0.3f;        
	private const float IDLE_FLOAT_AMOUNT = 0.005f;
	private const float IDLE_CYCLE_DURATION = 2.5f;  
	private const float IDLE_SCALE_AMOUNT = 0.005f;
	
	private const float OPPONENT_IDLE_FLOAT_AMOUNT = 2.15f;
	private const float OPPONENT_IDLE_SCALE_AMOUNT = 0.008f;
	
	private const float POT_POP_SCALE = 1.15f;
	private const float POT_POP_DURATION = 0.06f;
	private const float POT_SETTLE_DURATION = 0.10f;
	
	private readonly Dictionary<string, int> CHIP_VALUES = new Dictionary<string, int>
	{
		{ "pink", 5000 },
		{ "yellow", 1000 },
		{ "purple", 500 },
		{ "black", 100 },
		{ "green", 25 },
		{ "blue", 10 },
		{ "red", 5 },
		{ "white", 1 }
	};

	private readonly string[] CHIP_COLORS = { "pink", "yellow", "purple", "black", "green", "blue", "red", "white" };
	
	
	public void InitializeUI()
	{
		SetTableColor();
		LoadOpponentSprite();
		InitializeButtonAnimations();
		InitializeOpponentViewAnimation();
		InitializePotLabel();
		UpdateHud();
	}

	// --- BUTTON ANIMATION METHODS ---
	
	public void InitializeButtonAnimations()
	{

		SetupButtonPivot(foldButton);
		SetupButtonPivot(checkCallButton);
		SetupButtonPivot(betRaiseButton);
		SetupButtonPivot(cashOutButton);

		foldButton.MouseEntered += () => OnButtonHover(foldButton, ref foldButtonTween);
		foldButton.MouseExited += () => OnButtonUnhover(foldButton, ref foldButtonTween);
		foldButton.ButtonDown += () => OnButtonPress(foldButton, ref foldButtonTween);
		foldButton.ButtonUp += () => OnButtonRelease(foldButton, ref foldButtonTween);

		checkCallButton.MouseEntered += () => OnButtonHover(checkCallButton, ref checkCallButtonTween);
		checkCallButton.MouseExited += () => OnButtonUnhover(checkCallButton, ref checkCallButtonTween);
		checkCallButton.ButtonDown += () => OnButtonPress(checkCallButton, ref checkCallButtonTween);
		checkCallButton.ButtonUp += () => OnButtonRelease(checkCallButton, ref checkCallButtonTween);

		betRaiseButton.MouseEntered += () => OnButtonHover(betRaiseButton, ref betRaiseButtonTween);
		betRaiseButton.MouseExited += () => OnButtonUnhover(betRaiseButton, ref betRaiseButtonTween);
		betRaiseButton.ButtonDown += () => OnButtonPress(betRaiseButton, ref betRaiseButtonTween);
		betRaiseButton.ButtonUp += () => OnButtonRelease(betRaiseButton, ref betRaiseButtonTween);

		cashOutButton.MouseEntered += () => OnButtonHover(cashOutButton, ref cashOutButtonTween);
		cashOutButton.MouseExited += () => OnButtonUnhover(cashOutButton, ref cashOutButtonTween);
		cashOutButton.ButtonDown += () => OnButtonPress(cashOutButton, ref cashOutButtonTween);
		cashOutButton.ButtonUp += () => OnButtonRelease(cashOutButton, ref cashOutButtonTween);

		StartIdleAnimation(foldButton, ref foldIdleTween, 0f);
		StartIdleAnimation(checkCallButton, ref checkCallIdleTween, 0.33f);
		StartIdleAnimation(betRaiseButton, ref betRaiseIdleTween, 0.66f);
	}

	/// <summary>
	/// Sets button pivot to center for smooth scaling and prevents size drift
	/// </summary>
	private void SetupButtonPivot(Button button)
	{
		if (button == null) return;
		
		button.PivotOffset = button.Size / 2;
		button.CustomMinimumSize = button.Size;
		button.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
	}
	
	private void OnButtonHover(Button button, ref Tween buttonTween)
	{
		if (button.Disabled) return;
		
		if (buttonTween != null && buttonTween.IsValid())
			buttonTween.Kill();
		
		buttonTween = CreateTween();
		buttonTween.SetProcessMode(Tween.TweenProcessMode.Idle);
		
		buttonTween.TweenProperty(button, "modulate", new Color(1.1f, 1.1f, 1.1f), 0.15f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);
	}

	private void OnButtonUnhover(Button button, ref Tween buttonTween)
	{
		if (buttonTween != null && buttonTween.IsValid())
			buttonTween.Kill();
		
		buttonTween = CreateTween();
		buttonTween.SetProcessMode(Tween.TweenProcessMode.Idle);
		
		buttonTween.TweenProperty(button, "modulate", Colors.White, 0.15f)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);
	}

	private void OnButtonPress(Button button, ref Tween buttonTween)
	{
		if (button.Disabled) return;
		
		if (buttonTween != null && buttonTween.IsValid())
			buttonTween.Kill();
		
		// quick squash on press
		buttonTween = CreateTween();
		buttonTween.SetProcessMode(Tween.TweenProcessMode.Idle);
		buttonTween.SetEase(Tween.EaseType.Out);
		buttonTween.SetTrans(Tween.TransitionType.Quad);
		buttonTween.TweenProperty(button, "scale", Vector2.One * PRESS_SCALE, PRESS_DURATION);
	}

	private void OnButtonRelease(Button button, ref Tween buttonTween)
	{
		if (button.Disabled) return;
		
		if (buttonTween != null && buttonTween.IsValid())
			buttonTween.Kill();
		
		// spring back to normal. idle animation will take over from here
		buttonTween = CreateTween();
		buttonTween.SetProcessMode(Tween.TweenProcessMode.Idle);
		buttonTween.SetEase(Tween.EaseType.Out);
		buttonTween.SetTrans(Tween.TransitionType.Spring);
		
		// return to base scale, idle animation continues
		buttonTween.TweenProperty(button, "scale", Vector2.One, RELEASE_DURATION);
	}

	private void StartIdleAnimation(Button button, ref Tween idleTween, float timeOffset)
	{
		if (idleTween != null && idleTween.IsValid())
			idleTween.Kill();
		
		idleTween = CreateTween();
		idleTween.SetProcessMode(Tween.TweenProcessMode.Idle);
		idleTween.SetLoops();
		
		float floatAmount = IDLE_FLOAT_AMOUNT;
		float cycleDuration = IDLE_CYCLE_DURATION;
		var originalPos = button.Position;
		
		// offset starting position for staggered effect
		var startPos = originalPos + new Vector2(0, floatAmount * Mathf.Sin(timeOffset * Mathf.Pi * 2));
		button.Position = startPos;
		
		// Float UP + Scale UP (breathing in) ===
		idleTween.TweenProperty(button, "position:y", originalPos.Y - floatAmount, cycleDuration / 2)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
		
		idleTween.Parallel().TweenProperty(button, "scale", Vector2.One * (1.0f + IDLE_SCALE_AMOUNT), cycleDuration / 2)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
		
		// Float DOWN + Scale DOWN (breathing out) ===
		idleTween.TweenProperty(button, "position:y", originalPos.Y + floatAmount, cycleDuration / 2)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
		
		idleTween.Parallel().TweenProperty(button, "scale", Vector2.One * (1.0f - IDLE_SCALE_AMOUNT), cycleDuration / 2)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
	}

	private void SetButtonIdleAnimationEnabled(Button button, Tween idleTween, bool enabled)
	{
		if (idleTween != null && idleTween.IsValid())
		{
			if (enabled)
				idleTween.Play();
			else
				idleTween.Pause();
		}
	}

	// --- POT LABEL ANIMATION ---
	
	/// <summary>
	/// Initialize pot label pivot for scaling animations
	/// </summary>
	private void InitializePotLabel()
	{
		if (potLabel == null) return;
		
		potLabel.PivotOffset = potLabel.Size / 2;
	}
	
	/// <summary>
	/// Animates pot label with pop effect when value increases
	/// </summary>
	private void AnimatePotLabelPop()
	{
		if (potLabel == null) return;
		
		// Kill any existing animation
		if (potLabelTween != null && potLabelTween.IsValid())
			potLabelTween.Kill();
		
		// Ensure pivot is centered
		potLabel.PivotOffset = potLabel.Size / 2;
		
		// Create pop animation
		potLabelTween = CreateTween();
		potLabelTween.SetProcessMode(Tween.TweenProcessMode.Idle);
		
		// Scale up quickly
		potLabelTween.TweenProperty(potLabel, "scale", Vector2.One * POT_POP_SCALE, POT_POP_DURATION)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);
		
		// Settle back with slight bounce
		potLabelTween.TweenProperty(potLabel, "scale", Vector2.One, POT_SETTLE_DURATION)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Back);
		
		potLabelTween.TweenProperty(potLabel, "modulate", Colors.White, POT_SETTLE_DURATION)
			.SetEase(Tween.EaseType.Out);
	}

	// --- MESSAGE DISPLAY ---
	
	private void ShowMessage(string text)
	{
		gameStateLabel.Text = text;
	}

	// --- TABLE VISUALS ---
	
	private void SetTableColor()
	{
		Color baseColor = new Color("#52a67f");
		switch (GameManager.Instance.GetCircuitType())
		{
			case 0: baseColor = new Color("#52a67f"); break;
			case 1: baseColor = new Color("b0333dff"); break;
			case 2: baseColor = new Color("#127AE3"); break;
		}

		if (MiniTableRect != null)
		{
			var enemyViewShader = GD.Load<Shader>("res://Assets/Shaders/EnemyView.gdshader");
			
			var newGradient = new GradientTexture2D();
			newGradient.Gradient = new Gradient();
			newGradient.Gradient.SetColor(0, baseColor);
			newGradient.Gradient.SetColor(1, baseColor.Darkened(0.3f));
			newGradient.Fill = GradientTexture2D.FillEnum.Linear; 

			var mat = new ShaderMaterial();
			mat.Shader = enemyViewShader;
			
			mat.SetShaderParameter("gradient_texture", newGradient);
			mat.SetShaderParameter("border_width", 0.073f);
			mat.SetShaderParameter("pixel_factor", 0.01f);
			
			MiniTableRect.Material = mat;
		}

		if (MainTableRect != null)
		{
			var pixelShader = GD.Load<Shader>("res://Assets/Shaders/Pixelate.gdshader");
			UpdateMainTableGradient(MainTableRect, baseColor);

			var mat = new ShaderMaterial();
			mat.Shader = pixelShader;
			MainTableRect.Material = mat;
		}
	}

	private void UpdateMainTableGradient(TextureRect rect, Color baseColor)
	{
		if (rect == null) return;

		var gradTex = rect.Texture as GradientTexture2D;
		if (gradTex != null)
		{
			gradTex.Gradient = (Gradient)gradTex.Gradient.Duplicate();
			Color edgeColor = baseColor.Darkened(0.3f);

			gradTex.Gradient.SetColor(0, baseColor);
			gradTex.Gradient.SetColor(1, edgeColor);
		}
	}
	
	private async Task TossCard(CardVisual card, Card cardData, float maxAngleDegrees = 3.0f, float maxPixelOffset = 2.0f, bool revealCard=true)
	{
		Vector2 finalPosition = card.Position;
		float randomAngle = (float)GD.RandRange(-maxAngleDegrees, maxAngleDegrees);
		Vector2 randomOffset = new Vector2(
			(float)GD.RandRange(-maxPixelOffset, maxPixelOffset),
			(float)GD.RandRange(-maxPixelOffset, maxPixelOffset)
		);
		finalPosition += randomOffset;
		float finalRotation = Mathf.DegToRad(randomAngle);
		
		Vector2 startPosition = new Vector2(
			finalPosition.X + (float)GD.RandRange(-30.0, 30.0),
			finalPosition.Y + 600 
		);
		
		sfxPlayer.PlaySound("card_flip");
		if (revealCard) await card.RevealCard(cardData);
		
		card.Position = startPosition;
		card.Rotation = Mathf.DegToRad((float)GD.RandRange(-15.0, 15.0));
		card.Visible = true;
		
		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(card, "position", finalPosition, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(card, "rotation", finalRotation, 0.5f)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	// --- OPPONENT VISUALS ---
	
	/// <summary>
	/// Initialize breathing animation for the OpponentView Node2D
	/// </summary>
	private void InitializeOpponentViewAnimation()
	{
		if (opponentView == null)
		{
			GD.PrintErr("OpponentView is null - cannot initialize animation");
			return;
		}
		StartIdleOpponentView(opponentView, ref opponentViewIdleTween, 0.5f);
	}

	/// <summary>
	/// Start idle breathing animation for Node2D (opponent view)
	/// </summary>
	private void StartIdleOpponentView(Node2D node, ref Tween idleTween, float timeOffset)
	{
		if (node == null) return;
		
		if (idleTween != null && idleTween.IsValid())
			idleTween.Kill();
		
		idleTween = CreateTween();
		idleTween.SetProcessMode(Tween.TweenProcessMode.Idle);
		idleTween.SetLoops();
		
		float floatAmount = OPPONENT_IDLE_FLOAT_AMOUNT;
		float cycleDuration = IDLE_CYCLE_DURATION;
		
		float originalY = node.Position.Y;
		
		if (timeOffset > 0)
		{
			idleTween.TweenInterval(timeOffset * cycleDuration);
		}
		
		// Float UP (breathing in) ===
		idleTween.TweenProperty(node, "position:y", originalY - floatAmount, cycleDuration / 2)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
		
		// Float DOWN (breathing out) ===
		idleTween.TweenProperty(node, "position:y", originalY + floatAmount, cycleDuration / 2)
			.SetEase(Tween.EaseType.InOut)
			.SetTrans(Tween.TransitionType.Sine);
	}
	
	private void UpdateOpponentExpression(PlayerAction action)
	{
		if (aiOpponent.CurrentTiltState == TiltState.Steaming || aiOpponent.CurrentTiltState == TiltState.Monkey)
		{
			SetExpression(Expression.Angry);
			return;
		}

		switch (action)
		{
			case PlayerAction.Fold:
				SetExpression(Expression.Sad);
				break;
			case PlayerAction.Check:
				SetExpression(Expression.Neutral);
				break;
			case PlayerAction.Call:
				SetExpression(Expression.Neutral);
				break;
			case PlayerAction.Raise:
				SetExpression(Expression.Neutral);
				break;
			case PlayerAction.AllIn:
				SetExpression(Expression.Smirk);
				break;
		}
	}

	public void SetExpression(Expression expression)
	{
		if (faceSprite != null)
		{
			faceSprite.Frame = (int)expression;
		}
	}

	private void LoadOpponentSprite()
	{
		
		string opponent = currentOpponentName.ToLower();
		
		string folderPath = "res://Assets/Textures/expressions/";
		string targetPath = $"{folderPath}{opponent}_expressions.png";
		string fallbackPath = $"{folderPath}king_expressions.png";

		Texture2D loadedTexture = null;

		if (ResourceLoader.Exists(targetPath))
		{
			loadedTexture = GD.Load<Texture2D>(targetPath);
			GD.Print($"Loaded sprite: {opponent}");
		}
		else
		{
			GD.Print($"Sprite missing for {opponent}. Defaulting to King.");
			if (ResourceLoader.Exists(fallbackPath))
			{
				loadedTexture = GD.Load<Texture2D>(fallbackPath);
			}
			else
			{
				GD.PushError("CRITICAL: Fallback sprite missing!");
				return;
			}
		}

		faceSprite.Texture = loadedTexture;
		faceSprite.Hframes = 8;
		faceSprite.Vframes = 1;
		faceSprite.Frame = 0;
	}

	private void UpdateOpponentVisuals()
	{
		if (OpponentFrame == null) return;

		TiltState state = aiOpponent.CurrentTiltState;

		var currentStyle = OpponentFrame.GetThemeStylebox("panel");
		if (currentStyle is StyleBoxFlat)
		{
			StyleBoxFlat style = (StyleBoxFlat)currentStyle.Duplicate();

			switch (state)
			{
				case TiltState.Zen:
					style.BorderColor = new Color("d8d8d8");
					break;

				case TiltState.Annoyed:
					style.BorderColor = new Color("e1cb1eff");
					break;

				case TiltState.Steaming:
					style.BorderColor = new Color("be5d1bff");
					break;

				case TiltState.Monkey:
					style.BorderColor = Colors.Red;
					break;
			}

			OpponentFrame.AddThemeStyleboxOverride("panel", style);
		}
	}

	/// <summary>
	/// Determines which chip images to display for a given pot amount using standard poker chip values
	/// </summary>
	private List<string> GetChipImagesForPot(int potAmount)
	{
		if (potAmount <= 0) return new List<string>();
		
		int remaining = potAmount;
		List<string> chipStacks = new List<string>();
		
		foreach (string color in CHIP_COLORS)
		{
			int value = CHIP_VALUES[color];
			int chipCount = remaining / value;
			
			if (chipCount > 0)
			{
				// Break this chip count into visual stacks
				List<string> colorStacks = GetChipStackImages(color, chipCount);
				chipStacks.AddRange(colorStacks);
				
				remaining -= chipCount * value;
			}
			
			if (chipStacks.Count >= 6) break;
		}
		
		// Limit to 6 images max
		if (chipStacks.Count > 6)
			chipStacks = chipStacks.GetRange(0, 6);
		
		return chipStacks;
	}

	/// <summary>
	/// Converts a chip count into the right combination of image sizes
	/// _4 = 8 chips, _3 = 4 chips, _2 = 2 chips, _1 = 1 chip
	/// </summary>
	private List<string> GetChipStackImages(string color, int count)
	{
		List<string> images = new List<string>();
		
		while (count >= 8)
		{
			images.Add($"{color}_4.png");
			count -= 8;
		}
		
		if (count >= 4)
		{
			images.Add($"{color}_3.png");
			count -= 4;
		}
		
		if (count >= 2)
		{
			images.Add($"{color}_2.png");
			count -= 2;
		}
		
		if (count >= 1)
		{
			images.Add($"{color}_1.png");
		}
		
		return images;
	}
	
	private void UpdatePotDisplay(int potAmount)
	{
		if (chipContainer == null) return;
		
		// Only pop if pot increased from previous value
		bool potIncreased = (potAmount > _lastDisplayedPot && potAmount > 0);
		
		if (potAmount == _lastDisplayedPot)
			return;
		
		_lastDisplayedPot = potAmount;
		
		// Trigger pot label pop animation when pot increases
		if (potIncreased)
		{
			AnimatePotLabelPop();
		}
		
		foreach (Node child in chipContainer.GetChildren())
		{
			child.QueueFree();
		}
		
		List<string> chipImages = GetChipImagesForPot(potAmount);
		
		foreach (string chipFile in chipImages)
		{
			TextureRect chipSprite = new TextureRect();
			
			string path = $"res://Assets/Textures/chip_pngs/{chipFile}";
			if (ResourceLoader.Exists(path))
			{
				chipSprite.Texture = GD.Load<Texture2D>(path);
			}
			else
			{
				GD.PrintErr($"Chip texture not found: {path}");
				continue;
			}
			
			float width = 48;
			float height = GetChipHeight(chipFile);
			
			chipSprite.CustomMinimumSize = new Vector2(width, height);
			chipSprite.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
			chipSprite.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			
			chipContainer.AddChild(chipSprite);
			
			// Wait one frame for layout, then randomize
			GetTree().CreateTimer(0.0f).Timeout += () =>
			{
				if (chipSprite == null || !IsInstanceValid(chipSprite)) return;
				
				// Random position offset
				float offsetX = (float)GD.RandRange(-6.0, 6.0);
				float offsetY = (float)GD.RandRange(-6.0, 6.0);
				chipSprite.Position += new Vector2(offsetX, offsetY);
				
				// Random rotation
				float rotationDegrees = (float)GD.RandRange(-5.0, 5.0);
				chipSprite.Rotation = Mathf.DegToRad(rotationDegrees);
				
				// Pop-in animation
				chipSprite.Scale = Vector2.Zero;
				Tween tween = CreateTween();
				tween.TweenProperty(chipSprite, "scale", Vector2.One, 0.15f)
					.SetEase(Tween.EaseType.Out)
					.SetTrans(Tween.TransitionType.Back);
			};
		}
	}

	/// <summary>
	/// Returns appropriate height for chip stack based on filename
	/// </summary>
	private float GetChipHeight(string chipFile)
	{
		if (chipFile.Contains("_1.png")) return 48;
		if (chipFile.Contains("_2.png")) return 54;
		if (chipFile.Contains("_3.png")) return 60;
		if (chipFile.Contains("_4.png")) return 69;
		
		return 48; // default
	}

	private void UpdatePotSizeButtons()
	{
		if (!handInProgress || !isPlayerTurn || playerIsAllIn)
		{
			thirdPot.Disabled = true;
			halfPot.Disabled = true;
			standardPot.Disabled = true;
			twoThirdsPot.Disabled = true;
			allInPot.Disabled = true;
			return;
		}
		
		var (minBet, maxBet) = GetLegalBetRange();
		
		if (maxBet <= 0)
		{
			thirdPot.Disabled = true;
			halfPot.Disabled = true;
			standardPot.Disabled = true;
			twoThirdsPot.Disabled = true;
			allInPot.Disabled = true;
			return;
		}
		
		// Simple: Just calculate % of visible pot
		int thirdPotRaise = (int)Math.Round(pot * 0.33f);
		int halfPotRaise = (int)Math.Round(pot * 0.5f);
		int twoThirdsPotRaise = (int)Math.Round(pot * 0.67f);
		int fullPotRaise = pot;
		
		// Disable if raise is out of legal range
		thirdPot.Disabled = (thirdPotRaise < minBet || thirdPotRaise > maxBet);
		halfPot.Disabled = (halfPotRaise < minBet || halfPotRaise > maxBet);
		twoThirdsPot.Disabled = (twoThirdsPotRaise < minBet || twoThirdsPotRaise > maxBet);
		standardPot.Disabled = (fullPotRaise < minBet || fullPotRaise > maxBet);
		
		allInPot.Disabled = false;
	}


	/// <summary>
	/// Helper to calculate pot-sized bet amount
	/// </summary>
	/// <summary>
	/// Helper to calculate pot-sized bet amount - SIMPLE VERSION
	/// Just takes % of current visible pot
	/// </summary>
	private int CalculatePotSizeBet(float potMultiplier)
	{
		var (minBet, maxBet) = GetLegalBetRange();
		
		if (maxBet <= 0) return 0;
		
		int targetRaiseAmount = (int)Math.Round(pot * potMultiplier);
		
		return Math.Clamp(targetRaiseAmount, minBet, maxBet);
	}


	// --- HUD & BUTTONS ---
	
	private void UpdateButtonLabels()
	{
		if (waitingForNextGame) return;

		int toCall = currentBet - playerBet;
		var (minBet, maxBet) = GetLegalBetRange();

		bool allInOnly = (minBet == maxBet && maxBet == playerChips);
		bool sliderAllIn = (maxBet == playerChips && betAmount == maxBet);

		if (toCall == 0)
		{
			checkCallButton.Text = "Check";

			if (allInOnly || sliderAllIn)
			{
				betRaiseButton.Text = $"ALL IN: {maxBet}";
			}
			else
			{
				if (currentBet > 0)
				{
					betRaiseButton.Text = $"Raise: {betAmount}";
				}
				else
				{
					betRaiseButton.Text = $"Bet: {betAmount}";
				}
			}
		}
		else
		{
			if (toCall < 0)
			{
				checkCallButton.Text = $"Call (Take back {Math.Abs(toCall)})";
			}
			else
			{
				checkCallButton.Text = $"Call: {Math.Min(toCall, playerChips)}";
			}

			if (allInOnly || sliderAllIn)
			{
				betRaiseButton.Text = $"ALL IN ({maxBet})";
			}
			else
			{
				betRaiseButton.Text = $"Raise: {betAmount}";
			}
		}
	}

	private void UpdateHud()
	{
		if (isMatchComplete)
		{
			checkCallButton.Text = "Continue";
			checkCallButton.Disabled = false;
			foldButton.Visible = false;
			betRaiseButton.Visible = false;
			sliderUI.Visible = false;
			potArea.Visible = false;
			UpdatePotDisplay(0);
			return;
		}

		if (waitingForNextGame)
		{
			if (IsGameOver())
			{
				checkCallButton.Disabled = true;
			}
			else
			{
				checkCallButton.Text = "Next Hand";
				checkCallButton.Disabled = false;
			}

			cashOutButton.Disabled = false;
			cashOutButton.Visible = true;
			foldButton.Visible = false;
			betRaiseButton.Visible = false;
			sliderUI.Visible = false;
			potArea.Visible = false;
			UpdatePotDisplay(0);
		}
		else
		{
			RefreshBetSlider();
			UpdateButtonLabels();

			foldButton.Visible = true;
			betRaiseButton.Visible = true;

			bool enableButtons = isPlayerTurn && handInProgress && !playerIsAllIn;
			foldButton.Disabled = !enableButtons;
			checkCallButton.Disabled = !enableButtons;

			if (!enableButtons || raisesThisStreet >= MAX_RAISES_PER_STREET)
			{
				betRaiseButton.Disabled = true;
			}
			else
			{
				betRaiseButton.Disabled = false;
			}
		}

		playerStackLabel.Text = $"Money In-Hand: ${playerChips}";
		opponentStackLabel.Text = $"{currentOpponentName}: {opponentChips}";
		potLabel.Text = $"Pot: {pot}";
		UpdatePotDisplay(pot);
		UpdatePotSizeButtons();
	}

	private void RefreshBetSlider()
	{
		if (betSlider == null)
			return;

		var (minBet, maxBet) = GetLegalBetRange();

		if (maxBet <= 0)
		{
			betSlider.MinValue = 0;
			betSlider.MaxValue = 0;
			betSlider.Value = 0;
			betSlider.Editable = false;
			return;
		}

		betSlider.Editable = true;
		betSlider.MinValue = minBet;
		betSlider.MaxValue = maxBet;

		betAmount = Math.Clamp(betAmount, minBet, maxBet);
		betSlider.Value = betAmount;
		UpdatePotSizeButtons();
	}

	private void OnBetSliderValueChanged(double value)
	{
		int sliderValue = (int)Math.Round(value);
		var (minBet, maxBet) = GetLegalBetRange();
		sliderValue = Math.Clamp(sliderValue, minBet, maxBet);

		betAmount = sliderValue;
		betSlider.Value = betAmount;
		UpdateButtonLabels();
		UpdatePotSizeButtons();
	}

	// --- DIALOGUE SYSTEM ---
	
	private float AnimateText(Label label, string text, float speed = 0.03f)
	{
		if (string.IsNullOrWhiteSpace(text) || text.Length <= 1)
			return 0f;

		speechBubble.Visible = true;
		label.Text = text;
		label.VisibleRatio = 0;

		float typeDuration = text.Length * speed;

		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(label, "visible_ratio", 1.0f, typeDuration);

		float readTime = 2.0f + (text.Length * 0.05f);
		tween.TweenInterval(readTime);

		tween.TweenCallback(Callable.From(() =>
		{
			label.Text = "";
			speechBubble.Visible = false;
		}));

		return typeDuration;
	}
	
	private float PlayDialogue(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			speechBubble.Close(); 
			return 0f;
		}

		speechBubble.Say(text, faceSprite);

		float typingDuration = text.Length * 0.05f; 
		float totalDuration = 0.2f + typingDuration + 2.0f;

		GetTree().CreateTimer(totalDuration).Timeout += () => speechBubble.Close();

		return typingDuration; 
	}

	private float PlayActionDialogue(PlayerAction action, GameState state)
	{
		HandStrength strength = aiOpponent.DetermineHandStrengthCategory(state);
		string dialogueLine = aiOpponent.GetDialogueForAction(action, strength, aiBluffedThisHand);

		float chatRoll = GD.Randf();
		bool alwaysTalk = (aiOpponent.CurrentTiltState >= TiltState.Steaming);

		if ((chatRoll <= aiOpponent.Personality.Chattiness || alwaysTalk))
		{
			return PlayDialogue(dialogueLine);
		}

		return PlayDialogue(null);
	}

	private void PlayReactionDialogue(string category)
	{
		string line = aiOpponent.GetRandomDialogue(category);

		float chatRoll = GD.Randf();
		bool alwaysTalk = (aiOpponent.CurrentTiltState >= TiltState.Steaming);

		float threshold = aiOpponent.Personality.Chattiness;
		if (category == "OnWinPot" || category == "OnLosePot") threshold += 0.4f;

		if (chatRoll <= threshold || alwaysTalk)
		{
			PlayDialogue(line);
		}
		else
		{
			PlayDialogue(null);
		}
	}
}
