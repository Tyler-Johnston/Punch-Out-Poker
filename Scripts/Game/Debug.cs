using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
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
 *   
 * EDGE CASE TESTING:
 *   Ctrl+Shift+1 = Force AI Short Stack (10 chips)
 *   Ctrl+Shift+2 = Force Player Short Stack (10 chips)
 *   Ctrl+Shift+3 = Force Both All-In
 *   Ctrl+Shift+4 = Force AI Value-Raise Scenario (postflop)
 *   Ctrl+Shift+5 = Force Unbalanced Bets (player > opponent)
 *   Ctrl+Shift+6 = Force Large Pot, Small Stacks (pot odds test)
 *   Ctrl+Shift+7 = Force Equal Bets But No Action Flags Set
 *   Ctrl+Shift+8 = Force Negative Pot Test (overflow check)
 *   Ctrl+Shift+9 = Force AI at Exact CurrentBet (raise = 0 test)
 *   Ctrl+Shift+0 = Force Race Condition (spam AI turns)
 *   
 *   Ctrl+Shift+B = Force AI Max Tilt (rage quit risk)
 *   Ctrl+Shift+C = Force Counterfeit Board (trips/quads)
 *   Ctrl+Shift+X = Print Detailed AI State
 *   
 * FULL-RAISE / UNDER-RAISE TESTING:
 *   Ctrl+Shift+U = Test Under-Raise All-In (should NOT reopen betting)
 *   Ctrl+Shift+I = Test Full-Raise All-In (SHOULD reopen betting)
 *   Ctrl+Shift+M = Test Min-Raise Boundary (exactly at threshold)
 *   Ctrl+Shift+P = Print Reopening Flags State
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
		// Edge case testing (Ctrl+Shift combos)
		if (keyEvent.CtrlPressed && keyEvent.ShiftPressed)
		{
			HandleEdgeCaseInput(keyEvent);
			return;
		}
		
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
	
	#region Edge Case Testing
	
	private void HandleEdgeCaseInput(InputEventKey keyEvent)
	{
		switch (keyEvent.Keycode)
		{
			case Key.Key1:
				DebugForceAIShortStack();
				break;
			case Key.Key2:
				DebugForcePlayerShortStack();
				break;
			case Key.Key3:
				DebugForceBothAllIn();
				break;
			case Key.Key4:
				DebugForceAIValueRaiseScenario();
				break;
			case Key.Key5:
				DebugForceUnbalancedBets();
				break;
			case Key.Key6:
				DebugForceLargePotSmallStacks();
				break;
			case Key.Key7:
				DebugForceEqualBetsNoFlags();
				break;
			case Key.Key8:
				DebugForceNegativePotTest();
				break;
			case Key.Key9:
				DebugForceAIAtExactCurrentBet();
				break;
			case Key.Key0:
				DebugForceRaceCondition();
				break;
			case Key.B:
				DebugForceMaxTilt();
				break;
			case Key.C:
				DebugForceCounterfeitBoard();
				break;
			case Key.X:
				DebugPrintDetailedAIState();
				break;
			// ✅ NEW: Full-raise / Under-raise testing
			case Key.U:
				DebugTestUnderRaiseAllIn();
				break;
			case Key.I:
				DebugTestFullRaiseAllIn();
				break;
			case Key.M:
				DebugTestMinRaiseBoundary();
				break;
			case Key.P:
				DebugPrintReopeningFlags();
				break;
		}
	}
	
	// ✅ NEW: Test under-raise all-in (should NOT reopen betting)
	private void DebugTestUnderRaiseAllIn()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG REOPEN] No hand in progress");
			return;
		}
		
		if (currentStreet < Street.Flop)
		{
			GD.Print("[DEBUG REOPEN] Use Ctrl+F to skip to flop first");
			return;
		}
		
		// Scenario: Player raises to $80 (last raise was $40)
		// AI has only $95 total, goes all-in for $95 (only $15 more)
		// This is an UNDER-RAISE and should NOT reopen betting for player
		
		playerChips = 200;
		opponentChips = 15;  // AI has 15 chips left
		
		playerBet = 80;
		opponentBet = 40;
		currentBet = 80;
		previousBet = 40;
		lastRaiseAmount = 40;  // Last raise increment was $40
		
		pot = 100;
		displayPot = 100;
		playerChipsInPot = 80;
		opponentChipsInPot = 40;
		
		playerHasActedThisStreet = true;
		opponentHasActedThisStreet = true;  // Both acted before
		playerCanReopenBetting = false;  // Player already raised
		opponentCanReopenBetting = true;   // Opponent can act
		
		isPlayerTurn = false;
		playerIsAllIn = false;
		opponentIsAllIn = false;
		
		aiOpponent.ChipStack = opponentChips;
		aiOpponent.IsAllIn = false;
		
		UpdateHud();
		
		GD.Print("\n[DEBUG REOPEN] ===== UNDER-RAISE ALL-IN TEST =====");
		GD.Print($"[DEBUG REOPEN] Setup:");
		GD.Print($"  Player: ${playerChips} stack, bet ${playerBet}");
		GD.Print($"  Opponent: ${opponentChips} stack, bet ${opponentBet}");
		GD.Print($"  CurrentBet: ${currentBet}, LastRaiseAmount: ${lastRaiseAmount}");
		GD.Print($"  Min-raise required: ${lastRaiseAmount} (to ${currentBet + lastRaiseAmount})");
		GD.Print($"\n[DEBUG REOPEN] AI will now go all-in for ${opponentChips + opponentBet} total");
		GD.Print($"[DEBUG REOPEN] That's only ${opponentChips} more (< ${lastRaiseAmount} min-raise)");
		GD.Print($"[DEBUG REOPEN] EXPECTED: Under-raise, playerCanReopenBetting should stay FALSE");
		GD.Print($"[DEBUG REOPEN] EXPECTED: Street should advance immediately, NO action for player");
		GD.Print($"[DEBUG REOPEN] Watch for '[UNDER-RAISE ALL-IN]' log message\n");
		
		GetTree().CreateTimer(1.0f).Timeout += () => CheckAndProcessAITurn();
	}
	
	// ✅ NEW: Test full-raise all-in (SHOULD reopen betting)
	private void DebugTestFullRaiseAllIn()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG REOPEN] No hand in progress");
			return;
		}
		
		if (currentStreet < Street.Flop)
		{
			GD.Print("[DEBUG REOPEN] Use Ctrl+F to skip to flop first");
			return;
		}
		
		// Scenario: Player raises to $50 (last raise was $30)
		// AI has $100 left, goes all-in for $150 total ($100 more)
		// This is a FULL RAISE and SHOULD reopen betting for player
		
		playerChips = 200;
		opponentChips = 100;  // AI has 100 chips left
		
		playerBet = 50;
		opponentBet = 20;
		currentBet = 50;
		previousBet = 20;
		lastRaiseAmount = 30;  // Last raise increment was $30
		
		pot = 80;
		displayPot = 80;
		playerChipsInPot = 50;
		opponentChipsInPot = 20;
		
		playerHasActedThisStreet = true;
		opponentHasActedThisStreet = false;
		playerCanReopenBetting = false;  // Player already raised
		opponentCanReopenBetting = true;
		
		isPlayerTurn = false;
		playerIsAllIn = false;
		opponentIsAllIn = false;
		
		aiOpponent.ChipStack = opponentChips;
		aiOpponent.IsAllIn = false;
		
		UpdateHud();
		
		GD.Print("\n[DEBUG REOPEN] ===== FULL-RAISE ALL-IN TEST =====");
		GD.Print($"[DEBUG REOPEN] Setup:");
		GD.Print($"  Player: ${playerChips} stack, bet ${playerBet}");
		GD.Print($"  Opponent: ${opponentChips} stack, bet ${opponentBet}");
		GD.Print($"  CurrentBet: ${currentBet}, LastRaiseAmount: ${lastRaiseAmount}");
		GD.Print($"  Min-raise required: ${lastRaiseAmount} (to ${currentBet + lastRaiseAmount})");
		GD.Print($"\n[DEBUG REOPEN] AI will now go all-in for ${opponentChips + opponentBet} total");
		GD.Print($"[DEBUG REOPEN] That's ${opponentChips} more (>= ${lastRaiseAmount} min-raise)");
		GD.Print($"[DEBUG REOPEN] EXPECTED: Full raise, playerCanReopenBetting should become TRUE");
		GD.Print($"[DEBUG REOPEN] EXPECTED: Player should get action back");
		GD.Print($"[DEBUG REOPEN] Watch for '[FULL ALL-IN]' log message\n");
		
		GetTree().CreateTimer(1.0f).Timeout += () => CheckAndProcessAITurn();
	}
	
	// ✅ NEW: Test exact min-raise boundary
	private void DebugTestMinRaiseBoundary()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG REOPEN] No hand in progress");
			return;
		}
		
		if (currentStreet < Street.Flop)
		{
			GD.Print("[DEBUG REOPEN] Use Ctrl+F to skip to flop first");
			return;
		}
		
		// Scenario: Player bets $50, last raise was $50 (from 0 to 50)
		// AI goes all-in for exactly $100 (exactly $50 more = min-raise)
		// This is EXACTLY at the boundary and SHOULD be a full raise
		
		playerChips = 200;
		opponentChips = 50;  // AI has exactly 50 chips left
		
		playerBet = 50;
		opponentBet = 0;
		currentBet = 50;
		previousBet = 0;
		lastRaiseAmount = 50;  // Last raise increment was $50
		
		pot = 60;
		displayPot = 60;
		playerChipsInPot = 50;
		opponentChipsInPot = 0;
		
		playerHasActedThisStreet = true;
		opponentHasActedThisStreet = false;
		playerCanReopenBetting = false;
		opponentCanReopenBetting = true;
		
		isPlayerTurn = false;
		playerIsAllIn = false;
		opponentIsAllIn = false;
		
		aiOpponent.ChipStack = opponentChips;
		aiOpponent.IsAllIn = false;
		
		UpdateHud();
		
		GD.Print("\n[DEBUG REOPEN] ===== MIN-RAISE BOUNDARY TEST =====");
		GD.Print($"[DEBUG REOPEN] Setup:");
		GD.Print($"  Player: ${playerChips} stack, bet ${playerBet}");
		GD.Print($"  Opponent: ${opponentChips} stack, bet ${opponentBet}");
		GD.Print($"  CurrentBet: ${currentBet}, LastRaiseAmount: ${lastRaiseAmount}");
		GD.Print($"  Min-raise required: ${lastRaiseAmount} (to ${currentBet + lastRaiseAmount})");
		GD.Print($"\n[DEBUG REOPEN] AI will now go all-in for ${opponentChips + opponentBet} total");
		GD.Print($"[DEBUG REOPEN] That's EXACTLY ${opponentChips} more (= ${lastRaiseAmount} min-raise)");
		GD.Print($"[DEBUG REOPEN] EXPECTED: Full raise (boundary case), playerCanReopenBetting should become TRUE");
		GD.Print($"[DEBUG REOPEN] EXPECTED: Player should get action back");
		GD.Print($"[DEBUG REOPEN] Watch for '[FULL ALL-IN]' log message\n");
		
		GetTree().CreateTimer(1.0f).Timeout += () => CheckAndProcessAITurn();
	}

	
	// ✅ NEW: Print reopening flags state
	private void DebugPrintReopeningFlags()
	{
		GD.Print("\n[DEBUG REOPEN] ===== REOPENING FLAGS STATE =====");
		GD.Print($"lastRaiseAmount: ${lastRaiseAmount}");
		GD.Print($"playerCanReopenBetting: {playerCanReopenBetting}");
		GD.Print($"opponentCanReopenBetting: {opponentCanReopenBetting}");
		GD.Print($"");
		GD.Print($"currentBet: ${currentBet}");
		GD.Print($"previousBet: ${previousBet}");
		GD.Print($"playerBet: ${playerBet}");
		GD.Print($"opponentBet: ${opponentBet}");
		GD.Print($"");
		GD.Print($"playerHasActedThisStreet: {playerHasActedThisStreet}");
		GD.Print($"opponentHasActedThisStreet: {opponentHasActedThisStreet}");
		GD.Print($"playerIsAllIn: {playerIsAllIn}");
		GD.Print($"opponentIsAllIn: {opponentIsAllIn}");
		GD.Print($"");
		GD.Print($"Min-raise would be: ${currentBet} + ${lastRaiseAmount} = ${currentBet + lastRaiseAmount}");
		GD.Print($"============================================\n");
	}
	
	// [Rest of your existing edge case methods remain unchanged]
	
	// EDGE CASE 1: AI has very few chips (tests min-raise logic)
	private void DebugForceAIShortStack()
	{
		opponentChips = 10;
		aiOpponent.ChipStack = 10;
		RefreshAllInFlagsFromStacks();
		UpdateHud();
		GD.Print("[DEBUG EDGE] AI forced to 10 chips (short stack)");
		GD.Print($"[DEBUG EDGE] CurrentBet={currentBet}, OpponentBet={opponentBet}");
	}
	
	// EDGE CASE 2: Player has very few chips
	private void DebugForcePlayerShortStack()
	{
		playerChips = 10;
		RefreshAllInFlagsFromStacks();
		UpdateHud();
		GD.Print("[DEBUG EDGE] Player forced to 10 chips (short stack)");
		GD.Print($"[DEBUG EDGE] CurrentBet={currentBet}, PlayerBet={playerBet}");
	}
	
	// EDGE CASE 3: Both players all-in (tests showdown runout)
	private void DebugForceBothAllIn()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		// Set both to all-in state
		playerChips = 0;
		opponentChips = 0;
		playerBet = 100;
		opponentBet = 100;
		currentBet = 100;
		pot = 200;
		playerIsAllIn = true;
		opponentIsAllIn = true;
		aiOpponent.IsAllIn = true;
		
		UpdateHud();
		GD.Print("[DEBUG EDGE] Both players forced all-in");
		GD.Print("[DEBUG EDGE] Should skip to showdown runout");
		
		// Trigger street advance (should keep advancing without betting)
		GetTree().CreateTimer(1.0f).Timeout += AdvanceStreet;
	}
	
	// EDGE CASE 4: Force AI into raise decision with strong hand (CORRECTED)
	private void DebugForceAIValueRaiseScenario()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		// Need to be at flop or later for predictable AI decisions
		if (currentStreet < Street.Flop)
		{
			GD.Print("[DEBUG EDGE] Need to be at flop or later. Use Ctrl+F first.");
			return;
		}
		
		// Scenario: Small bet into AI, AI has strong hand, should raise
		playerBet = 10;
		currentBet = 10;
		opponentBet = 0;
		opponentChips = 200;
		playerChips = 100;
		pot = 80;  // Large pot to incentivize raise
		
		// Give AI a premium hand
		bool boardHasAce = communityCards.Any(c => c.Rank == Rank.Ace);
		if (boardHasAce && opponentHand.Count >= 2)
		{
			// Give AI trip Aces (nuts)
			opponentHand[0] = new Card(Rank.Ace, Suit.Spades);
			opponentHand[1] = new Card(Rank.Ace, Suit.Hearts);
			aiOpponent.Hand.Clear();
			aiOpponent.Hand.Add(opponentHand[0]);
			aiOpponent.Hand.Add(opponentHand[1]);
			GD.Print("[DEBUG EDGE] Gave AI trip Aces (nuts)");
		}
		else if (communityCards.Count >= 3)
		{
			// Give AI top pair or better
			var highCard = communityCards.OrderByDescending(c => (int)c.Rank).FirstOrDefault();
			if (highCard != null)
			{
				opponentHand[0] = new Card(highCard.Rank, Suit.Spades);
				opponentHand[1] = new Card(highCard.Rank, Suit.Hearts);
				aiOpponent.Hand.Clear();
				aiOpponent.Hand.Add(opponentHand[0]);
				aiOpponent.Hand.Add(opponentHand[1]);
				GD.Print($"[DEBUG EDGE] Gave AI overpair to {highCard.Rank}");
			}
		}
		
		// ✅ FIX: Set action flags correctly
		playerHasActedThisStreet = true;   // Player already acted (bet $10)
		opponentHasActedThisStreet = false; // Opponent hasn't acted yet
		isPlayerTurn = false;               // It's opponent's turn
		
		// Pot tracking
		playerChipsInPot = 10;
		opponentChipsInPot = 0;
		playerContributed = 10;
		opponentContributed = 0;
		
		playerIsAllIn = false;
		opponentIsAllIn = false;
		aiOpponent.IsAllIn = false;
		
		UpdateHud();
		GD.Print("[DEBUG EDGE] Forced AI value-raise scenario:");
		GD.Print($"[DEBUG EDGE] Pot={pot}, CurrentBet={currentBet}, AI has premium hand");
		GD.Print($"[DEBUG EDGE] playerHasActedThisStreet={playerHasActedThisStreet}, opponentHasActedThisStreet={opponentHasActedThisStreet}");
		GD.Print($"[DEBUG EDGE] isPlayerTurn={isPlayerTurn}");
		GD.Print($"[DEBUG EDGE] AI should decide to RAISE");
		GD.Print($"[DEBUG EDGE] Watch for AI raise decision and chip movement");
		
		GetTree().CreateTimer(0.5f).Timeout += () => CheckAndProcessAITurn();
	}
	
	// EDGE CASE 5: Unbalanced bets (player bet > opponent bet, wrong turn flag)
	private void DebugForceUnbalancedBets()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		// Create illegal state: player bet higher but it's player's turn
		playerBet = 50;
		opponentBet = 20;
		currentBet = 50;
		isPlayerTurn = true;  // Wrong! Should be opponent's turn
		playerHasActedThisStreet = true;
		opponentHasActedThisStreet = false;
		
		UpdateHud();
		GD.Print("[DEBUG EDGE] Forced unbalanced bet state:");
		GD.Print($"[DEBUG EDGE] playerBet={playerBet}, opponentBet={opponentBet}");
		GD.Print($"[DEBUG EDGE] isPlayerTurn={isPlayerTurn} (should be false!)");
		GD.Print("[DEBUG EDGE] This tests if betting round ends prematurely");
	}
	
	// EDGE CASE 6: Large pot, tiny stacks (tests pot odds override logic)
	private void DebugForceLargePotSmallStacks()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		pot = 500;
		displayPot = 500;
		playerChips = 20;
		opponentChips = 15;
		currentBet = 10;
		playerBet = 10;
		opponentBet = 0;
		
		// ✅ FIX: Set action flags
		playerHasActedThisStreet = true;
		opponentHasActedThisStreet = false;
		isPlayerTurn = false;
		
		playerChipsInPot = 10;
		opponentChipsInPot = 0;
		
		RefreshAllInFlagsFromStacks();
		UpdateHud();
		
		GD.Print("[DEBUG EDGE] Forced large pot, small stacks:");
		GD.Print($"[DEBUG EDGE] Pot={pot}, PlayerChips={playerChips}, OpponentChips={opponentChips}");
		GD.Print("[DEBUG EDGE] AI should call almost any bet due to pot odds");
		
		GetTree().CreateTimer(0.5f).Timeout += () => CheckAndProcessAITurn();
	}
	
	// EDGE CASE 7: Bets equal but action flags not set (tests round-complete logic)
	private void DebugForceEqualBetsNoFlags()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		playerBet = 50;
		opponentBet = 50;
		currentBet = 50;
		playerHasActedThisStreet = false;  // ⚠️ Should be true
		opponentHasActedThisStreet = false;  // ⚠️ Should be true
		isPlayerTurn = true;
		
		UpdateHud();
		GD.Print("[DEBUG EDGE] Forced equal bets but no action flags:");
		GD.Print($"[DEBUG EDGE] playerBet={playerBet}, opponentBet={opponentBet}");
		GD.Print($"[DEBUG EDGE] bothActed should be true but is false");
		GD.Print("[DEBUG EDGE] Round should NOT advance (waiting for actions)");
	}
	
	// EDGE CASE 8: Negative pot test (overflow/underflow check)
	private void DebugForceNegativePotTest()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		pot = -50;  // Illegal state
		displayPot = -50;
		playerChipsInPot = 100;
		opponentChipsInPot = 100;
		
		UpdateHud();
		GD.Print("[DEBUG EDGE] ⚠️ FORCED NEGATIVE POT!");
		GD.Print($"[DEBUG EDGE] pot={pot}, displayPot={displayPot}");
		GD.Print("[DEBUG EDGE] This tests if UI/logic handles negatives gracefully");
	}
	
	// EDGE CASE 9: AI bet exactly at currentBet (raise would add 0 chips)
	private void DebugForceAIAtExactCurrentBet()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		currentBet = 50;
		playerBet = 50;
		opponentBet = 50;  // AI already at currentBet
		opponentChips = 100;
		
		// ✅ FIX: Set action flags correctly
		playerHasActedThisStreet = true;
		opponentHasActedThisStreet = false;
		isPlayerTurn = false;
		
		UpdateHud();
		GD.Print("[DEBUG EDGE] Forced AI at exact currentBet:");
		GD.Print($"[DEBUG EDGE] opponentBet={opponentBet}, currentBet={currentBet}");
		GD.Print("[DEBUG EDGE] If AI decides to raise, raiseToTotal will be <= opponentBet");
		GD.Print("[DEBUG EDGE] Should trigger 'raise produced 0 add' path or convert to check");
		
		GetTree().CreateTimer(0.5f).Timeout += () => CheckAndProcessAITurn();
	}
	
	// EDGE CASE 10: Race condition stress test (spam AI turns)
	private async void DebugForceRaceCondition()
	{
		if (!handInProgress)
		{
			GD.Print("[DEBUG EDGE] No hand in progress");
			return;
		}
		
		GD.Print("[DEBUG EDGE] ⚠️ SPAMMING AI TURNS (race condition test)");
		GD.Print("[DEBUG EDGE] Watch for 'already processing' blocks");
		
		// Spam multiple AI turn requests simultaneously
		for (int i = 0; i < 5; i++)
		{
			CheckAndProcessAITurn();
			await ToSignal(GetTree().CreateTimer(0.05f), SceneTreeTimer.SignalName.Timeout);
		}
		
		GD.Print("[DEBUG EDGE] Race condition spam complete - check logs for lock conflicts");
	}
	
	// EDGE CASE: Max tilt (tests rage quit)
	private void DebugForceMaxTilt()
	{
		if (aiOpponent == null)
		{
			GD.Print("[DEBUG EDGE] AI opponent not initialized");
			return;
		}
		
		aiOpponent.Personality.TiltMeter = 95f;
		aiOpponent.Personality.ConsecutiveLosses = 5;
		
		GD.Print("[DEBUG EDGE] Forced AI to max tilt:");
		GD.Print($"[DEBUG EDGE] TiltMeter={aiOpponent.Personality.TiltMeter}");
		GD.Print($"[DEBUG EDGE] ConsecutiveLosses={aiOpponent.Personality.ConsecutiveLosses}");
		GD.Print($"[DEBUG EDGE] TiltState={aiOpponent.CurrentTiltState}");
		GD.Print("[DEBUG EDGE] AI may rage quit or play erratically");
		
		UpdateOpponentVisuals();
	}
	
	// EDGE CASE: Counterfeit board (trips/quads on board)
	private async void DebugForceCounterfeitBoard()
	{
		if (!handInProgress || currentStreet < Street.Flop)
		{
			GD.Print("[DEBUG EDGE] Need to be at flop or later");
			return;
		}
		
		// Force trips on board
		communityCards.Clear();
		communityCards.Add(new Card(Rank.King, Suit.Hearts));
		communityCards.Add(new Card(Rank.King, Suit.Diamonds));
		communityCards.Add(new Card(Rank.King, Suit.Clubs));
		
		if (currentStreet >= Street.Turn)
			communityCards.Add(new Card(Rank.Seven, Suit.Spades));
		if (currentStreet >= Street.River)
			communityCards.Add(new Card(Rank.Two, Suit.Hearts));
		
		// Update visuals
		await flop1.RevealCard(communityCards[0]);
		await flop2.RevealCard(communityCards[1]);
		await flop3.RevealCard(communityCards[2]);
		
		if (currentStreet >= Street.Turn)
			await turnCard.RevealCard(communityCards[3]);
		if (currentStreet >= Street.River)
			await riverCard.RevealCard(communityCards[4]);
		
		GD.Print("[DEBUG EDGE] Forced trip Kings on board (counterfeit test)");
		GD.Print("[DEBUG EDGE] AI hand strength should detect counterfeit situation");
		GD.Print("[DEBUG EDGE] Watch AI decision-making for downgrade logic");
	}
	
	// Detailed AI state dump
	private void DebugPrintDetailedAIState()
	{
		if (aiOpponent == null)
		{
			GD.Print("[DEBUG EDGE] AI opponent not initialized");
			return;
		}
		
		GD.Print("\n=== DEBUG DETAILED AI STATE ===");
		GD.Print($"Name: {aiOpponent.PlayerName}");
		GD.Print($"ChipStack: {aiOpponent.ChipStack}");
		GD.Print($"IsFolded: {aiOpponent.IsFolded}");
		GD.Print($"IsAllIn: {aiOpponent.IsAllIn}");
		GD.Print($"CurrentBetThisRound: {aiOpponent.CurrentBetThisRound}");
		GD.Print($"Hand: {(aiOpponent.Hand.Count >= 2 ? $"{aiOpponent.Hand[0]}, {aiOpponent.Hand[1]}" : "Not dealt")}");
		
		var personality = aiOpponent.Personality;
		GD.Print("\n--- Personality ---");
		GD.Print($"BaseAggression: {personality.BaseAggression:F2}");
		GD.Print($"CurrentAggression: {personality.CurrentAggression:F2}");
		GD.Print($"BaseBluffFrequency: {personality.BaseBluffFrequency:F2}");
		GD.Print($"CurrentBluffFrequency: {personality.CurrentBluffFrequency:F2}");
		GD.Print($"CallTendency: {personality.CallTendency:F2}");
		GD.Print($"CurrentRiskTolerance: {personality.CurrentRiskTolerance:F2}");
		GD.Print($"TiltMeter: {personality.TiltMeter:F1}");
		GD.Print($"TiltSensitivity: {personality.TiltSensitivity:F2}");
		GD.Print($"ConsecutiveLosses: {personality.ConsecutiveLosses}");
		GD.Print($"TiltState: {aiOpponent.CurrentTiltState}");
		
		GD.Print("\n--- Random Seeds (Current Hand) ---");
		GD.Print($"HandRandomnessSeed: {aiOpponent.HandRandomnessSeed:F3}");
		GD.Print($"BetSizeSeed: {aiOpponent.BetSizeSeed:F3}");
		GD.Print($"PreflopDecisionSeed: {aiOpponent.PreflopDecisionSeed:F3}");
		GD.Print($"FlopDecisionSeed: {aiOpponent.FlopDecisionSeed:F3}");
		GD.Print($"TurnDecisionSeed: {aiOpponent.TurnDecisionSeed:F3}");
		GD.Print($"RiverDecisionSeed: {aiOpponent.RiverDecisionSeed:F3}");
		GD.Print($"TrapDecisionSeed: {aiOpponent.TrapDecisionSeed:F3}");
		GD.Print($"AllInCommitmentSeed: {aiOpponent.AllInCommitmentSeed:F3}");
		
		GD.Print("\n--- Hand Strength (if available) ---");
		if (handInProgress && communityCards.Count >= 3)
		{
			GameState gameState = CreateGameState();
			float handStrength = aiOpponent.EvaluateCurrentHandStrength(gameState);
			GD.Print($"Current Hand Strength: {handStrength:F2}");
		}
		else
		{
			GD.Print("Hand strength unavailable (preflop or no community cards)");
		}
		
		GD.Print("===============================\n");
	}
	
	#endregion
	
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
		
		int winAmount = GetEffectivePot();
		AddPlayerChips(winAmount);
		RefreshAllInFlagsFromStacks();
		
		pot = 0;
		displayPot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		playerContributed = 0;
		opponentContributed = 0;
		
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
		
		int lossAmount = GetEffectivePot();
		AddOpponentChips(lossAmount);
		RefreshAllInFlagsFromStacks();
		
		pot = 0;
		displayPot = 0;
		playerChipsInPot = 0;
		opponentChipsInPot = 0;
		playerContributed = 0;
		opponentContributed = 0;
		
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
		RefreshAllInFlagsFromStacks();
		UpdateHud();
		GD.Print($"[DEBUG] Added {amount} chips to player (Total: {playerChips})");
	}
	
	private void DebugRemoveChips(int amount)
	{
		int removed = Math.Min(amount, playerChips);
		playerChips -= removed;
		RefreshAllInFlagsFromStacks();
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
		GD.Print($"Display Pot: ${displayPot}");
		GD.Print($"Effective Pot: ${GetEffectivePot()}");
		GD.Print($"Current Bet: ${currentBet}");
		GD.Print($"Previous Bet: ${previousBet}");
		GD.Print($"Player: ${playerChips} (Bet: ${playerBet}, InPot: ${playerChipsInPot}, Contributed: ${playerContributed})");
		GD.Print($"Opponent: ${opponentChips} (Bet: ${opponentBet}, InPot: ${opponentChipsInPot}, Contributed: ${opponentContributed})");
		GD.Print($"Player Turn: {isPlayerTurn}");
		GD.Print($"Hand In Progress: {handInProgress}");
		GD.Print($"Player All-In: {playerIsAllIn}");
		GD.Print($"Opponent All-In: {opponentIsAllIn}");
		GD.Print($"Player Has Acted: {playerHasActedThisStreet}");
		GD.Print($"Opponent Has Acted: {opponentHasActedThisStreet}");
		GD.Print($"Is Processing AI: {isProcessingAIAction}");
		GD.Print($"AI Tilt: {aiOpponent.CurrentTiltState} ({aiOpponent.Personality.TiltMeter:F1})");
		GD.Print($"Community Cards: {communityCards.Count}");
		if (communityCards.Count > 0)
		{
			string cardsStr = string.Join(", ", communityCards);
			GD.Print($"Board: {cardsStr}");
		}
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
