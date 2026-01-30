// PokerGame.UI.cs
using Godot;
using System;

public partial class PokerGame
{
	
	private void ShowMessage(string text)
	{
		gameStateLabel.Text = text;
	}
	
	private float AnimateText(Label label, string text, float speed = 0.03f)
	{
		if (string.IsNullOrWhiteSpace(text) || text.Length <= 1) 
			return 0f; // No wait time

		speechBubble.Visible = true; 
		label.Text = text;
		label.VisibleRatio = 0;
		
		float typeDuration = text.Length * speed;
		
		Tween tween = GetTree().CreateTween();
		tween.TweenProperty(label, "visible_ratio", 1.0f, typeDuration);
		
		float readTime = 2.0f + (text.Length * 0.05f);
		tween.TweenInterval(readTime);
		
		tween.TweenCallback(Callable.From(() => {
			label.Text = "";           
			speechBubble.Visible = false;
		}));

		return typeDuration; // Return how long typing takes
	}

	private void SetTableColor()
	{
		// get table color depending on the circuit we are in
		Color middleColor = new Color("#52a67f"); 
		switch (GameManager.Instance.GetCircuitType())
		{
			case 0:
				middleColor = new Color("#52a67f"); 
				break;
			case 1:
				middleColor = new Color("b0333dff"); 
				break;
			case 2:
				middleColor = new Color("#127AE3"); 
				break;
		}
		
		// set the table color
		var gradientTexture = tableColor.Texture as GradientTexture2D;
		if (gradientTexture != null)
		{
			gradientTexture.Gradient = (Gradient)gradientTexture.Gradient.Duplicate();
			Color cornerColor = middleColor.Darkened(0.3f);

			gradientTexture.Gradient.SetColor(0, middleColor);
			gradientTexture.Gradient.SetColor(1, cornerColor);
		}
		ShaderMaterial retroMat = new ShaderMaterial();
		retroMat.Shader = GD.Load<Shader>("res://Assets/Shaders/Pixelate.gdshader");
		tableColor.Material = retroMat;
	}
	
	private void UpdateOpponentExpression(PlayerAction action)
	{
		// 1. Heavy Tilt Overrides (Angry/Monkey)
		if (aiOpponent.CurrentTiltState == TiltState.Steaming || aiOpponent.CurrentTiltState == TiltState.Monkey)
		{
			SetExpression(Expression.Angry);
			return;
		}

		// 2. Action-based Expressions
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
					// Calculate total like the raise block
					int raiseTotal = currentBet + betAmount;
					betRaiseButton.Text = $"Raise: {raiseTotal}";
				}
				else
				{
					// True opening bet (0 in pot)
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

			int raiseTotal = currentBet + betAmount;
			int toAddForRaise = raiseTotal - playerBet;

			if (allInOnly || sliderAllIn)
			{
				betRaiseButton.Text = $"ALL IN ({maxBet})";
			}
			else
			{
				betRaiseButton.Text = $"Raise: {raiseTotal}";
			}
		}

		if (raisesThisStreet >= MAX_RAISES_PER_STREET && !waitingForNextGame)
		{
			betRaiseButton.Disabled = true;
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
			betSlider.Visible = false;
			betSliderLabel.Visible = false;
			potLabel.Visible = false;
			opponentDialogueLabel.Text = "";
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
			betSlider.Visible = false;
			betSliderLabel.Visible = false;
			potLabel.Visible = false;
			opponentDialogueLabel.Text = "";
		}
		else
		{
			RefreshBetSlider();
			UpdateButtonLabels();
			
			foldButton.Visible = true;
			betRaiseButton.Visible = true;

			// Disable buttons during AI turn to prevent race conditions
			bool enableButtons = isPlayerTurn && handInProgress && !playerIsAllIn;
			foldButton.Disabled = !enableButtons;
			checkCallButton.Disabled = !enableButtons;

			// Special handling for raise button
			if (!enableButtons || raisesThisStreet >= MAX_RAISES_PER_STREET)
			{
				betRaiseButton.Disabled = true;
			}
			else
			{
				betRaiseButton.Disabled = false;
			}
		}

		playerStackLabel.Text = $"You: {playerChips}";
		opponentStackLabel.Text = $"Opp: {opponentChips}";
		potLabel.Text = $"Pot: {pot}";
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
	
	public void SetExpression(Expression expression)
	{
		if (faceSprite != null)
		{
			// Cast the enum to int to set the frame index
			faceSprite.Frame = (int)expression;
		}
	}
	
	private void LoadOpponentSprite(string currentOpponentName)
	{
		string folderPath = "res://Assets/Textures/expressions/"; 
		string targetPath = $"{folderPath}{currentOpponentName}_expressions.png";
		string fallbackPath = $"{folderPath}king_expressions.png";
		
		Texture2D loadedTexture = null;

		if (ResourceLoader.Exists(targetPath))
		{
			loadedTexture = GD.Load<Texture2D>(targetPath);
			GD.Print($"Loaded sprite: {currentOpponentName}");
		}
		else
		{
			GD.Print($"Sprite missing for {currentOpponentName}. Defaulting to King.");
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
		TiltState state = aiOpponent.CurrentTiltState;
		
		var currentStyle = OpponentFrame.GetThemeStylebox("panel");
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
	
	/// <summary>
	/// Core helper to manage Bubble visibility and Animation.
	/// Returns the duration of the typing animation.
	/// </summary>
	private float PlayDialogue(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			opponentDialogueLabel.Text = "";
			speechBubble.Visible = false;
			return 0f;
		}
		
		return AnimateText(opponentDialogueLabel, text);
	}

	/// <summary>
	/// Gets dialogue specific to the AI's Action (Call/Raise/Fold).
	/// Returns the duration to wait.
	/// </summary>
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

	/// <summary>
	/// Gets dialogue for reaction events (Win/Loss). 
	/// Fire-and-forget (void).
	/// </summary>
	private void PlayReactionDialogue(string category)
	{
		string line = aiOpponent.GetRandomDialogue(category);
		
		float chatRoll = GD.Randf();
		bool alwaysTalk = (aiOpponent.CurrentTiltState >= TiltState.Steaming);
		
		// Reaction thresholds can be slightly higher
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
