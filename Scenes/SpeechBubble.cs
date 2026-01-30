using Godot;
using System.Threading.Tasks;

public partial class SpeechBubble : PanelContainer
{
	[ExportGroup("Settings")]
	[Export] public float MaxWidth = 200.0f;
	[Export] public float MinWidth = 32.0f;
	[Export] public float TypingSpeed = 0.05f;
	[Export] public float PopDuration = 0.2f;

	[ExportGroup("Tail Settings")]
	[Export] public float TailWidth = 12.0f;
	[Export] public float TailHeight = 12.0f;
	[Export] public Color TailColor = Colors.White; // Should match your Panel BG
	[Export] public Color BorderColor = Colors.Black; // Should match your Panel Border
	[Export] public float BorderWidth = 4.0f;

	private Label _label;
	private Tween _activeTween;

	public override void _Ready()
	{
		_label = GetNodeOrNull<Label>("Label");
		
		if (_label == null)
		{
			GD.PrintErr("SpeechBubble: Missing 'Label' child node!");
			return;
		}

		// Allow drawing outside bounds (for the tail)
		ClipContents = false;

		// Set Growth Direction: Expand LEFT and UP
		GrowHorizontal = GrowDirection.Begin; 
		GrowVertical = GrowDirection.Begin;   

		// Initial State: Hidden
		Scale = Vector2.Zero;
		Modulate = new Color(1, 1, 1, 0);
	}

	// --- FIX START: Handle Resize Events ---
	public override void _Notification(int what)
	{
		base._Notification(what);

		if (what == NotificationResized)
		{
			// CRITICAL: Always keep the pivot at the Bottom-Right corner.
			// This runs whenever Godot's layout engine updates the node's Size.
			PivotOffset = Size;

			// Request a redraw because the tail position depends on Size.
			QueueRedraw();
		}
	}
	// --- FIX END ---

	public override void _Draw()
	{
		// 1. Define Points relative to the Bubble Box (Bottom-Right anchor)
		Vector2 p1 = new Vector2(Size.X - TailWidth - BorderWidth, Size.Y);
		Vector2 p2 = new Vector2(Size.X - BorderWidth, Size.Y);
		Vector2 tip = new Vector2(Size.X, Size.Y + TailHeight);

		// 2. Draw Border Triangle
		Vector2[] borderPoints = new Vector2[] 
		{ 
			p1 + new Vector2(-BorderWidth, 0), 
			p2 + new Vector2(BorderWidth, 0),  
			tip + new Vector2(0, BorderWidth)  
		};
		DrawColoredPolygon(borderPoints, BorderColor);

		// 3. Draw Inner Triangle
		Vector2 innerP1 = new Vector2(p1.X, Size.Y - BorderWidth);
		Vector2 innerP2 = new Vector2(p2.X, Size.Y - BorderWidth);
		
		Vector2[] innerPoints = new Vector2[] { innerP1, innerP2, tip };
		DrawColoredPolygon(innerPoints, TailColor);
	}

	public void Say(string text)
	{
		if (_activeTween != null && _activeTween.IsRunning())
			_activeTween.Kill();
		
		_label.Text = text;
		_label.VisibleRatio = 0.0f;
		
		// Reset visibility
		Scale = Vector2.Zero;
		Modulate = new Color(1, 1, 1, 1);
		Show();

		// 1. Calculate and apply new size constraints
		UpdateBubbleSize();

		// 2. Force pivot update immediately (for the starting frame)
		// Even if size doesn't change, we ensure pivot is correct before popping.
		// If size DOES change, _Notification will fire shortly and update it again.
		PivotOffset = Size;

		// 3. Animate
		_activeTween = CreateTween();
		
		// Pop In
		_activeTween.SetParallel(true);
		_activeTween.TweenProperty(this, "scale", Vector2.One, PopDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		_activeTween.TweenProperty(this, "modulate:a", 1.0f, PopDuration);
		
		// Typewriter
		_activeTween.SetParallel(false);
		float totalTypingTime = text.Length * TypingSpeed;
		totalTypingTime = Mathf.Max(0.2f, totalTypingTime); 
		
		_activeTween.TweenProperty(_label, "visible_ratio", 1.0f, totalTypingTime)
			.SetTrans(Tween.TransitionType.Linear).SetEase(Tween.EaseType.InOut);
	}

	public void Close()
	{
		if (_activeTween != null && _activeTween.IsRunning())
			_activeTween.Kill();

		_activeTween = CreateTween();
		_activeTween.SetParallel(true);
		
		// Shrink back to Pivot (Bottom-Right)
		_activeTween.TweenProperty(this, "scale", Vector2.Zero, PopDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
		_activeTween.TweenProperty(this, "modulate:a", 0.0f, PopDuration);
		
		_activeTween.Chain().TweenCallback(Callable.From(Hide));
	}

	private void UpdateBubbleSize()
	{
		_label.AutowrapMode = TextServer.AutowrapMode.Off;
		_label.CustomMinimumSize = Vector2.Zero;
		_label.ResetSize(); 
		
		Vector2 naturalSize = _label.GetCombinedMinimumSize();

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
		
		// Reset container to fit the new Label size
		CustomMinimumSize = Vector2.Zero;
		ResetSize();
	}
}
