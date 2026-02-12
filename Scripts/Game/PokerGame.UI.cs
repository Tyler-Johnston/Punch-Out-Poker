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
	private Tween nextHandButtonTween;
	private Tween foldIdleTween;
	private Tween checkCallIdleTween;
	private Tween betRaiseIdleTween;
	private Tween nextHandIdleTween;
	private Tween cashOutIdleTween;
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
		SetupButtonPivot(nextHandButton);

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

		nextHandButton.MouseEntered += () => OnButtonHover(nextHandButton, ref nextHandButtonTween);
		nextHandButton.MouseExited += () => OnButtonUnhover(nextHandButton, ref nextHandButtonTween);
		nextHandButton.ButtonDown += () => OnButtonPress(nextHandButton, ref nextHandButtonTween);
		nextHandButton.ButtonUp += () => OnButtonRelease(nextHandButton, ref nextHandButtonTween);

		StartIdleAnimation(foldButton, ref foldIdleTween, 0f);
		StartIdleAnimation(checkCallButton, ref checkCallIdleTween, 0.33f);
		StartIdleAnimation(betRaiseButton, ref betRaiseIdleTween, 0.66f);
		StartIdleAnimation(nextHandButton, ref nextHandIdleTween, 0.5f);
		StartIdleAnimation(cashOutButton, ref cashOutIdleTween, 0.5f);
	}

	/// <summary>
	/// Sets button pivot to center for smooth scaling and prevents size drift
	/// </summary>
	private void SetupButtonPivot(Button button)
	{
		if (button == null) return;
		
		button.PivotOffset = button.Size / 2;
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
		if (button == null) return;
		
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

	// --- TABLE VISUALS ---
	
	private void SetTableColor()
	{
		Color baseColor = new Color("#52a67f");
		Color trimColor = new Color("#8b5a2b");
	  	switch (GameManager.Instance.GetCircuitType())
		{
			case 0: // Circuit A
				baseColor = new Color("#52a67f");
				trimColor = new Color("#b6764a"); 
				break;
			case 1: // Circuit B
				baseColor = new Color("b0333dff");
				trimColor = new Color("#336cb0");
				break;
			case 2: // Circuit C
				baseColor = new Color("#127AE3");  // Royal Blue
				//trimColor = new Color("#c0c0c0");  // Silver
				trimColor = new Color("9b59b6");  // Silver
				break;
		} 
		
		Color outlineColor = trimColor.Darkened(0.35f); 
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
		  	mat.SetShaderParameter("border_color", outlineColor); 
			
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
		UpdateDashboardColors(baseColor, trimColor, outlineColor);
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
	
	private void UpdateDashboardColors(Color baseFeltColor, Color trimColor, Color outlineColor)
	{
		int borderThick = 4;

		if (dashboardTopPanel != null)
		{
			
			var style = dashboardTopPanel.GetThemeStylebox("panel") as StyleBoxFlat;
			if (style != null)
			{
				style = (StyleBoxFlat)style.Duplicate();
				style.BgColor = trimColor;

				style.BorderWidthTop = borderThick;
				style.BorderWidthLeft = borderThick;
				style.BorderWidthRight = borderThick;
				style.BorderWidthBottom = borderThick;
				style.BorderColor = outlineColor;
				
				dashboardTopPanel.AddThemeStyleboxOverride("panel", style);
			}
		}

		if (dashboardBottomPanel != null)
		{
			var style = dashboardBottomPanel.GetThemeStylebox("panel") as StyleBoxFlat;
			if (style != null)
			{
				style = (StyleBoxFlat)style.Duplicate();
				style.BgColor = baseFeltColor.Darkened(0.50f); 
				
				style.BorderWidthTop = 0;
				style.BorderWidthLeft = borderThick;
				style.BorderWidthRight = borderThick;
				style.BorderWidthBottom = borderThick;
				style.BorderColor = outlineColor;
				
				dashboardBottomPanel.AddThemeStyleboxOverride("panel", style);
			}
		}
		
		if (betSlider != null)
		{
			// 1. Define the Fill Style (Trim Color)
			int radius = 10;
			int borderWidth = 5;
			
			var fillStyle = new StyleBoxFlat();
			fillStyle.BgColor = trimColor;
			fillStyle.CornerRadiusTopLeft = radius;
			fillStyle.CornerRadiusTopRight = radius;
			fillStyle.CornerRadiusBottomLeft = radius;
			fillStyle.CornerRadiusBottomRight = radius;
			fillStyle.BorderBlend = false;
		   	fillStyle.BorderColor = new Color(1, 1, 1, 1); // Pure White
			fillStyle.BorderWidthTop = 1;
			fillStyle.BorderWidthBottom = 1;
			fillStyle.BorderWidthLeft = 1;
			fillStyle.BorderWidthRight = 1;
			betSlider.AddThemeStyleboxOverride("grabber_area", fillStyle);

			var hoverStyle = (StyleBoxFlat)fillStyle.Duplicate();
			hoverStyle.BgColor = trimColor.Lightened(0.2f);
			betSlider.AddThemeStyleboxOverride("grabber_area_highlight", hoverStyle);
			
			var trackStyle = new StyleBoxFlat();
			trackStyle.BgColor = new Color ("00000092");
			trackStyle.CornerRadiusTopLeft = radius;
			trackStyle.CornerRadiusTopRight = radius;
			trackStyle.CornerRadiusBottomLeft = radius;
			trackStyle.CornerRadiusBottomRight = radius;

			trackStyle.BorderWidthTop = borderWidth;
			trackStyle.BorderWidthLeft = borderWidth;
			trackStyle.BorderWidthRight = borderWidth;
			trackStyle.BorderWidthBottom = borderWidth;
			trackStyle.ContentMarginTop = 12;
			trackStyle.ContentMarginBottom = 12;
			trackStyle.BorderColor = new Color("1b1b1b66");
			trackStyle.BorderBlend = true;
			betSlider.AddThemeStyleboxOverride("slider", trackStyle);
		}
		
		if (betSlider != null)
		{
			string chipPath = "res://Assets/Textures/chip_pngs/yellow_1.png";
			switch (GameManager.Instance.GetCircuitType())
			{
				case 0: // Circuit A (Green Felt) -> Gold Chip
					chipPath = "res://Assets/Textures/chip_pngs/yellow_1.png";
					break;
				case 1: // Circuit B (Red Felt) -> Blue Chip
					chipPath = "res://Assets/Textures/chip_pngs/blue_1.png";
					break;
				case 2: // Circuit C (Blue Felt) -> Black Chip
					chipPath = "res://Assets/Textures/chip_pngs/purple_1.png";
					break;
			}

			// 2. Load the Texture
			var chipTexture = GD.Load<Texture2D>(chipPath);
			if (chipTexture != null)
			{
				betSlider.AddThemeIconOverride("grabber", chipTexture);
				betSlider.AddThemeIconOverride("grabber_highlight", chipTexture);
				betSlider.AddThemeIconOverride("grabber_disabled", chipTexture);
			}
			else
			{
				GD.PrintErr($"Failed to load chip texture at: {chipPath}");
			}
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
			finalPosition.Y - 600 
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

	// --- POT & CHIP DISPLAY ---

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
	
	private void UpdatePotLabel(int currentPot)
	{
		if (potLabel == null) return;
		
		potLabel.Text = $"Pot: ${currentPot}";
		
		// Only animate if pot increased
		bool potIncreased = (currentPot > _lastPotLabel && currentPot > 0);
		
		if (potIncreased)
		{
			AnimatePotLabelPop();
		}
		
		_lastPotLabel = currentPot;
	}
	
	private void UpdatePotDisplay(int potAmount)
	{
		if (chipContainer == null) return;
		
		// Only update chip sprites if displayPot changed
		if (potAmount == _lastDisplayedPot)
			return;
		
		_lastDisplayedPot = potAmount;
		
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
			
			GetTree().CreateTimer(0.0f).Timeout += () =>
			{
				if (chipSprite == null || !IsInstanceValid(chipSprite)) return;
				
				float offsetX = (float)GD.RandRange(-6.0, 6.0);
				float offsetY = (float)GD.RandRange(-6.0, 6.0);
				chipSprite.Position += new Vector2(offsetX, offsetY);
				
				float rotationDegrees = (float)GD.RandRange(-5.0, 5.0);
				chipSprite.Rotation = Mathf.DegToRad(rotationDegrees);
				
				chipSprite.Scale = Vector2.Zero;
				Tween tween = CreateTween();
				tween.TweenProperty(chipSprite, "scale", Vector2.One, 0.15f)
					.SetEase(Tween.EaseType.Out)
					.SetTrans(Tween.TransitionType.Back);
			};
		}
	}


	/// <summary>
	/// Updates the player's chip display showing their contribution to the current betting round
	/// </summary>
	private void UpdatePlayerChipDisplay()
	{
		if (PlayerChipGridBox == null) return;
		if (playerChipsInPot == _lastDisplayedPlayerChips) return;
		_lastDisplayedPlayerChips = playerChipsInPot;
		
		foreach (Node child in PlayerChipGridBox.GetChildren())
		{
			child.QueueFree();
		}
		
		if (playerChipsInPot <= 0) return;
		
		List<string> chipImages = GetChipImagesForPot(playerChipsInPot);
		
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
				continue;
			}
			
			float width = 32;
			float height = GetChipHeight(chipFile) * 0.67f;
			
			chipSprite.CustomMinimumSize = new Vector2(width, height);
			chipSprite.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
			chipSprite.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			
			PlayerChipGridBox.AddChild(chipSprite);
			
			GetTree().CreateTimer(0.0f).Timeout += () =>
			{
				if (chipSprite == null || !IsInstanceValid(chipSprite)) return;
				
				float offsetX = (float)GD.RandRange(-3.0, 3.0);
				float offsetY = (float)GD.RandRange(-3.0, 3.0);
				chipSprite.Position += new Vector2(offsetX, offsetY);
				
				float rotationDegrees = (float)GD.RandRange(-5.0, 5.0);
				chipSprite.Rotation = Mathf.DegToRad(rotationDegrees);
				
				chipSprite.Scale = Vector2.Zero;
				Tween tween = CreateTween();
				tween.TweenProperty(chipSprite, "scale", Vector2.One, 0.15f)
					.SetEase(Tween.EaseType.Out)
					.SetTrans(Tween.TransitionType.Back);
			};
		}
	}

	/// <summary>
	/// Updates the opponent's chip display showing their contribution to the current betting round
	/// </summary>
	private void UpdateOpponentChipDisplay()
	{
		if (OpponentChipGridBox == null) return;
		if (opponentChipsInPot == _lastDisplayedOpponentChips) return;
		_lastDisplayedOpponentChips = opponentChipsInPot;
		
		foreach (Node child in OpponentChipGridBox.GetChildren())
		{
			child.QueueFree();
		}
		
		if (opponentChipsInPot <= 0) return;
		
		List<string> chipImages = GetChipImagesForPot(opponentChipsInPot);
		
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
				continue;
			}
			
			float width = 32;
			float height = GetChipHeight(chipFile) * 0.67f;
			
			chipSprite.CustomMinimumSize = new Vector2(width, height);
			chipSprite.ExpandMode = TextureRect.ExpandModeEnum.KeepSize;
			chipSprite.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			
			OpponentChipGridBox.AddChild(chipSprite);
			
			GetTree().CreateTimer(0.0f).Timeout += () =>
			{
				if (chipSprite == null || !IsInstanceValid(chipSprite)) return;
				
				float offsetX = (float)GD.RandRange(-3.0, 3.0);
				float offsetY = (float)GD.RandRange(-3.0, 3.0);
				chipSprite.Position += new Vector2(offsetX, offsetY);
				
				float rotationDegrees = (float)GD.RandRange(-5.0, 5.0);
				chipSprite.Rotation = Mathf.DegToRad(rotationDegrees);
				
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

	// --- HUD UPDATE METHODS ---

	private void UpdatePotSizeButtons(bool enabled = true)
	{
		// Gate: only when the slider/betting UI is actually usable
		if (!enabled || !handInProgress || !isPlayerTurn || playerIsAllIn)
		{
			thirdPot.Disabled = true;
			halfPot.Disabled = true;
			twoThirdsPot.Disabled = true;
			standardPot.Disabled = true;
			allInPot.Disabled = true;
			return;
		}

		var (minBet, maxBet) = GetLegalBetRange();

		if (maxBet <= 0)
		{
			thirdPot.Disabled = true;
			halfPot.Disabled = true;
			twoThirdsPot.Disabled = true;
			standardPot.Disabled = true;
			allInPot.Disabled = true;
			return;
		}

		int basePot = GetEffectivePot();

		// All-in: always available (even if it matches another preset)
		allInPot.Disabled = false;
		allInPot.SetMeta("raise_to", maxBet); // store for click handler [web:10]

		// If pot is 0, disable pot-% presets (but keep All-in usable)
		if (basePot <= 0)
		{
			thirdPot.Disabled = true;
			halfPot.Disabled = true;
			twoThirdsPot.Disabled = true;
			standardPot.Disabled = true;
			return;
		}

		int thirdPotRaise     = Math.Clamp((int)Math.Round(basePot * 0.33f), minBet, maxBet);
		int halfPotRaise      = Math.Clamp((int)Math.Round(basePot * 0.50f), minBet, maxBet);
		int twoThirdsPotRaise = Math.Clamp((int)Math.Round(basePot * 0.67f), minBet, maxBet);
		int fullPotRaise      = Math.Clamp(basePot, minBet, maxBet);

		var seen = new HashSet<int>();

		bool EnablePreset(Button btn, int amount)
		{
			if (amount < minBet || amount > maxBet) return false;
			if (seen.Contains(amount)) return false;

			btn.Disabled = false;
			btn.SetMeta("raise_to", amount); // store for click handler [web:10]
			seen.Add(amount);
			return true;
		}

		thirdPot.Disabled = true;
		halfPot.Disabled = true;
		twoThirdsPot.Disabled = true;
		standardPot.Disabled = true;

		EnablePreset(thirdPot, thirdPotRaise);
		EnablePreset(halfPot, halfPotRaise);
		EnablePreset(twoThirdsPot, twoThirdsPotRaise);
		EnablePreset(standardPot, fullPotRaise);
	}

	
	private void UpdateButtonLabels()
	{
		if (waitingForNextGame) return;

		if (!isPlayerTurn || !handInProgress) 
		{
			if (currentBet > playerBet)
			{
				checkCallButton.Text = "Call";
				betRaiseButton.Text = "Raise";
			}
			else
			{
				checkCallButton.Text = "Check";
				
				if (currentBet > 0) 
					betRaiseButton.Text = "Raise";
				else
					betRaiseButton.Text = "Bet";
			}
			return;
		}

		int toCall = Math.Max(0, currentBet - playerBet);
		
		var (minBet, maxBet) = GetLegalBetRange();

		bool isAllIn = (betAmount >= playerChips + playerBet);

		if (toCall == 0)
		{
			checkCallButton.Text = "Check";

			if (isAllIn)
			{
				betRaiseButton.Text = $"ALL IN: ${betAmount}";
			}
			else if (currentBet > 0)
			{
				// Raising against an existing bet
				betRaiseButton.Text = $"Raise to: ${betAmount}";
			}
			else
			{
				// Opening a new bet
				betRaiseButton.Text = $"Bet: ${betAmount}";
			}
		}
		else
		{
			// Calling an existing bet
			int callAmount = Math.Min(toCall, playerChips);
			checkCallButton.Text = $"Call: ${callAmount}";

			if (isAllIn)
			{
				betRaiseButton.Text = $"ALL IN: ${betAmount}";
			}
			else
			{
				betRaiseButton.Text = $"Raise to: ${betAmount}";
			}
		}
	}

	private void UpdateHud(bool disableButtons = false)
	{
		if (isMatchComplete)
		{
			betweenHandsUI.Visible = true;
			activePlayUI.Visible = false;
			//sliderUI.Visible = false;
			potArea.Visible = false;
			nextHandButton.Disabled = true;
			UpdateSessionProfitLabel();
			return;
		}

		if (waitingForNextGame)
		{
			//actionButtons.Visible = false;
			//sliderUI.Visible = false;
			betweenHandsUI.Visible = true;
			activePlayUI.Visible = false;
			potArea.Visible = false;
			//playerStackLabel.Visible = false;
			handTypeLabel.Text = lastHandDescription;
			handTypeLabel.Visible = true; 
			
			if (betSlider != null) betSlider.Value = 0; 
			
			UpdateSessionProfitLabel();
			UpdatePotDisplay(0);
			UpdatePlayerChipDisplay();
			UpdateOpponentChipDisplay();
		}
		else
		{
			// Active hand - show gameplay buttons and hide between hands UI
			betweenHandsUI.Visible = false;
			actionButtons.Visible = true;
			handTypeLabel.Visible = false; 
			
			bool enableButtons = true;
			bool canActuallyRaise = true;

			if (!disableButtons)
			{
				enableButtons = isPlayerTurn && handInProgress && !playerIsAllIn;
				var (minBet, maxBet) = GetLegalBetRange();
				canActuallyRaise = (maxBet > currentBet);
			}
			else
			{
				enableButtons = false;
				canActuallyRaise = false;
			}
			
			foldButton.Disabled = !enableButtons;
			checkCallButton.Disabled = !enableButtons;
			betRaiseButton.Disabled = !enableButtons || !canActuallyRaise;
			
			bool enableSlider = enableButtons && canActuallyRaise;
			
			if (betSlider != null)
			{
				betSlider.Editable = enableSlider;
				betSlider.Modulate = enableSlider ? Colors.White : new Color(0.7f, 0.7f, 0.7f, 0.5f);
			}
			
			UpdatePotSizeButtons(enableSlider); 
		}

		opponentStackLabel.Text = $"{currentOpponentName}: ${opponentChips}";
		int effectivePot = GetEffectivePot();
		
		RefreshBetSlider();
		UpdateButtonLabels();
		UpdatePlayerStackLabels();
		UpdatePotLabel(displayPot); 
		UpdatePotDisplay(displayPot); 
		UpdatePlayerChipDisplay();
		UpdateOpponentChipDisplay();
		UpdateOpponentVisuals();
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

		betSlider.MinValue = minBet;
		betSlider.MaxValue = maxBet;

		betAmount = Math.Clamp(betAmount, minBet, maxBet);
		betSlider.SetValueNoSignal(betAmount);
	}

	private void OnBetSliderValueChanged(double value)
	{
		int sliderValue = (int)Math.Round(value);
		var (minBet, maxBet) = GetLegalBetRange();
	
		sliderValue = Math.Clamp(sliderValue, minBet, maxBet);

		betAmount = sliderValue;
		betSlider.Value = betAmount;
		UpdateButtonLabels();
	}


	// --- DIALOGUE SYSTEM ---
	
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
