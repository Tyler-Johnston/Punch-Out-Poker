using Godot;

/// <summary>
/// Data-driven AI personality configuration for poker opponents.
/// Each opponent has distinct traits that determine their playing style.
/// </summary>
public class OpponentProfile
{
	public string Name { get; set; }
	public int BuyIn { get; set; }

	// Personality traits (0.0-1.0)

	/// <summary>
	/// How often the AI bets/raises vs checks/calls. Higher = more aggressive.
	/// </summary>
	public float Aggression { get; set; }

	/// <summary>
	/// How wide a hand range they play. Higher = plays more hands.
	/// </summary>
	public float Looseness { get; set; }

	/// <summary>
	/// How frequently they bluff. Higher = more bluffs.
	/// </summary>
	public float Bluffiness { get; set; }

	/// <summary>
	/// How much they adjust to player tendencies. (Reserved for future use)
	/// </summary>
	public float Adaptability { get; set; }

	/// <summary>
	/// Controls deviation from optimal play. 
	/// 1.0 = Solid regular (near-optimal), 
	/// 1.1-1.2 = Weak regular (small leaks),
	/// 1.3-1.4 = Amateur (clearly exploitable),
	/// 1.5+ = Very weak (major mistakes).
	/// </summary>
	public float MistakeFactor { get; set; } = 1.0f;

	// Street-specific modifiers

	/// <summary>
	/// Aggression multiplier for preflop betting. 
	/// Values > 1.0 make AI more aggressive preflop.
	/// </summary>
	public float PreflopAggression { get; set; } = 1.0f;

	/// <summary>
	/// Aggression multiplier for postflop betting.
	/// Values < 1.0 make AI more passive after the flop.
	/// </summary>
	public float PostflopAggression { get; set; } = 1.0f;

	// Calculated decision thresholds

	/// <summary>
	/// Hand strength threshold below which AI will fold.
	/// Lower looseness = folds more often.
	/// </summary>
	public float FoldThreshold => 0.15f - (Looseness * 0.08f);

	/// <summary>
	/// Hand strength threshold for calling vs raising.
	/// Lower aggression = calls more, raises less.
	/// </summary>
	public float CallThreshold => 0.35f - (Aggression * 0.15f);

	/// <summary>
	/// Hand strength threshold above which AI will raise.
	/// Lower aggression = needs stronger hands to raise.
	/// </summary>
	public float RaiseThreshold => 0.60f - (Aggression * 0.20f);

	/// <summary>
	/// Probability of bluffing in any given situation.
	/// </summary>
	public float BluffChance => Bluffiness * 0.5f;

	/// <summary>
	/// Bet sizing as a fraction of the pot.
	/// Higher aggression = larger bets.
	/// </summary>
	public float BetSizeFactor => 0.4f + (Aggression * 0.6f);
}
