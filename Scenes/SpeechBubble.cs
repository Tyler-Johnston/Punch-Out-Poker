using Godot;
using System.Threading.Tasks;

[Tool]
public partial class SpeechBubble : PanelContainer
{
	[ExportGroup("Settings")]
	[Export] public float MaxWidth = 250.0f;
	[Export] public float MinWidth = 32.0f;
	[Export] public float TypingSpeed = 0.04f;
	[Export] public float PopDuration = 0.3f;
	[Export] public SFXPlayer AudioPlayer;

	[ExportGroup("Tail Settings")]
	[Export] public float TailWidth = 16.0f;
	[Export] public float TailHeight = 16.0f;
	[Export] public Color TailColor = Colors.White; 
	[Export] public Color BorderColor = Colors.Black;
	[Export] public float BorderWidth = 4.0f;
	[Export] public bool DebugDrawTarget = true;
	[Export] public Vector2 TailTargetOffset = new Vector2(0, -20);

	private Label _label;
	private Tween _activeTween;
	private Node2D _currentTarget;
	public float VoicePitch { get; set; } = 1.0f; 

	public override void _Ready()
	{
		_label = GetNodeOrNull<Label>("Label");
		
		if (_label == null)
		{
			if (!Engine.IsEditorHint()) GD.PrintErr("SpeechBubble: Missing 'Label' child node!");
			return;
		}

		ClipContents = false;
		GrowHorizontal = GrowDirection.End;   // Grows RIGHT
		GrowVertical = GrowDirection.Begin;   // Grows UP

		if (!Engine.IsEditorHint())
		{
			Scale = Vector2.Zero;
			Modulate = new Color(1, 1, 1, 0);
		}
	}

	public override void _Notification(int what)
	{
		base._Notification(what);
		if (what == NotificationResized)
		{
			PivotOffset = new Vector2(0, Size.Y); // Bottom-LEFT corner
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		if (Engine.IsEditorHint() && DebugDrawTarget && _currentTarget != null)
		{
			Vector2 targetPos = GetTargetWorldPosition();
			DrawCircle(targetPos, 3, Colors.Green);
		}

		// Tail points LEFT from bottom-left corner
		Vector2 p1 = new Vector2(BorderWidth, Size.Y);
		Vector2 p2 = new Vector2(TailWidth + BorderWidth, Size.Y);
		Vector2 tip = new Vector2(0, Size.Y + TailHeight);

		// Outer Border (Black)
		Vector2[] borderPoints = new Vector2[] 
		{ 
			p1 + new Vector2(-BorderWidth, 0), 
			p2 + new Vector2(BorderWidth, 0),  
			tip + new Vector2(0, BorderWidth)  
		};
		DrawColoredPolygon(borderPoints, BorderColor);

		// Inner Color (White)
		Vector2 innerP1 = new Vector2(p1.X, Size.Y - BorderWidth);
		Vector2 innerP2 = new Vector2(p2.X, Size.Y - BorderWidth);
		
		Vector2[] innerPoints = new Vector2[] { innerP1, innerP2, tip };
		DrawColoredPolygon(innerPoints, TailColor);
	}

	private Vector2 GetTargetWorldPosition()
	{
		if (_currentTarget != null && IsInstanceValid(_currentTarget))
		{
			if (_currentTarget is Sprite2D sprite)
			{
				return _currentTarget.GlobalPosition + TailTargetOffset;
			}
			return _currentTarget.GlobalPosition;
		}
		
		return GlobalPosition;
	}

	public async void Say(string text, Node2D targetNode)
	{
		_currentTarget = targetNode;
		
		GD.Print($"[DEBUG] Say called with text: '{text}'. AudioPlayer is: {(AudioPlayer == null ? "NULL" : "ASSIGNED")}");
		
		if (_activeTween != null && _activeTween.IsRunning())
			_activeTween.Kill();

		Show();
		Scale = Vector2.One;
		Modulate = new Color(1, 1, 1, 0);
		
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
		Vector2 targetWorldPos = GetTargetWorldPosition();
		Vector2 tailOffset = new Vector2(0, Size.Y + TailHeight); // Tail at bottom-left
		Position = targetWorldPos - tailOffset;

		// --- PASS 4: Pop Animation ---
		PivotOffset = new Vector2(0, Size.Y); // Pivot at bottom-left
		Scale = Vector2.Zero;
		Modulate = new Color(1, 1, 1, 1);
		
		_activeTween = CreateTween();
		_activeTween.SetParallel(true);
		_activeTween.TweenProperty(this, "scale", Vector2.One, PopDuration)
			.SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		
		// --- PASS 5: Typing Loop ---
		int totalChars = text.Length;
		float delayPerChar = TypingSpeed;

		for (int i = 0; i <= totalChars; i++)
		{
			_label.VisibleCharacters = i;
			
			if (i > 0 && i < totalChars && i % 2 == 0)
			{
				if (AudioPlayer != null)
				{
					float minPitch = VoicePitch - 0.1f;
					float maxPitch = VoicePitch + 0.1f;
					
					AudioPlayer.PlaySpeechBlip(minPitch, maxPitch);
				}
			}

			await ToSignal(GetTree().CreateTimer(delayPerChar), "timeout");
			
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
