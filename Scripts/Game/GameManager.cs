using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }
	
	// This holds the data when switching scenes
	public OpponentProfile SelectedOpponent { get; set; }

	public override void _Ready()
	{
		Instance = this;
	}
}
