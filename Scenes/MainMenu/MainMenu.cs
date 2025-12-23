using Godot;
using System;

public partial class MainMenu : Control
{
	public override void _Ready()
	{
		var startButton = GetNode<Button>("ButtonContainer/StartButton");
		var quitButton = GetNode<Button>("ButtonContainer/QuitButton");

		startButton.Pressed += OnStartPressed;
		quitButton.Pressed += OnQuitPressed;
	}

	private void OnStartPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/PokerGame.tscn");
	}

	private void OnQuitPressed()
	{
		GetTree().Quit();
	}
}
