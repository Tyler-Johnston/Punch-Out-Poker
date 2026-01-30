using Godot;
using System.Threading.Tasks;

[Tool] // Runs in editor so you can see the Red Target Circle
public partial class SpeechBubble : PanelContainer
{
	[ExportGroup("Settings")]
	[Export] public float MaxWidth = 250.0f; // Slightly wider for poker tables
	[Export] public float MinWidth = 32.0f;
	[Export] public float TypingSpeed = 0.04f;
	[Export] public float PopDuration = 0.3f;
	[Export] public SFXPlayer AudioPlayer;

	[ExportGroup("Tail Settings")]
	[Export] public Vector2 TailTipPosition = new Vector2(500, 300);
	[Export] public float TailWidth = 16.0f;
	[Export] public float TailHeight = 16.0f;
	[Export] public Color TailColor = Colors.White; 
	[Export] public Color BorderColor = Colors.Black;
	[Export] public float BorderWidth = 4.0f;
	[Export] public bool DebugDrawTarget = true; // Uncheck this to hide the red circle

	private Label _label;
	private Tween _activeTween;
	public float VoicePitch { get; set; } = 1.0f; 

	public override void _Ready()
	{
		_label = GetNodeOrNull<Label>("Label");
		
		if (_label == null)
		{
			// Only print error if running game, not in editor
			if (!Engine.IsEditorHint()) GD.PrintErr("SpeechBubble: Missing 'Label' child node!");
			return;
		}

		// Ensure strictly manual sizing
		ClipContents = false;
		GrowHorizontal = GrowDirection.Begin; // "Begin" = Left in Godot
		GrowVertical = GrowDirection.Begin;   // "Begin" = Top in Godot

		if (!Engine.IsEditorHint())
		{
			// Hide initially in game
			Scale = Vector2.Zero;
			Modulate = new Color(1, 1, 1, 0);
		}
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		// Update pivot/redraw when size changes (Editor or Game)
		if (what == NotificationResized)
		{
			// Set pivot to Bottom-Right corner (Box corner, not tail tip)
			// This makes the bubble "fan out" from the tail connection point
			PivotOffset = Size; 
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		// 1. Draw Debug Target (Editor Only)
		if (Engine.IsEditorHint() && DebugDrawTarget)
		{
			DrawCircle(GetParentControl()?.GetLocalMousePosition() ?? Vector2.Zero, 5, Colors.Red);
			DrawCircle(new Vector2(Size.X, Size.Y + TailHeight), 3, Colors.Red);
		}

		// 2. Draw The Tail
		Vector2 p1 = new Vector2(Size.X - TailWidth - BorderWidth, Size.Y);
		Vector2 p2 = new Vector2(Size.X - BorderWidth, Size.Y);
		Vector2 tip = new Vector2(Size.X, Size.Y + TailHeight);

		// Outer Border (Black)
		Vector2[] borderPoints = new Vector2[] 
		{ 
			p1 + new Vector2(-BorderWidth, 0), 
			p2 + new Vector2(BorderWidth, 0),  
			tip + new Vector2(0, BorderWidth)  
		};
		DrawColoredPolygon(borderPoints, BorderColor);

		// Inner Color (White) - Overlaps the box border to look seamless
		Vector2 innerP1 = new Vector2(p1.X, Size.Y - BorderWidth);
		Vector2 innerP2 = new Vector2(p2.X, Size.Y - BorderWidth);
		
		Vector2[] innerPoints = new Vector2[] { innerP1, innerP2, tip };
		DrawColoredPolygon(innerPoints, TailColor);
	}

	public async void Say(string text)
	{
			GD.Print($"[DEBUG] Say called with text: '{text}'. AudioPlayer is: {(AudioPlayer == null ? "NULL" : "ASSIGNED")}");
			
		if (_activeTween != null && _activeTween.IsRunning())
			_activeTween.Kill();

		Show();
		Scale = Vector2.One;
		Modulate = new Color(1, 1, 1, 0); // Invisible for calculation
		
		_label.Text = text;
		_label.VisibleRatio = 0.0f;
		
		// --- PASS 1: Natural Width Calculation ---
		_label.AutowrapMode = TextServer.AutowrapMode.Off;
		_label.CustomMinimumSize = Vector2.Zero;
		_label.ResetSize(); 
		
		await ToSignal(GetTree(), "process_frame");
		if (!IsInstanceValid(this) || !IsInstanceValid(_label)) return;

		Vector2 naturalSize = _label.GetCombinedMinimumSize();

		// --- PASS 2: Apply Constraints ---
		if (naturalSize.X > MaxWidth)
		{
			_label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			_label.CustomMinimumSize = new Vector2(MaxWidth, 0);
		}
		else
		{
			_label.AutowrapMode = TextServer.AutowrapMode.Off;
			float tightWidth = Mathf.Max(MinWidth, naturalSize.X);
			_label.CustomMinimumSize = new Vector2(tightWidth, 0);
		}
		
		_label.ResetSize();
		ResetSize();
		
		await ToSignal(GetTree(), "process_frame");

		// --- PASS 3: Position Snap ---
		Vector2 tailOffset = new Vector2(Size.X, Size.Y + TailHeight);
		Position = TailTipPosition - tailOffset;

		// --- PASS 4: Pop Animation ---
		PivotOffset = Size; 
		Scale = Vector2.Zero;
		Modulate = new Color(1, 1, 1, 1);
		
		_activeTween = CreateTween();
		_activeTween.SetParallel(true);
		_activeTween.TweenProperty(this, "scale", Vector2.One, PopDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		
		// Wait for pop to finish before typing (optional, feels cleaner)
		// or just let it type while popping
		
		// --- PASS 5: Typing Loop with Sound ---
		// We do NOT use TweenProperty for visible_ratio anymore. We do it manually.
		
		int totalChars = text.Length;
		float delayPerChar = TypingSpeed;

		for (int i = 0; i <= totalChars; i++)
		{
			_label.VisibleCharacters = i;
			
			// Play sound every 2 characters (so it's not too crazy)
			// AND ensure we aren't at the very start (i=0)
			if (i > 0 && i < totalChars && i % 2 == 0)
			{
				if (AudioPlayer != null)
				{
					float minPitch = VoicePitch - 0.1f;
					float maxPitch = VoicePitch + 0.1f;
					
					AudioPlayer.PlaySpeechBlip(minPitch, maxPitch);
				}
			}

			// Wait for next char
			// Using CreateTimer ensures it runs even if the game is paused (if configured)
			// or use ToSignal(GetTree().CreateTimer(delayPerChar), "timeout");
			await ToSignal(GetTree().CreateTimer(delayPerChar), "timeout");
			
			// Safety check if the bubble was closed mid-typing
			if (!IsInstanceValid(this) || !Visible) return;
		}
	}


	public void Close()
	{
		if (_activeTween != null && _activeTween.IsRunning())
			_activeTween.Kill();

		_activeTween = CreateTween();
		_activeTween.SetParallel(true);
		
		_activeTween.TweenProperty(this, "scale", Vector2.Zero, PopDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		_activeTween.TweenProperty(this, "modulate:a", 0.0f, PopDuration);
		
		_activeTween.Chain().TweenCallback(Callable.From(Hide));
	}
}
