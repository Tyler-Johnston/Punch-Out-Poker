using Godot;
using System.Collections.Generic;

public partial class MusicPlayer : AudioStreamPlayer
{
	// Singleton instance for easy access
	public static MusicPlayer Instance { get; private set; }

	private Dictionary<string, AudioStream> _tracks = new Dictionary<string, AudioStream>();
	private Tween _fadeTween;

	public override void _Ready()
	{
		Instance = this;
		
		// Ensure this player is on the "Music" bus
		this.Bus = "Music"; 

		LoadTrack("steve_bg_music", "res://Assets/Music/Mind Games/Puzzled Mind [87 BPM]/Puzzled Mind.wav");
		LoadTrack("aryll_bg_music", "res://Assets/Music/Mind Games/Sneaky Thoughts [87 BPM]/Sneaky Thoughts.wav");
		LoadTrack("boy wizard_bg_music", "res://Assets/Music/Mind Games/Brainstorm [180 BPM]/Brainstorm.wav");
		LoadTrack("apprentice_bg_music", "res://Assets/Music/Mind Games/Clear Headed [180 BPM]/Clear Headed.wav");
		LoadTrack("malandro_bg_music", "res://Assets/Music/Mind Games/Funky Feeling [120 BPM]/Funky Feeling.wav");
		LoadTrack("cowboy_bg_music", "res://Assets/Music/Mind Games/Hazy Mood [87 BPM]/Hazy Mood.wav");
		LoadTrack("king_bg_music", "res://Assets/Music/Mind Games/Wandering Rumination [93 BPM]/Wandering Rumination.wav");
		LoadTrack("old wizard_bg_music", "res://Assets/Music/Mind Games/Meditation [87 BPM]/Meditation.wav");
		LoadTrack("akalite_bg_music", "res://Assets/Music/Mind Games/Dream [90 BPM]/Dream.wav");
	}

	public void PlayTrack(string trackName, float fadeTime = 1.0f)
	{
		if (!_tracks.TryGetValue(trackName, out AudioStream newStream))
		{
			GD.PrintErr($"MusicPlayer: Track '{trackName}' not found!");
			return;
		}

		if (this.Stream == newStream && this.Playing) return;

		// IF playing, crossfade
		if (this.Playing && fadeTime > 0)
		{
			FadeOutAndPlayNew(newStream, fadeTime);
		}
		// IF NOT playing but we want a fade (Fade In)
		else if (!this.Playing && fadeTime > 0)
		{
			this.Stream = newStream;
			this.VolumeDb = -80.0f; // Start silent
			this.Play();

			// Create a tween to fade in
			if (_fadeTween != null && _fadeTween.IsRunning()) _fadeTween.Kill();
			_fadeTween = CreateTween();
			_fadeTween.TweenProperty(this, "volume_db", 0.0f, fadeTime);
		}
		// Instant play
		else
		{
			if (_fadeTween != null && _fadeTween.IsRunning()) _fadeTween.Kill();
			this.Stream = newStream;
			this.VolumeDb = 0; 
			this.Play();
		}
	}


	/// <summary>
	/// Stops the current track with an optional fade out.
	/// </summary>
	public void StopTrack(float fadeTime = 1.0f)
	{
		if (!this.Playing) return;

		// Kill existing tween so it doesn't fight us
		if (_fadeTween != null && _fadeTween.IsRunning()) _fadeTween.Kill();

		if (fadeTime > 0)
		{
			_fadeTween = CreateTween();
			// Fade volume down to -80dB (effectively silent)
			_fadeTween.TweenProperty(this, "volume_db", -80.0f, fadeTime);
			
			// Stop playback once fade completes
			_fadeTween.TweenCallback(Callable.From(() => 
			{
				this.Stop();
				this.Stream = null; 
			}));
		}
		else
		{
			this.Stop();
			this.Stream = null;
		}
	}

	private void FadeOutAndPlayNew(AudioStream newStream, float duration)
	{
		// Kill existing tween if user switches tracks rapidly
		if (_fadeTween != null && _fadeTween.IsRunning()) _fadeTween.Kill();

		_fadeTween = CreateTween();
		
		// Fade out current track
		_fadeTween.TweenProperty(this, "volume_db", -80.0f, duration);
		
		// Callback to swap stream and fade back in
		_fadeTween.TweenCallback(Callable.From(() => 
		{
			this.Stream = newStream;
			this.Play();
		}));
		
		// Fade in new track
		_fadeTween.TweenProperty(this, "volume_db", 0.0f, duration);
	}

	private void LoadTrack(string name, string path)
	{
		var stream = GD.Load<AudioStream>(path);
		
		if (stream != null)
		{
			// Auto-configure looping for convenience
			if (stream is AudioStreamWav wavStream)
			{
				wavStream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
			}
			else if (stream is AudioStreamOggVorbis oggStream)
			{
				oggStream.Loop = true;
			}
			else if (stream is AudioStreamMP3 mp3Stream)
			{
				mp3Stream.Loop = true;
			}
			
			_tracks[name] = stream;
		}
		else
		{
			 GD.PrintErr($"MusicPlayer: Failed to load track '{name}' at path '{path}'");
		}
	}
}
