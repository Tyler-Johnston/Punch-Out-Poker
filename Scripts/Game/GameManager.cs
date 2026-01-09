using Godot;
using System;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	// --- GAME DATA ---
	public int PlayerMoney { get; set; } = 1000; // Starting money

	// The opponent selected for the NEXT match
	public OpponentProfile SelectedOpponent { get; set; }

	public override void _Ready()
	{
		// Set the static instance to this node
		Instance = this;
	}
}
