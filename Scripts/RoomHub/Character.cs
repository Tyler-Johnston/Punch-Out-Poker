using Godot;
using System;

public partial class Character : CharacterBody2D
{
	[Export] public float Speed = 200f;
	[Export] public float InteractRange = 50f;

	private Vector2 _velocity = Vector2.Zero;
	private Area2D _interactArea;

	public override void _Ready()
	{
		// Create an interaction detection area around the player
		_interactArea = GetNodeOrNull<Area2D>("InteractArea");
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleMovement();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("interact"))
		{
			TryInteract();
		}
	}

	private void HandleMovement()
	{
		Vector2 direction = Vector2.Zero;

		if (Input.IsActionPressed("ui_right")) direction.X += 1;
		if (Input.IsActionPressed("ui_left"))  direction.X -= 1;
		if (Input.IsActionPressed("ui_down"))  direction.Y += 1;
		if (Input.IsActionPressed("ui_up"))    direction.Y -= 1;

		if (direction != Vector2.Zero)
			direction = direction.Normalized();

		Velocity = direction * Speed;
		MoveAndSlide();
	}

	private void TryInteract()
	{
		if (_interactArea == null)
		{
			GD.PrintErr("No InteractArea found on Character.");
			return;
		}

		foreach (Node2D body in _interactArea.GetOverlappingBodies())
		{
			if (body.HasMethod("Interact"))
			{
				body.Call("Interact");
				break; // Only interact with the closest/first thing
			}
		}
	}
}
