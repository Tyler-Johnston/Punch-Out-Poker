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
		// Existing Game Sounds
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
		LoadSound("speech_blip", "res://Assets/SFX/speech_blip.wav");
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
	
	/// <summary>
	/// Plays a short blip for text dialogue.
	/// </summary>
	/// <param name="minPitch">Lowest pitch variance (e.g. 0.8)</param>
	/// <param name="maxPitch">Highest pitch variance (e.g. 1.2)</param>
	public void PlaySpeechBlip(float minPitch = 0.9f, float maxPitch = 1.1f)
	{
		if (_sounds.TryGetValue("speech_blip", out AudioStream stream))
		{
			this.Stream = stream;
			
		  	this.VolumeDb = -10.0f; 
			this.PitchScale = (float)GD.RandRange(minPitch, maxPitch);
			
			this.Play();
		}
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
			
			// Adjust Pitch based on who triggered it
			float basePitch = 1.0f;
			
			if (isOpponent)
			{
				basePitch = 0.925f; // Lower/Darker for opponent
			}
			else
			{
				basePitch = 1.0f; // Normal/Brighter for player
			}
			this.PitchScale = basePitch;
			
			this.Play();
		}
		else
		{
			// Only log error if it's not the speech blip (which might be missing initially)
			if(soundName != "speech_blip") 
				GD.PrintErr($"SFXPlayer: Sound '{soundName}' not found!");
		}
	}

	private void LoadSound(string name, string path)
	{
		// Use FileAccess to check existence first to avoid console spam if file is missing
		if (!FileAccess.FileExists(path))
		{
			GD.Print($"SFXPlayer: File not found at {path} - Sound '{name}' will be silent.");
			return;
		}

		var stream = GD.Load<AudioStream>(path);
		if (stream != null)
		{
			_sounds[name] = stream;
		}
		else
		{
			GD.PrintErr($"SFXPlayer: Failed to load resource at {path}");
		}
	}
}
