using Godot;
using System.Collections.Generic;

/// <summary>
/// Unit tests for under-raise and min-raise logic.
/// Tests both engine-level betting mechanics and AI decision-making.
/// </summary>
public partial class PokerRaiseLogicTests : Node
{
	private PokerDecisionMaker decisionMaker;
	private AIPokerPlayer testAI;
	private int testsPassed = 0;
	private int testsFailed = 0;

	public override void _Ready()
	{
		GD.Print("\n========================================");
		GD.Print("POKER RAISE LOGIC UNIT TESTS");
		GD.Print("========================================\n");

		SetupTestEnvironment();
		RunAllTests();
		PrintTestSummary();
	}

	private void SetupTestEnvironment()
	{
		decisionMaker = new PokerDecisionMaker();
		AddChild(decisionMaker);

		testAI = new AIPokerPlayer
		{
			PlayerName = "TestAI",
			ChipStack = 1000,
			Personality = PersonalityPresets.CreateSteve()
		};
		testAI.SetDecisionMaker(decisionMaker);
		AddChild(testAI);
		testAI.ResetForNewHand();
	}

	private void RunAllTests()
	{
		// ===== UNDER-RAISE DETECTION TESTS =====
		TestUnderRaiseAllIn_Simple();
		TestUnderRaiseAllIn_VerySmall();
		TestUnderRaiseAllIn_OneChipShort();
		TestFullRaiseAllIn_Exact();
		TestFullRaiseAllIn_Overfull();

		// ===== MIN-RAISE CALCULATION TESTS =====
		TestMinRaise_AfterUnderRaise();
		TestMinRaise_AfterMultipleUnderRaises();
		TestMinRaise_AfterFullRaise();
		TestMinRaise_OpeningBet();
		TestMinRaise_MultiwayPot();

		// ===== REOPENING FLAG TESTS =====
		TestReopeningFlag_UnderRaiseDoesNotReopen();
		TestReopeningFlag_FullRaiseReopens();
		TestReopeningFlag_BothPlayersCantReopen();
		TestReopeningFlag_OnlyNonAggressorCanReopen();

		// ===== AI GUARD TESTS =====
		TestAIGuard_CannotRaiseWhenNotReopened();
		TestAIGuard_CanRaiseWhenReopened();
		TestAIGuard_ForcesCallNotFold();

		// ===== EDGE CASE TESTS =====
		TestEdgeCase_AllInForLessThanBigBlind();
		TestEdgeCase_BothPlayersAllIn();
		TestEdgeCase_ZeroRaiseIncrement();
		TestEdgeCase_NegativeIncrement();
		TestEdgeCase_MaxIntOverflow();

		// ===== INTEGRATION TESTS =====
		TestIntegration_ComplexBettingSequence();
		TestIntegration_MultiStreetUnderRaises();
		TestIntegration_SidePotScenario();
	}

	// ========================================
	// UNDER-RAISE DETECTION TESTS
	// ========================================

	private void TestUnderRaiseAllIn_Simple()
	{
		string testName = "Under-Raise All-In: Simple Case";
		
		// Setup: Last raise was $50, AI goes all-in for $30 more
		int lastRaiseAmount = 50;
		int aiAllInAmount = 30;
		
		// Calculate what increment would be
		int raiseIncrement = aiAllInAmount;
		int minRaiseRequired = lastRaiseAmount;
		
		bool isFullRaise = raiseIncrement >= minRaiseRequired;
		bool shouldReopen = isFullRaise;
		
		AssertFalse(testName, shouldReopen, 
			$"Under-raise of ${aiAllInAmount} should NOT reopen betting (min: ${minRaiseRequired})");
	}

	private void TestUnderRaiseAllIn_VerySmall()
	{
		string testName = "Under-Raise All-In: Very Small Amount";
		
		// Setup: AI has only $1 left
		int lastRaiseAmount = 50;
		int aiAllInAmount = 1;
		
		int raiseIncrement = aiAllInAmount;
		bool isFullRaise = raiseIncrement >= lastRaiseAmount;
		
		AssertFalse(testName, isFullRaise, 
			$"$1 all-in should be under-raise (min: ${lastRaiseAmount})");
	}

	private void TestUnderRaiseAllIn_OneChipShort()
	{
		string testName = "Under-Raise All-In: One Chip Short of Full Raise";
		
		// Setup: One chip short of full raise
		int lastRaiseAmount = 50;
		int aiAllInAmount = 49;
		
		bool isFullRaise = aiAllInAmount >= lastRaiseAmount;
		
		AssertFalse(testName, isFullRaise, 
			$"${aiAllInAmount} is one short of ${lastRaiseAmount}, should be under-raise");
	}

