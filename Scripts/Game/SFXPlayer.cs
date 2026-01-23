using Godot;
using System.Collections.Generic;

public partial class SFXPlayer : AudioStreamPlayer
{
	private readonly string[] _chipSounds = { "chips_1", "chips_2" };
	private readonly string[] _allInSounds = { "all_in_1", "all_in_2" };
	private readonly string[] _deckSounds = { "deck_deal_1", "deck_deal_2", "deck_deal_3", "deck_deal_4" };

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

	public void PlayRandomChip(bool isOpponent = false)
	{
		PlayRandomFromList(_chipSounds, isOpponent);
	}

	public void PlayRandomAllIn(bool isOpponent = false)
	{
		PlayRandomFromList(_allInSounds, isOpponent);
	}

	public void PlayRandomDeckSound(bool isOpponent = false)
	{
		PlayRandomFromList(_deckSounds, isOpponent);
	}

	private void PlayRandomFromList(string[] soundList, bool isOpponent)
	{
		if (soundList.Length == 0) return;

		int index = (int)(GD.Randi() % soundList.Length);
		string selectedSound = soundList[index];
		
		PlaySound(selectedSound, isOpponent);
	}

	public void PlaySound(string soundName, bool isOpponent = false)
	{
		if (_sounds.TryGetValue(soundName, out AudioStream stream))
		{
			this.Stream = stream;
			
			// 2. Adjust Pitch based on who triggered it
			float basePitch = 1.0f;
			
			if (isOpponent)
			{
				basePitch = 0.925f; 
			}
			else
			{
				// Normal pitch for player (Slightly higher/brighter)
				basePitch = 1.0f;
			}
			this.PitchScale = basePitch;
			
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
