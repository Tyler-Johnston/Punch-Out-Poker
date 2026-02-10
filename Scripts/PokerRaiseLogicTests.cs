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
			// Make sure you have this preset, or create a dummy personality
			Personality = PersonalityPresets.CreateSteve() 
		};
		testAI.SetDecisionMaker(decisionMaker);
		AddChild(testAI);
		testAI.ResetForNewHand();
	}

	private void RunAllTests()
	{
		// 1. Test the Pure Math (PokerRules.cs)
		// These ensure the core logic used by the Engine is correct.
		TestRule_RefundLogic();
		TestRule_FullRaiseLogic();
		TestRule_MinRaiseCalculation();

		// 2. Test the AI Integration (PokerDecisionMaker.cs)
		// These ensure the AI asks the Engine (PokerRules) for the correct limits.
		TestAI_RespectsReopenFlag();
		TestAI_CalculatesCorrectMinRaise();
		TestAI_HandlesOpeningBet();
	}

	// ========================================
	// 1. PURE LOGIC TESTS (PokerRules.cs)
	// ========================================

	private void TestRule_RefundLogic()
	{
		// Scenario: Uncalled Bet Refund
		// Opponent is All-in for 80. We bet 100.
		// Diff is -20 (relative to opponent call). Our street bet is 100.
		var result = PokerRules.CalculateRefund(-20, 100);

		AssertEqual("Rules: Refund Amount", result.RefundAmount, 20, "Should refund 20 chips");
		AssertEqual("Rules: Refund Source", result.FromStreet, 20, "Should take from street");

		// Scenario: Multi-Street Refund
		// We have 500 total in. Opponent 120. Diff 380.
		// We only have 200 in current street.
		result = PokerRules.CalculateRefund(-380, 200);

		AssertEqual("Rules: Multi-Street Refund Cap", result.RefundAmount, 200, 
			"Should cap refund at current street bet (standard rule)");
	}

	private void TestRule_FullRaiseLogic()
	{
		// Min raise is 50.
		
		// Case A: Under-raise (30)
		bool isFull = PokerRules.IsFullRaise(30, 50);
		AssertFalse("Rules: Detect Under-Raise", isFull, "30 < 50 is NOT a full raise");

		// Case B: Exact Min-Raise (50)
		isFull = PokerRules.IsFullRaise(50, 50);
		AssertTrue("Rules: Detect Exact Raise", isFull, "50 == 50 IS a full raise");

		// Case C: Over-Raise (100)
		isFull = PokerRules.IsFullRaise(100, 50);
		AssertTrue("Rules: Detect Over-Raise", isFull, "100 > 50 IS a full raise");
	}

	private void TestRule_MinRaiseCalculation()
	{
		// Current Bet 100. Last Full Raise was 50.
		// Min Total should be 150.
		int minTotal = PokerRules.CalculateMinRaiseTotal(100, 50, 50, 10);
		AssertEqual("Rules: Calc Min Raise", minTotal, 150, "100 + 50 = 150");

		// Opening Bet (Current 0). BB is 10.
		minTotal = PokerRules.CalculateMinRaiseTotal(0, 0, 0, 10);
		AssertEqual("Rules: Calc Opening Bet", minTotal, 10, "Opening bet min is BB");
	}

	// ========================================
	// 2. AI INTEGRATION TESTS
	// ========================================

	private void TestAI_RespectsReopenFlag()
	{
		var gameState = new GameState();
		// Setup: River, Pot 200, Bet 100. 
		// CRITICAL: canAIReopen = false (Under-raise happened previously)
		gameState.SetupTestScenario(100, 100, 200, Street.River, 50, 10, canAIReopen: false);

		// Give AI the "Nuts" (Strength 1.0) -> It definitely wants to raise
		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 1.0f);

		// Since Reopen is FALSE, it must return CurrentBet (Call), not Raise.
		AssertEqual("AI: Respect Reopen Flag", raiseTo, 100, 
			"AI should return CurrentBet (Call) if betting is closed");
	}

	private void TestAI_CalculatesCorrectMinRaise()
	{
		var gameState = new GameState();
		// Setup: Flop, Bet 100, Last Raise 50. Min Legal Raise is 150.
		gameState.SetupTestScenario(100, 50, 200, Street.Flop, 50, 10, canAIReopen: true);

		// Force AI to want a small bet (0 size seed)
		testAI.ForceBetSizeSeedForTesting(0.0f);

		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.9f);

		// Even though AI wants small bet, logic must clamp it to 150.
		AssertGreaterOrEqual("AI: Enforce Min Raise", raiseTo, 150, 
			"AI raise must be >= MinRaise (150)");
	}

	private void TestAI_HandlesOpeningBet()
	{
		var gameState = new GameState();
		// Setup: Preflop, CurrentBet 0. BB 10.
		gameState.SetupTestScenario(0, 0, 0, Street.Preflop, 0, 10, canAIReopen: true);

		int raiseTo = decisionMaker.CalculateRaiseToTotal(testAI, gameState, 0.5f);

		AssertGreaterOrEqual("AI: Opening Bet Size", raiseTo, 10, 
			"Opening bet must be at least BB");
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