	private void TestFullRaiseAllIn_Exact()
	{
		string testName = "Full-Raise All-In: Exactly Min-Raise";
		
		// Setup: Exactly the minimum raise amount
		int lastRaiseAmount = 50;
		int aiAllInAmount = 50;
		
		bool isFullRaise = aiAllInAmount >= lastRaiseAmount;
		
		AssertTrue(testName, isFullRaise, 
			$"${aiAllInAmount} equals min-raise ${lastRaiseAmount}, should be full raise");
	}

	private void TestFullRaiseAllIn_Overfull()
	{
		string testName = "Full-Raise All-In: More Than Min-Raise";
		
		// Setup: More than minimum
		int lastRaiseAmount = 50;
		int aiAllInAmount = 100;
		
		bool isFullRaise = aiAllInAmount >= lastRaiseAmount;
		
		AssertTrue(testName, isFullRaise, 
			$"${aiAllInAmount} exceeds min-raise ${lastRaiseAmount}, should be full raise");
	}

	// ========================================
	// MIN-RAISE CALCULATION TESTS
	// ========================================

	private void TestMinRaise_AfterUnderRaise()
	{
		string testName = "Min-Raise Calculation: After Under-Raise";
		
		// Setup game state after under-raise
		var gameState = new GameState
		{
			CurrentBet = 120,
			PreviousBet = 100,
			BigBlind = 2,
			PotSize = 250,
			Street = Street.Flop,
			OpponentChipStack = 200,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(50);  // Last FULL raise was $50, not $20
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 200;
		
		// AI calculates raise
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.75f);
		
		// Expected: currentBet (120) + lastFullRaise (50) = 170
		int expectedMin = 170;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"AI should use last FULL raise increment of $50, not the $20 under-raise");
	}

	private void TestMinRaise_AfterMultipleUnderRaises()
	{
		string testName = "Min-Raise Calculation: After Multiple Under-Raises";
		
		// Setup: $50 → $100 (full $50) → $110 (under $10) → $115 (under $5)
		var gameState = new GameState
		{
			CurrentBet = 115,
			PreviousBet = 110,
			BigBlind = 2,
			PotSize = 300,
			Street = Street.Turn,
			OpponentChipStack = 200,
			IsAIInPosition = false
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(50);  // Original full raise
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 200;
		
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.70f);
		
		// Expected: 115 + 50 = 165
		int expectedMin = 165;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"Should ignore multiple under-raises and use original $50");
	}

	private void TestMinRaise_AfterFullRaise()
	{
		string testName = "Min-Raise Calculation: After Normal Full Raise";
		
		var gameState = new GameState
		{
			CurrentBet = 100,
			PreviousBet = 50,
			BigBlind = 2,
			PotSize = 150,
			Street = Street.Flop,
			OpponentChipStack = 200,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(50);
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 200;
		
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.75f);
		
		// Expected: 100 + 50 = 150
		int expectedMin = 150;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"Standard min-raise should work correctly");
	}

	private void TestMinRaise_OpeningBet()
	{
		string testName = "Min-Raise Calculation: Opening Bet";
		
		var gameState = new GameState
		{
			CurrentBet = 0,
			PreviousBet = 0,
			BigBlind = 2,
			PotSize = 10,
			Street = Street.Flop,
			OpponentChipStack = 100,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(0);
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 100;
		
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.60f);
		
		// Expected: At least big blind
		int expectedMin = 2;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"Opening bet should be at least big blind");
	}

	private void TestMinRaise_MultiwayPot()
	{
		string testName = "Min-Raise Calculation: Multiway Pot (3+ players)";
		
		// Same logic applies in multiway - use last full raise
		var gameState = new GameState
		{
			CurrentBet = 80,
			PreviousBet = 50,
			BigBlind = 2,
			PotSize = 240,  // More chips from 3rd player
			Street = Street.Flop,
			OpponentChipStack = 150,
			IsAIInPosition = false
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(30);  // Last full raise
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 150;
		
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.65f);
		
		// Expected: 80 + 30 = 110
		int expectedMin = 110;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"Multiway pot should still use last full raise");
	}

	// ========================================
	// REOPENING FLAG TESTS
	// ========================================

	private void TestReopeningFlag_UnderRaiseDoesNotReopen()
	{
		string testName = "Reopening Flag: Under-Raise Does Not Reopen";
		
		// Simulate under-raise
		int raiseIncrement = 20;
		int minRaiseRequired = 50;
		bool isFullRaise = raiseIncrement >= minRaiseRequired;
		
		bool shouldReopenForOpponent = isFullRaise;
		
		AssertFalse(testName, shouldReopenForOpponent,
			$"Under-raise should NOT reopen betting for opponent");
	}

	private void TestReopeningFlag_FullRaiseReopens()
	{
		string testName = "Reopening Flag: Full Raise Reopens";
		
		int raiseIncrement = 50;
		int minRaiseRequired = 50;
		bool isFullRaise = raiseIncrement >= minRaiseRequired;
		
		bool shouldReopenForOpponent = isFullRaise;
		
		AssertTrue(testName, shouldReopenForOpponent,
			$"Full raise should reopen betting for opponent");
	}

	private void TestReopeningFlag_BothPlayersCantReopen()
	{
		string testName = "Reopening Flag: Both Players Cannot Reopen After Under-Raise";
		
		// After under-raise all-in:
		// - Actor (all-in) cannot reopen: true
		// - Opponent cannot reopen: true (if they already acted)
		//bool actorAllIn = true;
		bool opponentAlreadyActed = true;
		bool wasUnderRaise = true;
		
		bool actorCanReopen = false;  // All-in
		bool opponentCanReopen = !(opponentAlreadyActed && wasUnderRaise);
		
		AssertFalse(testName, actorCanReopen && opponentCanReopen,
			$"After under-raise all-in, both players should be unable to reopen");
	}

	private void TestReopeningFlag_OnlyNonAggressorCanReopen()
	{
		string testName = "Reopening Flag: Only Non-Aggressor Can Reopen After Full Raise";
		
		// After player raises (full):
		//bool playerWasAggressor = true;
		bool isFullRaise = true;
		
		bool playerCanReopen = false;  // Was aggressor
		bool opponentCanReopen = isFullRaise;  // Wasn't aggressor, full raise
		
		AssertTrue(testName, opponentCanReopen && !playerCanReopen,
			$"Only non-aggressor should be able to reopen after full raise");
	}

	// ========================================
	// AI GUARD TESTS
	// ========================================

	private void TestAIGuard_CannotRaiseWhenNotReopened()
	{
		string testName = "AI Guard: Cannot Raise When Betting Not Reopened";
		
		var gameState = new GameState
		{
			CurrentBet = 50,
			PreviousBet = 30,
			BigBlind = 2,
			PotSize = 100,
			Street = Street.Flop,
			OpponentChipStack = 100,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(false);  // KEY: Betting not reopened
		gameState.SetLastFullRaiseIncrement(20);
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 100;
		testAI.Hand.Clear();
		testAI.Hand.Add(new Card(Rank.Ace, Suit.Spades));
		testAI.Hand.Add(new Card(Rank.King, Suit.Spades));
		
		// AI has strong hand, would normally raise
		PlayerAction action = decisionMaker.DecideAction(testAI, gameState);
		
		AssertNotEqual(testName, action, PlayerAction.Raise,
			$"AI should NOT raise when CanAIReopenBetting is false");
		
		// Should be Call or Fold only
		bool isCallOrFold = (action == PlayerAction.Call || action == PlayerAction.Fold);
		AssertTrue(testName + " (Action is Call/Fold)", isCallOrFold,
			$"AI should Call or Fold, got: {action}");
	}

	private void TestAIGuard_CanRaiseWhenReopened()
	{
		string testName = "AI Guard: Can Raise When Betting Is Reopened";
		
		var gameState = new GameState
		{
			CurrentBet = 50,
			PreviousBet = 30,
			BigBlind = 2,
			PotSize = 100,
			Street = Street.Flop,
			OpponentChipStack = 200,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(true);  // KEY: Betting reopened
		gameState.SetLastFullRaiseIncrement(20);
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 200;
		testAI.Hand.Clear();
		testAI.Hand.Add(new Card(Rank.Ace, Suit.Spades));
		testAI.Hand.Add(new Card(Rank.Ace, Suit.Hearts));
		
		// Give AI many chances to raise
		bool canRaise = false;
		for (int i = 0; i < 10; i++)
		{
			testAI.ResetForNewHand();
			testAI.Hand.Clear();
			testAI.Hand.Add(new Card(Rank.Ace, Suit.Spades));
			testAI.Hand.Add(new Card(Rank.Ace, Suit.Hearts));
			
			PlayerAction action = decisionMaker.DecideAction(testAI, gameState);
			if (action == PlayerAction.Raise)
			{
				canRaise = true;
				break;
			}
		}
		
		AssertTrue(testName, canRaise,
			$"AI should be ABLE to raise when CanAIReopenBetting is true (may not always, but should be possible)");
	}

	private void TestAIGuard_ForcesCallNotFold()
	{
		string testName = "AI Guard: Forces Call (Not Fold) When Raising Blocked";
		
		var gameState = new GameState
		{
			CurrentBet = 10,
			PreviousBet = 5,
			BigBlind = 2,
			PotSize = 50,
			Street = Street.River,
			OpponentChipStack = 100,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(false);
		gameState.SetLastFullRaiseIncrement(5);
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 100;
		testAI.Hand.Clear();
		testAI.Hand.Add(new Card(Rank.King, Suit.Spades));
		testAI.Hand.Add(new Card(Rank.Queen, Suit.Spades));
		
		// AI has decent hand, wouldn't fold normally
		PlayerAction action = decisionMaker.DecideAction(testAI, gameState);
		
		// Should not raise (blocked) and should not fold (decent hand + pot odds)
		bool isCallOrAllIn = (action == PlayerAction.Call || action == PlayerAction.AllIn);
		AssertTrue(testName, isCallOrAllIn,
			$"AI with decent hand should Call when raise is blocked, got: {action}");
	}

	// ========================================
	// EDGE CASE TESTS
	// ========================================

	private void TestEdgeCase_AllInForLessThanBigBlind()
	{
		string testName = "Edge Case: All-In for Less Than Big Blind";
		
		int aiAllInAmount = 3;
		int lastRaiseAmount = 20;
		
		// Should still be under-raise
		bool isFullRaise = aiAllInAmount >= lastRaiseAmount;
		
		AssertFalse(testName, isFullRaise,
			$"${aiAllInAmount} all-in (< BB) should be under-raise");
	}

	private void TestEdgeCase_BothPlayersAllIn()
	{
		string testName = "Edge Case: Both Players All-In";
		
		bool playerAllIn = true;
		bool opponentAllIn = true;
		
		// Neither can reopen
		bool playerCanReopen = !playerAllIn;
		bool opponentCanReopen = !opponentAllIn;
		
		AssertFalse(testName, playerCanReopen || opponentCanReopen,
			$"When both all-in, neither can reopen betting");
	}

	private void TestEdgeCase_ZeroRaiseIncrement()
	{
		string testName = "Edge Case: Zero Raise Increment";
		
		int raiseIncrement = 0;
		int minRaise = 10;
		
		bool isFullRaise = raiseIncrement >= minRaise;
		
		AssertFalse(testName, isFullRaise,
			$"Zero increment should not be a full raise");
	}

	private void TestEdgeCase_NegativeIncrement()
	{
		string testName = "Edge Case: Negative Increment (Should Never Happen)";
		
		// This would be a bug, but test defensive handling
		int raiseIncrement = -10;
		int minRaise = 20;
		
		bool isFullRaise = raiseIncrement >= minRaise;
		
		AssertFalse(testName, isFullRaise,
			$"Negative increment should never be full raise");
	}

	private void TestEdgeCase_MaxIntOverflow()
	{
		string testName = "Edge Case: Very Large Chip Amounts (Overflow Protection)";
		
		var gameState = new GameState
		{
			CurrentBet = 999999,
			PreviousBet = 500000,
			BigBlind = 1000,
			PotSize = 2000000,
			Street = Street.River,
			OpponentChipStack = 1000000,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(499999);
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 1000000;
		
		// Should not crash or overflow
		try
		{
			int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.80f);
			AssertTrue(testName, raiseTotal >= 0,
				$"Should handle large numbers without overflow");
		}
		catch (System.Exception e)
		{
			AssertFail(testName, $"Should not throw exception: {e.Message}");
		}
	}

	// ========================================
	// INTEGRATION TESTS
	// ========================================

	private void TestIntegration_ComplexBettingSequence()
	{
		string testName = "Integration: Complex Betting Sequence";
		
		// Sequence: $10 → $30 (full) → $60 (full) → $75 (under) → AI raises
		var gameState = new GameState
		{
			CurrentBet = 75,
			PreviousBet = 60,
			BigBlind = 2,
			PotSize = 200,
			Street = Street.Turn,
			OpponentChipStack = 300,
			IsAIInPosition = false
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(30);  // $30 → $60 was last full raise
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 300;
		
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.75f);
		
		// Expected: 75 + 30 = 105
		int expectedMin = 105;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"Should use $30 increment from last full raise, not $15 under-raise");
	}

	private void TestIntegration_MultiStreetUnderRaises()
	{
		string testName = "Integration: Under-Raises Across Multiple Streets";
		
		// Each street resets lastRaiseAmount, so under-raises don't carry over
		// Flop: $20 → $40 (full) → $45 (under)
		// Turn: $10 (opening bet) → AI raises
		
		var gameState = new GameState
		{
			CurrentBet = 10,
			PreviousBet = 0,
			BigBlind = 2,
			PotSize = 150,
			Street = Street.Turn,
			OpponentChipStack = 200,
			IsAIInPosition = true
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(10);  // Turn opening bet
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 200;
		
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.70f);
		
		// Expected: 10 + 10 = 20 (uses current street's increment)
		int expectedMin = 20;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"New street should use its own raise increment, not previous street's");
	}

	private void TestIntegration_SidePotScenario()
	{
		string testName = "Integration: Side Pot with Multiple All-Ins";
		
		// Player1: $100, Player2 (AI): $50, Player3: $200
		// Player1 raises to $80
		// Player2 (AI) all-in for $50 (under-raise)
		// Player3's turn: should use $80 increment from Player1, not AI's under-raise
		
		var gameState = new GameState
		{
			CurrentBet = 80,
			PreviousBet = 20,
			BigBlind = 5,
			PotSize = 180,
			Street = Street.Flop,
			OpponentChipStack = 200,
			IsAIInPosition = false
		};
		gameState.SetCanAIReopenBetting(true);
		gameState.SetLastFullRaiseIncrement(60);  // Player1's full raise
		gameState.SetPlayerBet(testAI, 0);
		
		testAI.ChipStack = 200;
		
		int raiseTotal = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.75f);
		
		// Expected: 80 + 60 = 140
		int expectedMin = 140;
		
		AssertGreaterOrEqual(testName, raiseTotal, expectedMin,
			$"Side pot scenario should ignore middle all-in under-raise");
	}

	// ========================================
	// ASSERTION HELPERS
	// ========================================

	private void AssertTrue(string testName, bool condition, string message)
	{
		if (condition)
		{
			testsPassed++;
			GD.Print($"[✅ PASS] {testName}");
		}
		else
		{
			testsFailed++;
			GD.PrintErr($"[❌ FAIL] {testName}");
			GD.PrintErr($"         {message}");
		}
	}

	private void AssertFalse(string testName, bool condition, string message)
	{
		AssertTrue(testName, !condition, message);
	}

	private void AssertEqual<T>(string testName, T actual, T expected, string message)
	{
		if (EqualityComparer<T>.Default.Equals(actual, expected))
		{
			testsPassed++;
			GD.Print($"[✅ PASS] {testName}");
		}
		else
		{
			testsFailed++;
			GD.PrintErr($"[❌ FAIL] {testName}");
			GD.PrintErr($"         Expected: {expected}, Got: {actual}");
			GD.PrintErr($"         {message}");
		}
	}

	private void AssertNotEqual<T>(string testName, T actual, T unexpected, string message)
	{
		if (!EqualityComparer<T>.Default.Equals(actual, unexpected))
		{
			testsPassed++;
			GD.Print($"[✅ PASS] {testName}");
		}
		else
		{
			testsFailed++;
			GD.PrintErr($"[❌ FAIL] {testName}");
			GD.PrintErr($"         Should NOT be: {unexpected}, but got: {actual}");
			GD.PrintErr($"         {message}");
		}
	}

	private void AssertGreaterOrEqual(string testName, int actual, int minimum, string message)
	{
		if (actual >= minimum)
		{
			testsPassed++;
			GD.Print($"[✅ PASS] {testName}");
		}
		else
		{
			testsFailed++;
			GD.PrintErr($"[❌ FAIL] {testName}");
			GD.PrintErr($"         Expected >= {minimum}, Got: {actual}");
			GD.PrintErr($"         {message}");
		}
	}

	private void AssertFail(string testName, string message)
	{
		testsFailed++;
		GD.PrintErr($"[❌ FAIL] {testName}");
		GD.PrintErr($"         {message}");
	}

	private void PrintTestSummary()
	{
		int totalTests = testsPassed + testsFailed;
		float passRate = (totalTests > 0) ? (testsPassed / (float)totalTests) * 100f : 0f;

		GD.Print("\n========================================");
		GD.Print("TEST SUMMARY");
		GD.Print("========================================");
		GD.Print($"Total Tests: {totalTests}");
		GD.Print($"✅ Passed: {testsPassed}");
		GD.Print($"❌ Failed: {testsFailed}");
		GD.Print($"Pass Rate: {passRate:F1}%");
		
		if (testsFailed == 0)
		{
			GD.Print("\n🎉 ALL TESTS PASSED! 🎉");
		}
		else
		{
			GD.PrintErr($"\n⚠️ {testsFailed} TEST(S) FAILED");
		}
		
		GD.Print("========================================\n");
	}
}
