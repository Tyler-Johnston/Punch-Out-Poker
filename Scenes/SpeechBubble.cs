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
		if (_activeTween != null && _activeTween.IsRunning())
			_activeTween.Kill();

		Show();
		Scale = Vector2.One;
		Modulate = new Color(1, 1, 1, 0); // Visible to engine, invisible to player
		
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
		
		_label.ResetSize(); // Force label to update rect
		ResetSize();        // Force Container to shrink to Label
		
		await ToSignal(GetTree(), "process_frame");

		// --- PASS 3: Position Snap ---
		// We want the Tail Tip (Size.X, Size.Y + TailHeight) to land at TailTipPosition.
		// Math: GlobalPosition = Target - LocalOffset
		Vector2 tailOffset = new Vector2(Size.X, Size.Y + TailHeight);
		
		// If SpeechBubble is child of a Control/CanvasLayer, Position works fine.
		Position = TailTipPosition - tailOffset;

		// --- PASS 4: Animate ---
		// Re-set pivot because size just changed
		PivotOffset = Size; 
		
		// Start "Pop" animation
		Scale = Vector2.Zero;
		Modulate = new Color(1, 1, 1, 1); // Fade alpha back in
		
		_activeTween = CreateTween();
		
		_activeTween.SetParallel(true);
		_activeTween.TweenProperty(this, "scale", Vector2.One, PopDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		
		_activeTween.SetParallel(false);
		float totalTypingTime = Mathf.Max(0.5f, text.Length * TypingSpeed);
		_activeTween.TweenProperty(_label, "visible_ratio", 1.0f, totalTypingTime)
			.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);
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
