using Godot;
using System;

public partial class LevelSelect : Control
{
	// Reference to the container where cards will go
	[Export] private NodePath containerPath; 
	private Container _cardContainer;

	public override void _Ready()
	{
		// 1. Find the container (assuming HBoxContainer for horizontal layout)
		// If you didn't export it, we try to find it by name
		_cardContainer = GetNodeOrNull<Container>("HBoxContainer") ?? 
						 GetNode<Container>("CenterContainer/HBoxContainer");

		// 2. Clear dummy placeholders from the editor
		foreach (Node child in _cardContainer.GetChildren())
		{
			child.QueueFree();
		}

		// 3. Load opponents from your static file
		var circuit1 = OpponentProfiles.CircuitAOpponents();

		// 4. Create a card for each
		foreach (var opponent in circuit1)
		{
			CreateOpponentCard(opponent);
		}
		
		// Optional: Back/Quit button
		var quitBtn = GetNodeOrNull<Button>("QuitButton");
		if (quitBtn != null) quitBtn.Pressed += () => GetTree().Quit();
	}

	private void CreateOpponentCard(OpponentProfile profile)
	{
		// Root Panel
		var cardPanel = new PanelContainer();
		cardPanel.CustomMinimumSize = new Vector2(220, 320);
		
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		cardPanel.AddChild(vbox);

		// -- Header: Name --
		var nameLabel = new Label();
		nameLabel.Text = profile.Name;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		// Make name bold/large
		nameLabel.AddThemeFontSizeOverride("font_size", 24); 
		vbox.AddChild(nameLabel);

		// -- Subheader: Buy-In --
		var buyInLabel = new Label();
		buyInLabel.Text = $"Buy-In: ${profile.BuyIn}";
		buyInLabel.HorizontalAlignment = HorizontalAlignment.Center;
		buyInLabel.Modulate = Colors.GreenYellow; // Make it look like money
		vbox.AddChild(buyInLabel);

		// -- Separator --
		vbox.AddChild(new HSeparator());

		// -- Stats Block --
		var statsLabel = new Label();
		statsLabel.Text = GenerateStatsText(profile);
		statsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		vbox.AddChild(statsLabel);

		// -- Spacer to push button to bottom --
		//var spacer = new Control();
		//spacer.SizeFlagsVertical = Control.SizeFlagsExpandFill;
		//vbox.AddChild(spacer);

		// -- Select Button --
		var playButton = new Button();
		playButton.Text = "PLAY HAND";
		playButton.CustomMinimumSize = new Vector2(0, 50);
		playButton.Pressed += () => OnOpponentSelected(profile);
		vbox.AddChild(playButton);

		_cardContainer.AddChild(cardPanel);
	}

	private string GenerateStatsText(OpponentProfile p)
	{
		// Dynamically describe the opponent
		string style = "";
		if (p.Aggression > 0.7f) style = "Maniac";
		else if (p.Aggression < 0.3f) style = "Passive";
		else style = "Balanced";

		string looseness = "";
		if (p.Looseness > 0.6f) looseness = "Loose";
		else if (p.Looseness < 0.35f) looseness = "Tight";
		else looseness = "Normal";

		return $"Style: {style} / {looseness}\n\n" +
			   $"Aggression: {p.Aggression:P0}\n" +
			   $"Bluffing: {p.Bluffiness:P0}\n";
	}

	private void OnOpponentSelected(OpponentProfile profile)
	{
		// Store data in Singleton
		GameManager.Instance.SelectedOpponent = profile;
		
		// Change Scene
		GetTree().ChangeSceneToFile("res://Scenes/PokerGame.tscn");
	}
}
