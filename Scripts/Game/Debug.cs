using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/*
 * DEBUG CONTROLS (DevTestMode only)
 * 
 * Street Navigation:
 *   Ctrl+F = Skip to Flop
 *   Ctrl+T = Skip to Turn
 *   Ctrl+R = Skip to River
 *   Ctrl+S = Force Showdown
 *   Ctrl+N = Start New Hand
 * 
 * Outcomes:
 *   Ctrl+W = Force Win
 *   Ctrl+L = Force Loss
 * 
 * Chips:
 *   Shift+= = Add 100 chips
 *   Shift+- = Remove 50 chips
 * 
 * Hand Presets (1-0):
 *   1 = Royal Flush
 *   2 = Straight Flush
 *   3 = Four of a Kind
 *   4 = Full House
 *   5 = Flush
 *   6 = Straight
 *   7 = Three of a Kind
 *   8 = Two Pair
 *   9 = Pair
 *   0 = High Card
 * 
 * Testing:
 *   Ctrl+A = Toggle AI on/off
 *   E = Cycle facial expressions
 *   D = Print game state to console
 */

public partial class PokerGame : Node2D
{
	private bool debugAIDisabled = false;
	private int debugExpressionIndex = 0;
	
	private enum HandPreset
	{
		RoyalFlush,
		StraightFlush,
		FourOfAKind,
		FullHouse,
		Flush,
		Straight,
		ThreeOfAKind,
		TwoPair,
		Pair,
		HighCard
	}
	
