using Godot;
using System.Collections.Generic;

public partial class SFXPlayer : AudioStreamPlayer
{
	// 1. Define lists of sound names for random selection
	private readonly string[] _chipSounds = { "chips_1", "chips_2" };
	private readonly string[] _allInSounds = { "all_in_1", "all_in_2" };
	private readonly string[] _deckSounds = { "deck_deal_1", "deck_deal_2", "deck_deal_3", "deck_deal_4" };

	// Dictionary to hold loaded sound resources
	private Dictionary<string, AudioStream> _sounds = new Dictionary<string, AudioStream>();

	public override void _Ready()
	{
		LoadSound("chips_1", "res://Assets/SFX/Chips/chips_1.wav");
		LoadSound("chips_2", "res://Assets/SFX/Chips/chips_2.wav");
		LoadSound("all_in_1", "res://Assets/SFX/Chips/all_in_1.wav");
		LoadSound("all_in_2", "res://Assets/SFX/Chips/all_in_2.wav");
		LoadSound("deck_deal_1", "res://Assets/SFX/DeckDeal/deck_deal_1.wav");
		LoadSound("deck_deal_2", "res://Assets/SFX/DeckDeal/deck_deal_2.wav");
		LoadSound("deck_deal_3", "res://Assets/SFX/DeckDeal/deck_deal_3.wav");
		LoadSound("deck_deal_4", "res://Assets/SFX/DeckDeal/deck_deal_4.wav");
		LoadSound("card_flip", "res://Assets/SFX/card_flip.mp3");
		LoadSound("check", "res://Assets/SFX/check.mp3");
	}

	// --- RANDOM PLAY METHODS ---

	public void PlayRandomChip()
	{
		PlayRandomFromList(_chipSounds);
	}

	public void PlayRandomAllIn()
	{
		PlayRandomFromList(_allInSounds);
	}

	public void PlayRandomDeckSound()
	{
		PlayRandomFromList(_deckSounds);
	}

	// --- HELPER METHODS ---

	// Private helper to pick a random string from an array and play it
	private void PlayRandomFromList(string[] soundList)
	{
		if (soundList.Length == 0) return;

		// GD.Randi() returns a random unsigned int. 
		// We use modulo (%) to wrap it within the array's index range.
		int index = (int)(GD.Randi() % soundList.Length);
		string selectedSound = soundList[index];
		
		PlaySound(selectedSound);
	}

	public void PlaySound(string soundName)
	{
		if (_sounds.TryGetValue(soundName, out AudioStream stream))
		{
			this.Stream = stream;
			
			// Optional: Add slight pitch variance for realism
			// this.PitchScale = (float)GD.RandRange(0.95, 1.05);
			
			this.Play();
		}
		else
		{
			GD.PrintErr($"SFXPlayer: Sound '{soundName}' not found!");
		}
	}

	private void LoadSound(string name, string path)
	{
		var stream = GD.Load<AudioStream>(path);
		if (stream != null)
		{
			_sounds[name] = stream;
		}
		else
		{
			GD.PrintErr($"SFXPlayer: Could not load sound at {path}");
		}
	}
}
