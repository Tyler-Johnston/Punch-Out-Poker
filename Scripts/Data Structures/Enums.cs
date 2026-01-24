using Godot;

/// <summary>
/// Possible outcomes after a poker hand is completed
/// Used for tilt tracking and personality adjustments
/// </summary>
public enum HandResult
{
	Win,              // Player won the hand normally
	Loss,             // Player lost the hand normally
	BadBeat,          // Player lost with a strong hand to a lucky draw
	BluffCaught,      // Player folded and opponent showed they were bluffing
	AllInLoss,        // Player went all-in and lost (extra tilting)
	Neutral           // Hand ended with no major emotional impact (e.g., everyone folded)
}

/// <summary>
/// Relative strength categories for poker hands
/// Used for tell system and decision-making shortcuts
/// </summary>
public enum HandStrength
{
	Strong,           // Premium hands (top pair or better)
	Medium,           // Marginal hands (weak pair, draws)
	Weak,             // Poor hands (high card, bottom pair)
	Bluffing          // AI is betting with weak hand intentionally
}

/// <summary>
/// Available poker actions during a betting round
/// </summary>
public enum PlayerAction
{
	Fold,             // Give up the hand
	Check,            // Pass action when no bet is facing (0 to call)
	Call,             // Match the current bet
	Raise,            // Increase the bet amount
	AllIn             // Bet all remaining chips
}

public enum Decision 
{ 
	Fold, 
	Call, 
	Check, 
	Bet 
}

/// <summary>
/// Standard poker hand rankings from lowest to highest
/// </summary>
public enum HandRank
{
	HighCard,         // No pairs or better
	OnePair,          // Two cards of same rank
	TwoPair,          // Two different pairs
	ThreeOfAKind,     // Three cards of same rank
	Straight,         // Five cards in sequence
	Flush,            // Five cards of same suit
	FullHouse,        // Three of a kind + pair
	FourOfAKind,      // Four cards of same rank
	StraightFlush,    // Five cards in sequence, same suit
	RoyalFlush        // A-K-Q-J-10 of same suit
}

/// <summary>
/// Card suits for standard 52-card deck
/// </summary>
public enum Suit
{
	Clubs,            // Order matches your ToEvaluatorFormat() method
	Diamonds,
	Hearts,
	Spades
}

/// <summary>
/// Card ranks from lowest to highest
/// Numeric values align with poker hand evaluation
/// Note: In your ToEvaluatorFormat, this uses 0-12 range (Ace=12)
/// </summary>
public enum Rank
{
	Two,              // 0 in evaluator format
	Three,            // 1
	Four,             // 2
	Five,             // 3
	Six,              // 4
	Seven,            // 5
	Eight,            // 6
	Nine,             // 7
	Ten,              // 8
	Jack,             // 9
	Queen,            // 10
	King,             // 11
	Ace               // 12
}

public enum Street
{
	Preflop,
	Flop,
	Turn,
	River
}

public enum TiltState
{
	Zen,        // 0 - 10   (Calm, playing optimally)
	Annoyed,    // 10 - 25  (Slightly looser, chatty)
	Steaming,   // 25 - 50  (Aggressive, betting bigger)
	Monkey      // 50+      (Shoving random hands, zero patience)
}

public enum Expression
{
	Neutral,
	Happy,
	Sad,
	Angry,
	Surprised,
	Annoyed,
	Worried,
	Smirk
}

public enum OpponentExitType
{
	None,
	RageQuit,   // TKO: They leave because of Tilt. YOU WIN (Unlock Next).
	Surrender   // Escape: They leave to save money. YOU LOSE/DRAW (No Unlock).
}
