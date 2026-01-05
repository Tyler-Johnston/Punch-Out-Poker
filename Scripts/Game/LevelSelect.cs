using Godot;
using System;

public partial class LevelSelect : Control
{
	// Make sure your buttons are in order in the inspector!
	[Export]
	public Godot.Collections.Array<Button> CircuitButtons { get; set; }

	// Path to your Poker Game scene
	private string _gameScenePath = "res://Scenes/PokerGame.tscn";

	public override void _Ready()
	{
		// 1. Get the list of opponents
		var opponents = OpponentProfiles.CircuitAOpponents();

		// 2. Loop through buttons by index 'i'
		for (int i = 0; i < CircuitButtons.Count; i++)
		{
			Button btn = CircuitButtons[i];

			// 3. Check if we have an opponent for this button index
			if (i < opponents.Length)
			{
				// Capture the specific opponent for this loop iteration
				var opponent = opponents[i];

				btn.Text = $"{opponent.Name}\n${opponent.BuyIn}";

				btn.Pressed += () => OnOpponentSelected(opponent);
				
				GD.Print($"Wired up {btn.Name} to {opponent.Name}");
			}
			else
			{
				// If there are more buttons than opponents, disable the extras
				btn.Disabled = true;
				btn.Text = "Locked";
			}
		}
	}

	private void OnOpponentSelected(OpponentProfile opponent)
	{
		GD.Print($"You clicked {opponent.Name}. Attempting to start game...");

		// 1. Check if we can afford it (using the singleton we just made)
		if (GameManager.Instance.PlayerMoney < opponent.BuyIn)
		{
			GD.Print($"Not enough money! You have ${GameManager.Instance.PlayerMoney}, need ${opponent.BuyIn}");
			return; 
		}

		// 2. Set the global selected opponent
		GameManager.Instance.SelectedOpponent = opponent;

		// 3. Switch Scene
		GetTree().ChangeSceneToFile(_gameScenePath);
	}
}