	public override void _Input(InputEvent @event)
	{
		if (!GameManager.Instance.DevTestMode) return;
		
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			HandleDebugInput(keyEvent);
		}
	}
	
	private void HandleDebugInput(InputEventKey keyEvent)
	{
		// Street navigation
		if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.F)
			DebugSkipToFlop();
		else if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.T)
			DebugSkipToTurn();
		else if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.R)
			DebugSkipToRiver();
		else if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.S)
			DebugSkipToShowdown();
		
		// Force outcomes
		else if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.W)
			DebugForceWin();
		else if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.L)
			DebugForceLoss();
		
		// Chip manipulation
		else if (keyEvent.ShiftPressed && keyEvent.Keycode == Key.Equal)
			DebugAddChips(100);
		else if (keyEvent.ShiftPressed && keyEvent.Keycode == Key.Minus)
			DebugRemoveChips(50);
		
		// Force specific hands (number keys)
		else if (keyEvent.Keycode == Key.Key1)
			DebugSetPlayerHand(HandPreset.RoyalFlush);
		else if (keyEvent.Keycode == Key.Key2)
			DebugSetPlayerHand(HandPreset.StraightFlush);
		else if (keyEvent.Keycode == Key.Key3)
			DebugSetPlayerHand(HandPreset.FourOfAKind);
		else if (keyEvent.Keycode == Key.Key4)
			DebugSetPlayerHand(HandPreset.FullHouse);
		else if (keyEvent.Keycode == Key.Key5)
			DebugSetPlayerHand(HandPreset.Flush);
		else if (keyEvent.Keycode == Key.Key6)
			DebugSetPlayerHand(HandPreset.Straight);
		else if (keyEvent.Keycode == Key.Key7)
			DebugSetPlayerHand(HandPreset.ThreeOfAKind);
		else if (keyEvent.Keycode == Key.Key8)
			DebugSetPlayerHand(HandPreset.TwoPair);
		else if (keyEvent.Keycode == Key.Key9)
			DebugSetPlayerHand(HandPreset.Pair);
		else if (keyEvent.Keycode == Key.Key0)
			DebugSetPlayerHand(HandPreset.HighCard);
		
		// AI & Visual testing
		else if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.A)
			DebugToggleAI();
		else if (keyEvent.Keycode == Key.E)
			DebugCycleExpression();
		else if (keyEvent.Keycode == Key.D)
			DebugPrintGameState();
		
		// New hand
		else if (keyEvent.CtrlPressed && keyEvent.Keycode == Key.N)
			DebugStartNewHand();
	}
	
	#region Street Navigation
	
	private async void DebugSkipToFlop()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG] No hand in progress");
			return;
		}
		
		if (currentStreet >= Street.Flop)
		{
			GD.Print("[DEBUG] Already at or past Flop");
			return;
		}
		
		currentStreet = Street.Flop;
		await DealCommunityCards(Street.Flop);
		ResetBettingRound();
		isPlayerTurn = true;
		UpdateButtonLabels();
		UpdateHud();
		GD.Print("[DEBUG] Skipped to Flop");
	}
	
	private async void DebugSkipToTurn()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG] No hand in progress");
			return;
		}
		
		if (currentStreet < Street.Flop)
		{
			currentStreet = Street.Flop;
			await DealCommunityCards(Street.Flop);
		}
		
		if (currentStreet >= Street.Turn)
		{
			GD.Print("[DEBUG] Already at or past Turn");
			return;
		}
		
		currentStreet = Street.Turn;
		await DealCommunityCards(Street.Turn);
		ResetBettingRound();
		isPlayerTurn = true;
		UpdateButtonLabels();
		UpdateHud();
		GD.Print("[DEBUG] Skipped to Turn");
	}
	
	private async void DebugSkipToRiver()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG] No hand in progress");
			return;
		}
		
		if (currentStreet < Street.Flop)
		{
			currentStreet = Street.Flop;
			await DealCommunityCards(Street.Flop);
		}
		
		if (currentStreet < Street.Turn)
		{
			currentStreet = Street.Turn;
			await DealCommunityCards(Street.Turn);
		}
		
		if (currentStreet >= Street.River)
		{
			GD.Print("[DEBUG] Already at River");
			return;
		}
		
		currentStreet = Street.River;
		await DealCommunityCards(Street.River);
		ResetBettingRound();
		isPlayerTurn = true;
		UpdateButtonLabels();
		UpdateHud();
		GD.Print("[DEBUG] Skipped to River");
	}
	
	private async void DebugSkipToShowdown()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG] No hand in progress");
			return;
		}
		
		if (currentStreet < Street.River)
		{
			await DebugSkipToRiverAsync();
		}
		
		GetTree().CreateTimer(0.5f).Timeout += () => ShowDown();
		GD.Print("[DEBUG] Forcing Showdown");
	}
	
	private async Task DebugSkipToRiverAsync()
	{
		if (currentStreet < Street.Flop)
		{
			currentStreet = Street.Flop;
			await DealCommunityCards(Street.Flop);
		}
		
		if (currentStreet < Street.Turn)
		{
			currentStreet = Street.Turn;
			await DealCommunityCards(Street.Turn);
		}
		
		if (currentStreet < Street.River)
		{
			currentStreet = Street.River;
			await DealCommunityCards(Street.River);
		}
	}
	
	#endregion
	
	#region Force Outcomes
	
	private void DebugForceWin()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG] No hand in progress");
			return;
		}
		
		int winAmount = pot;
		playerChips += winAmount;
		opponentChips = Math.Max(0, opponentChips - (pot - playerContributed));
		pot = 0;
		
		ShowMessage($"[DEBUG] You win ${winAmount}!");
		GD.Print($"[DEBUG] Forced player win - awarded ${winAmount}");
		
		handInProgress = false;
		GetTree().CreateTimer(1.5f).Timeout += EndHand;
	}
	
	private void DebugForceLoss()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG] No hand in progress");
			return;
		}
		
		int lossAmount = pot;
		opponentChips += lossAmount;
		playerChips = Math.Max(0, playerChips - (pot - opponentContributed));
		pot = 0;
		
		ShowMessage($"[DEBUG] {currentOpponentName} wins ${lossAmount}!");
		GD.Print($"[DEBUG] Forced player loss - opponent awarded ${lossAmount}");
		
		handInProgress = false;
		GetTree().CreateTimer(1.5f).Timeout += EndHand;
	}
	
	#endregion
	
	#region Chip Manipulation
	
	private void DebugAddChips(int amount)
	{
		playerChips += amount;
		UpdateHud();
		GD.Print($"[DEBUG] Added {amount} chips to player (Total: {playerChips})");
	}
	
	private void DebugRemoveChips(int amount)
	{
		int removed = Math.Min(amount, playerChips);
		playerChips -= removed;
		UpdateHud();
		GD.Print($"[DEBUG] Removed {removed} chips from player (Total: {playerChips})");
	}
	
	#endregion
	
	#region Hand Presets
	
	private async void DebugSetPlayerHand(HandPreset preset)
	{
		if (playerHand.Count < 2)
		{
			GD.Print("[DEBUG] Player hand not dealt yet");
			return;
		}
		
		switch (preset)
		{
			case HandPreset.RoyalFlush:
				playerHand[0] = new Card(Rank.Ace, Suit.Hearts);
				playerHand[1] = new Card(Rank.King, Suit.Hearts);
				break;
				
			case HandPreset.StraightFlush:
				playerHand[0] = new Card(Rank.Nine, Suit.Diamonds);
				playerHand[1] = new Card(Rank.Eight, Suit.Diamonds);
				break;
				
			case HandPreset.FourOfAKind:
				playerHand[0] = new Card(Rank.Queen, Suit.Hearts);
				playerHand[1] = new Card(Rank.Queen, Suit.Spades);
				break;
				
			case HandPreset.FullHouse:
				playerHand[0] = new Card(Rank.Jack, Suit.Hearts);
				playerHand[1] = new Card(Rank.Jack, Suit.Diamonds);
				break;
				
			case HandPreset.Flush:
				playerHand[0] = new Card(Rank.Ace, Suit.Clubs);
				playerHand[1] = new Card(Rank.Nine, Suit.Clubs);
				break;
				
			case HandPreset.Straight:
				playerHand[0] = new Card(Rank.Ten, Suit.Hearts);
				playerHand[1] = new Card(Rank.Nine, Suit.Spades);
				break;
				
			case HandPreset.ThreeOfAKind:
				playerHand[0] = new Card(Rank.Eight, Suit.Hearts);
				playerHand[1] = new Card(Rank.Eight, Suit.Diamonds);
				break;
				
			case HandPreset.TwoPair:
				playerHand[0] = new Card(Rank.King, Suit.Hearts);
				playerHand[1] = new Card(Rank.Queen, Suit.Hearts);
				break;
				
			case HandPreset.Pair:
				playerHand[0] = new Card(Rank.Ace, Suit.Hearts);
				playerHand[1] = new Card(Rank.Ace, Suit.Spades);
				break;
				
			case HandPreset.HighCard:
				playerHand[0] = new Card(Rank.Ace, Suit.Hearts);
				playerHand[1] = new Card(Rank.Seven, Suit.Clubs);
				break;
		}
		
		// Use RevealCard instead of SetCard
		await playerCard1.RevealCard(playerHand[0]);
		await playerCard2.RevealCard(playerHand[1]);
		GD.Print($"[DEBUG] Set player hand to {preset}");
	}
	
	#endregion
	
	#region AI & Visual Testing
	
	private void DebugToggleAI()
	{
		debugAIDisabled = !debugAIDisabled;
		GD.Print($"[DEBUG] AI is now {(debugAIDisabled ? "DISABLED" : "ENABLED")}");
		
		if (debugAIDisabled)
		{
			ShowMessage("[DEBUG] AI Disabled - manual control");
		}
		else
		{
			ShowMessage("[DEBUG] AI Enabled");
		}
	}
	
	public bool IsAIDebugDisabled()
	{
		return debugAIDisabled;
	}
	
	private void DebugCycleExpression()
	{
		var expressions = Enum.GetValues(typeof(Expression));
		var expr = (Expression)expressions.GetValue(debugExpressionIndex % expressions.Length);
		SetExpression(expr);
		GD.Print($"[DEBUG] Expression: {expr}");
		debugExpressionIndex++;
	}
	
	#endregion
	
	#region Game State Info
	
	private void DebugPrintGameState()
	{
		GD.Print("\n=== DEBUG GAME STATE ===");
		GD.Print($"Street: {currentStreet}");
		GD.Print($"Pot: ${pot}");
		GD.Print($"Current Bet: ${currentBet}");
		GD.Print($"Player: ${playerChips} (Bet: ${playerBet}, Contributed: ${playerContributed})");
		GD.Print($"Opponent: ${opponentChips} (Bet: ${opponentBet}, Contributed: ${opponentContributed})");
		GD.Print($"Player Turn: {isPlayerTurn}");
		GD.Print($"Hand In Progress: {handInProgress}");
		GD.Print($"Player All-In: {playerIsAllIn}");
		GD.Print($"Opponent All-In: {opponentIsAllIn}");
		GD.Print($"AI Tilt: {aiOpponent.CurrentTiltState}");
		GD.Print($"Community Cards: {communityCards.Count}");
		GD.Print("========================\n");
	}
	
	private void DebugStartNewHand()
	{
		GD.Print("[DEBUG] Forcing new hand");
		handInProgress = false;
		waitingForNextGame = true;
		StartNewHand();
	}
	
	#endregion
}
