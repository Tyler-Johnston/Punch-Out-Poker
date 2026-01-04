using Godot;
using System;

public partial class GameManager : Node
{
	// Static instance so you can access it from anywhere as 'GameManager.Instance'
	public static GameManager Instance { get; private set; }

	// --- GAME DATA ---
	// The player's total money (persistent)
	public int PlayerMoney { get; set; } = 1000; // Starting money

	// The opponent selected for the NEXT match
	public OpponentProfile SelectedOpponent { get; set; }

	public override void _Ready()
	{
		// Set the static instance to this node
		Instance = this;
	}
}
