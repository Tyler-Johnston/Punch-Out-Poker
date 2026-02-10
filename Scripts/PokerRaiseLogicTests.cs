using Godot;
using System.Collections.Generic;

public partial class PokerRaiseLogicTests : Node
{
	private PokerDecisionMaker decisionMaker;
	private AIPokerPlayer testAI;
	private int testsPassed = 0;
	private int testsFailed = 0;

	public override void _Ready()
	{
		GD.Print("\n========================================");
		GD.Print("POKER ENGINE INTEGRATION TESTS");
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
		// 1. PURE MATH TESTS
		TestRule_RefundLogic();
		TestRule_FullRaiseLogic();
		TestRule_MinRaiseCalculation();

		// 2. AI INTEGRATION TESTS
		TestAI_RespectsReopenFlag();
		TestAI_CalculatesCorrectMinRaise();
		TestAI_HandlesOpeningBet();
		
		// 3. NEW TESTS (Edge Cases)
		TestAI_CapsAtStackSize();
		TestAI_HandlesShortStackMinRaise();
	}

	// ========================================
	// 1. PURE LOGIC TESTS (PokerRules.cs)
	// ========================================

	private void TestRule_RefundLogic()
	{
		var result = PokerRules.CalculateRefund(-20, 100);
		AssertEqual("Rules: Refund Amount", result.RefundAmount, 20, "Should refund 20 chips");
		AssertEqual("Rules: Refund Source", result.FromStreet, 20, "Should take from street");

		result = PokerRules.CalculateRefund(-380, 200);
		AssertEqual("Rules: Multi-Street Refund Cap", result.RefundAmount, 200, 
			"Should cap refund at current street bet");
	}

	private void TestRule_FullRaiseLogic()
	{
		bool isFull = PokerRules.IsFullRaise(30, 50);
		AssertFalse("Rules: Detect Under-Raise", isFull, "30 < 50 is NOT a full raise");

		isFull = PokerRules.IsFullRaise(50, 50);
		AssertTrue("Rules: Detect Exact Raise", isFull, "50 == 50 IS a full raise");

		isFull = PokerRules.IsFullRaise(100, 50);
		AssertTrue("Rules: Detect Over-Raise", isFull, "100 > 50 IS a full raise");
	}

	private void TestRule_MinRaiseCalculation()
	{
		int minTotal = PokerRules.CalculateMinRaiseTotal(100, 50, 50, 10);
		AssertEqual("Rules: Calc Min Raise", minTotal, 150, "100 + 50 = 150");

		minTotal = PokerRules.CalculateMinRaiseTotal(0, 0, 0, 10);
		AssertEqual("Rules: Calc Opening Bet", minTotal, 10, "Opening bet min is BB");
	}

	// ========================================
	// 2. AI INTEGRATION TESTS
	// ========================================

	private void TestAI_RespectsReopenFlag()
	{
		var gameState = new GameState();
		gameState.SetupTestScenario(100, 100, 200, Street.River, 50, 10, canAIReopen: false);

		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 1.0f);
		AssertEqual("AI: Respect Reopen Flag", raiseTo, 100, "Should return CurrentBet (Call) if closed");
	}

	private void TestAI_CalculatesCorrectMinRaise()
	{
		var gameState = new GameState();
		gameState.SetupTestScenario(100, 50, 200, Street.Flop, 50, 10, canAIReopen: true);
		testAI.ForceBetSizeSeedForTesting(0.0f); // Wants small bet

		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.9f);
		AssertGreaterOrEqual("AI: Enforce Min Raise", raiseTo, 150, "Must be >= 150");
	}

	private void TestAI_HandlesOpeningBet()
	{
		var gameState = new GameState();
		gameState.SetupTestScenario(0, 0, 0, Street.Preflop, 0, 10, canAIReopen: true);

		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.5f);
		AssertGreaterOrEqual("AI: Opening Bet Size", raiseTo, 10, "Must be >= BB");
	}

	// ========================================
	// 3. NEW EDGE CASE TESTS
	// ========================================

	private void TestAI_CapsAtStackSize()
	{
		var gameState = new GameState();
		// Pot is HUGE (10,000). AI has 1000 chips.
		// AI wants to bet Pot Size (10,000), but only has 1000.
		gameState.SetupTestScenario(0, 0, 10000, Street.Flop, 0, 10, canAIReopen: true);
		
		// Reset AI stack to known amount
		testAI.ChipStack = 1000;
		gameState.SetPlayerBet(testAI, 0); // Currently bet 0

		// Force HUGE bet desire
		testAI.ForceBetSizeSeedForTesting(1.0f); 

		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 1.0f);

		// Should be exactly 1000 (All-In), not 10,000.
		AssertEqual("AI: Cap at Stack", raiseTo, 1000, 
			"Raise should never exceed player's chip stack");
	}

	private void TestAI_HandlesShortStackMinRaise()
	{
		var gameState = new GameState();
		// Current Bet 100. Min Raise is 150.
		// AI only has 120 chips total (including current bet).
		// Wait... if AI has 0 bet currently, and stack is 120.
		// Max legal bet is 120 (All-In).
		// Min 'Full' Raise is 150.
		
		gameState.SetupTestScenario(100, 50, 500, Street.Turn, 50, 10, canAIReopen: true);
		
		testAI.ChipStack = 120;
		gameState.SetPlayerBet(testAI, 0);

		// AI wants to raise big
		testAI.ForceBetSizeSeedForTesting(1.0f);

		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.95f);

		// Logic dictates: 
		// Min is 150. Max is 120.
		// Clamp(Target, Min, Max) logic in DecisionMaker handles this:
		// if (Max < Min) return Max;
		
		AssertEqual("AI: Short Stack All-In", raiseTo, 120, 
			"If Stack < MinRaise, AI should return Stack (All-In)");
	}

	// ========================================
	// HELPERS
	// ========================================

	private void AssertEqual<T>(string name, T actual, T expected, string msg) 
	{
		if(EqualityComparer<T>.Default.Equals(actual, expected)) 
			{ testsPassed++; GD.Print($"[✅ PASS] {name}"); }
		else 
			{ testsFailed++; GD.PrintErr($"[❌ FAIL] {name}: Expected {expected}, Got {actual}. {msg}"); }
	}

	private void AssertTrue(string name, bool condition, string msg) => AssertEqual(name, condition, true, msg);
	private void AssertFalse(string name, bool condition, string msg) => AssertEqual(name, condition, false, msg);
	private void AssertGreaterOrEqual(string name, int actual, int min, string msg) 
	{
		if(actual >= min) { testsPassed++; GD.Print($"[✅ PASS] {name}"); }
		else { testsFailed++; GD.PrintErr($"[❌ FAIL] {name}: Got {actual}, Expected >= {min}. {msg}"); }
	}

	private void PrintTestSummary()
	{
		GD.Print("\n----------------------------------------");
		GD.Print($"TESTS: {testsPassed + testsFailed} | PASS: {testsPassed} | FAIL: {testsFailed}");
		GD.Print("----------------------------------------");
	}
}
