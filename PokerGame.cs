using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame : Node2D
{

	private PackedScene cardVisualScene;
	private Deck deck;
	private List<Card> playerHand = new List<Card>();
	private List<Card> opponentHand = new List<Card>();
	private List<Card> communityCards = new List<Card>();

	private CardVisual playerCard1;
	private CardVisual playerCard2;
	private CardVisual opponentCard1;
	private CardVisual opponentCard2;
	private CardVisual flop1;
	private CardVisual flop2;
	private CardVisual flop3;
	private CardVisual turnCard;
	private CardVisual riverCard;

	private Button foldButton;
	private Button checkCallButton;
	private Button betRaiseButton;

	private int playerChips = 1000;
	private int opponentChips = 1000;
	private int pot = 0;
	private int betAmount = 20;
	private int currentBet = 0;
	private int playerBet = 0;
	private int opponentBet = 0;
	private int smallBlind = 5;
	private int bigBlind = 10;

	private Label playerStackLabel;
	private Label opponentStackLabel;
	private Label potLabel;
	private Label gameStateLabel;
	private Label playerHandType;
	private Label opponentHandType;

	private Street currentStreet = Street.Preflop;
	private bool handInProgress = false;
	private bool waitingForNextGame = false;
	private bool isPlayerTurn = true;
	private bool playerIsAllIn = false;
	private bool opponentIsAllIn = false;

	// Action tracking to prevent infinite loops
	private bool playerHasActedThisStreet = false;
	private bool opponentHasActedThisStreet = false;

	// AI System Variables
	private OpponentProfile currentOpponent;
	private int selectedOpponentIndex = 0; // Set by opponent selection screen
	private bool aiBluffedThisHand = false;
	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();
	private int playerTotalBetsThisHand = 0;
	private int raisesThisStreet = 0;
	private const int MAX_RAISES_PER_STREET = 4;

	// Define Circuit A opponents with distinct personalities

	public override void _Ready()
	{
		GD.Print("=== Poker Game Started ===");
		Control hudControl = GetNode<Control>("CanvasLayer/Control");
		cardVisualScene = GD.Load<PackedScene>("res://CardVisual.tscn");

		Node2D opponentArea = GetNode<Node2D>("OpponentArea");
		Node2D communityCardsArea = GetNode<Node2D>("CommunityCardsArea");
		Node2D playerArea = GetNode<Node2D>("PlayerArea");

		playerCard1 = playerArea.GetNode<CardVisual>("PlayerCard1");
		playerCard2 = playerArea.GetNode<CardVisual>("PlayerCard2");
		opponentCard1 = opponentArea.GetNode<CardVisual>("OpponentCard1");
		opponentCard2 = opponentArea.GetNode<CardVisual>("OpponentCard2");

		flop1 = communityCardsArea.GetNode<CardVisual>("Flop1");
		flop2 = communityCardsArea.GetNode<CardVisual>("Flop2");
		flop3 = communityCardsArea.GetNode<CardVisual>("Flop3");
		turnCard = communityCardsArea.GetNode<CardVisual>("Turn");
		riverCard = communityCardsArea.GetNode<CardVisual>("River");

		Control actionButtons = hudControl.GetNode<Control>("ActionButtons");
		foldButton = actionButtons.GetNode<Button>("FoldButton");
		checkCallButton = actionButtons.GetNode<Button>("CheckCallButton");
		betRaiseButton = actionButtons.GetNode<Button>("BetRaiseButton");

		playerStackLabel = hudControl.GetNode<Label>("PlayerStackLabel");
		opponentStackLabel = hudControl.GetNode<Label>("OpponentStackLabel");
		potLabel = hudControl.GetNode<Label>("PotLabel");
		gameStateLabel = hudControl.GetNode<Label>("GameStateLabel");
		playerHandType = hudControl.GetNode<Label>("PlayerHandType");
		opponentHandType = hudControl.GetNode<Label>("OpponentHandType");

		foldButton.Pressed += OnFoldPressed;
		checkCallButton.Pressed += OnCheckCallPressed;
		betRaiseButton.Pressed += OnBetRaisePressed;

		OpponentProfile[] circuitAOpponents = OpponentProfiles.CircuitAOpponents();
		currentOpponent = circuitAOpponents[selectedOpponentIndex];
		GD.Print($"=== Opponent: {currentOpponent.Name} ===");
		GD.Print($"Aggression: {currentOpponent.Aggression:F2}");
		GD.Print($"Looseness: {currentOpponent.Looseness:F2}");
		GD.Print($"Bluffiness: {currentOpponent.Bluffiness:F2}");

		UpdateHud();
		StartNewHand();
	}

	private void StartNewHand()
	{
		// Check if either player is out of chips
		if (playerChips < smallBlind)
		{
			ShowMessage("GAME OVER - You ran out of chips!");
			GD.Print("GAME OVER - Player has no chips");
			waitingForNextGame = true;
			handInProgress = false;
			UpdateHud();
			return;
		}

		if (opponentChips < bigBlind)
		{
			ShowMessage("YOU WIN - Opponent ran out of chips!");
			GD.Print("GAME OVER - Opponent has no chips");
			waitingForNextGame = true;
			handInProgress = false;
			UpdateHud();
			return;
		}

		GD.Print("\n=== New Hand ===");
		ShowMessage("New hand starting...");

		foldButton.Visible = true;
		betRaiseButton.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";

		deck = new Deck();
		deck.Shuffle();

		pot = 0;
		currentBet = bigBlind;
		playerBet = smallBlind;
		opponentBet = bigBlind;
		aiBluffedThisHand = false;
		playerTotalBetsThisHand = 0;
		raisesThisStreet = 0;
		playerIsAllIn = false;
		opponentIsAllIn = false;

		// Reset action tracking
		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;

		playerBetOnStreet.Clear();
		playerBetSizeOnStreet.Clear();
		playerBetOnStreet[Street.Preflop] = false;
		playerBetOnStreet[Street.Flop] = false;
		playerBetOnStreet[Street.Turn] = false;
		playerBetOnStreet[Street.River] = false;

		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

		playerCard1.ShowBack();
		playerCard2.ShowBack();
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
		flop1.ShowBack();
		flop2.ShowBack();
		flop3.ShowBack();
		turnCard.ShowBack();
		riverCard.ShowBack();

		DealInitialHands();
		currentStreet = Street.Preflop;
		handInProgress = true;
		waitingForNextGame = false;

		// Player is small blind, opponent is big blind
		playerChips -= smallBlind;
		opponentChips -= bigBlind;
		pot += smallBlind + bigBlind;

		ShowMessage($"Blinds posted: You {smallBlind}, Opponent {bigBlind}");
		GD.Print($"Blinds posted: Player {smallBlind}, Opponent {bigBlind}, Pot: {pot}");

		// Player acts first preflop (small blind position)
		isPlayerTurn = true;
		UpdateHud();
		UpdateButtonLabels();
	}

	private void DealInitialHands()
	{
		GD.Print("\n=== Dealing Initial Hands ===");
		playerHand.Clear();
		opponentHand.Clear();
		communityCards.Clear();

		playerHand.Add(deck.Deal());
		playerHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());
		opponentHand.Add(deck.Deal());

		GD.Print($"Player hand: {playerHand[0]}, {playerHand[1]}");
		GD.Print($"Opponent hand: {opponentHand[0]}, {opponentHand[1]}");

		playerCard1.ShowCard(playerHand[0]);
		playerCard2.ShowCard(playerHand[1]);
		opponentCard1.ShowBack();
		opponentCard2.ShowBack();
	}

	private void DealCommunityCards(Street street)
	{
		GD.Print($"\n=== Community Cards: {street} ===");
		switch (street)
		{
			case Street.Flop:
				communityCards.Add(deck.Deal());
				communityCards.Add(deck.Deal());
				communityCards.Add(deck.Deal());
				GD.Print($"Flop: {communityCards[0]}, {communityCards[1]}, {communityCards[2]}");
				ShowMessage("Flop dealt");
				break;
			case Street.Turn:
				communityCards.Add(deck.Deal());
				GD.Print($"Turn: {communityCards[3]}");
				ShowMessage("Turn card");
				break;
			case Street.River:
				communityCards.Add(deck.Deal());
				GD.Print($"River: {communityCards[4]}");
				ShowMessage("River card");
				break;
		}
	}

	private void RevealCommunityCards(Street street)
	{
		GD.Print($"\n=== Reveal Community Cards: {street} ===");
		switch (street)
		{
			case Street.Flop:
				flop1.ShowCard(communityCards[0]);
				flop2.ShowCard(communityCards[1]);
				flop3.ShowCard(communityCards[2]);
				break;
			case Street.Turn:
				turnCard.ShowCard(communityCards[3]);
				break;
			case Street.River:
				riverCard.ShowCard(communityCards[4]);
				break;
		}
	}

	private void OnFoldPressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		playerHasActedThisStreet = true;

		ShowMessage("You fold");
		GD.Print("Player folds");
		opponentChips += pot;
		pot = 0;
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");
		handInProgress = false;
		waitingForNextGame = true;
		UpdateHud();
	}

	private void OnCheckCallPressed()
	{
		if (!handInProgress)
		{
			checkCallButton.Text = "Check";
			StartNewHand();
			return;
		}

		if (!isPlayerTurn) return;

		playerHasActedThisStreet = true;

		int toCall = currentBet - playerBet;

		if (toCall == 0)
		{
			ShowMessage("You check");
			GD.Print($"Player checks on {currentStreet}");
		}
		else
		{
			int actualCall = Math.Min(toCall, playerChips);
			playerChips -= actualCall;
			playerBet += actualCall;
			pot += actualCall;

			if (playerChips == 0)
			{
				playerIsAllIn = true;
				ShowMessage($"You call {actualCall} chips (ALL-IN!)");
				GD.Print($"Player calls {actualCall} (ALL-IN)");
			}
			else
			{
				ShowMessage($"You call {actualCall} chips");
				GD.Print($"Player calls {actualCall}, Player stack: {playerChips}, Pot: {pot}");
			}
		}

		isPlayerTurn = false;
		UpdateHud();

		// Delay AI action slightly
		GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
	}

	private void OnBetRaisePressed()
	{
		if (!handInProgress || !isPlayerTurn) return;

		bool isRaise = currentBet > 0;

		// Check raise limit
		if (isRaise && raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			ShowMessage("Maximum raises reached - can only call or fold");
			GD.Print("Max raises reached this street");
			return;
		}

		playerHasActedThisStreet = true;

		int raiseAmount = betAmount;
		int totalBet = currentBet + raiseAmount;
		int toAdd = totalBet - playerBet;
		int actualBet = Math.Min(toAdd, playerChips);

		playerChips -= actualBet;
		playerBet += actualBet;
		pot += actualBet;
		currentBet = playerBet;

		playerBetOnStreet[currentStreet] = true;
		playerBetSizeOnStreet[currentStreet] = actualBet;
		playerTotalBetsThisHand++;

		// Only increment raise counter if this is actually a raise
		if (isRaise)
		{
			raisesThisStreet++;
		}

		if (playerChips == 0)
		{
			playerIsAllIn = true;
			ShowMessage($"You raise to {playerBet} chips (ALL-IN!)");
			GD.Print($"Player raises to {playerBet} (ALL-IN)");
		}
		else if (isRaise)
		{
			ShowMessage($"You raise to {playerBet} chips");
			GD.Print($"Player raises to {playerBet}");
		}
		else
		{
			ShowMessage($"You bet {actualBet} chips");
			GD.Print($"Player bets {actualBet}");
		}

		isPlayerTurn = false;
		UpdateHud();

		// Delay AI action
		GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
	}

	private void ProcessAIAction()
	{
		if (!handInProgress) return;

		AIAction action = DecideAIAction();
		ExecuteAIAction(action);

		opponentHasActedThisStreet = true;

		// Check if hand ended (AI folded or won)
		if (!handInProgress) return;

		// Proper betting round completion logic
		bool betsAreEqual = (playerBet == opponentBet);
		bool bothPlayersActed = playerHasActedThisStreet && opponentHasActedThisStreet;
		bool someoneAllIn = playerIsAllIn || opponentIsAllIn;

		// Round is complete when bets match AND both have acted, OR someone is all-in
		if ((betsAreEqual && bothPlayersActed) || someoneAllIn)
		{
			GD.Print($"Betting round complete: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}, allIn={someoneAllIn}");
			// Add delay before advancing street for better UX
			GetTree().CreateTimer(1.0).Timeout += AdvanceStreet;
		}
		else
		{
			// Betting continues - give turn to player
			GD.Print($"Betting continues: betsEqual={betsAreEqual}, bothActed={bothPlayersActed}");
			isPlayerTurn = true;
			UpdateHud();
			UpdateButtonLabels();
		}
	}

	private AIAction DecideAIAction()
	{
		float handStrength = EvaluateAIHandStrength();
		int toCall = currentBet - opponentBet;
		bool facingBet = toCall > 0;

		GD.Print($"AI Decision - Hand Strength: {handStrength:F2}, To Call: {toCall}");

		// Get thresholds from opponent profile
		float foldThreshold = currentOpponent.FoldThreshold;
		float callThreshold = currentOpponent.CallThreshold;
		float raiseThreshold = currentOpponent.RaiseThreshold;
		float bluffChance = currentOpponent.BluffChance;

		// Apply street-specific modifiers
		float streetMod = (currentStreet == Street.Preflop) 
			? currentOpponent.PreflopAggression 
			: currentOpponent.PostflopAggression;

		// Adjust aggression-based thresholds by street modifier
		raiseThreshold /= streetMod;

		GD.Print($"Thresholds - Fold: {foldThreshold:F2}, Call: {callThreshold:F2}, Raise: {raiseThreshold:F2}, Bluff: {bluffChance:F2}");

		// Adjust thresholds based on player betting patterns
		float playerStrength = EstimatePlayerStrength();
		GD.Print($"Estimated Player Strength: {playerStrength:F2}");

		if (playerStrength > 0.7f)
		{
			foldThreshold += 0.08f;
			raiseThreshold += 0.1f;
		}

		// Adjust based on bet size (pot odds consideration)
		if (facingBet && pot > 0)
		{
			float potOdds = (float)toCall / (pot + toCall);
			if (potOdds > 0.5f) // Large bet - getting bad pot odds
			{
				foldThreshold += 0.1f;
				callThreshold += 0.08f;
			}
			else if (potOdds < 0.25f) // Small bet - getting good pot odds
			{
				foldThreshold -= 0.05f;
			}
		}

		// Prevent infinite raising
		if (raisesThisStreet >= MAX_RAISES_PER_STREET)
		{
			// Can only call or fold at max raises
			if (facingBet)
			{
				return handStrength >= foldThreshold ? AIAction.Call : AIAction.Fold;
			}
			else
			{
				return AIAction.Check;
			}
		}

		// Decision logic
		if (facingBet)
		{
			// Facing a bet - decide fold/call/raise
			if (handStrength < foldThreshold)
			{
				// Check if we should bluff-raise instead
				if (GD.Randf() < bluffChance * 0.5f && raisesThisStreet < MAX_RAISES_PER_STREET)
				{
					aiBluffedThisHand = true;
					return AIAction.Raise;
				}
				return AIAction.Fold;
			}
			else if (handStrength < raiseThreshold)
			{
				// Occasionally bluff-raise with medium hands
				if (GD.Randf() < bluffChance * 0.3f && handStrength > callThreshold && raisesThisStreet < MAX_RAISES_PER_STREET)
				{
					aiBluffedThisHand = true;
					return AIAction.Raise;
				}
				return AIAction.Call;
			}
			else
			{
				// Strong hand - usually raise for value
				if (GD.Randf() < 0.7f && raisesThisStreet < MAX_RAISES_PER_STREET)
					return AIAction.Raise;
				else
					return AIAction.Call; // Slowplay occasionally
			}
		}
		else
		{
			// No bet to face - decide check/bet
			if (handStrength < callThreshold)
			{
				// Weak hand - mostly check, sometimes bluff
				if (GD.Randf() < bluffChance)
				{
					aiBluffedThisHand = true;
					return AIAction.Bet;
				}
				return AIAction.Check;
			}
			else if (handStrength < raiseThreshold)
			{
				// Medium hand - mix of check and bet
				return GD.Randf() < 0.5f ? AIAction.Bet : AIAction.Check;
			}
			else
			{
				// Strong hand - usually bet
				return GD.Randf() < 0.8f ? AIAction.Bet : AIAction.Check;
			}
		}
	}

	private void ExecuteAIAction(AIAction action)
	{
		switch (action)
		{
			case AIAction.Fold:
				ShowMessage("Opponent folds");
				GD.Print("Opponent folds");
				playerChips += pot;
				pot = 0;
				handInProgress = false;
				waitingForNextGame = true;
				UpdateHud();
				break;

			case AIAction.Check:
				ShowMessage("Opponent checks");
				GD.Print("Opponent checks");
				break;

			case AIAction.Call:
				int toCall = currentBet - opponentBet;
				int actualCall = Math.Min(toCall, opponentChips);
				opponentChips -= actualCall;
				opponentBet += actualCall;
				pot += actualCall;

				if (opponentChips == 0)
				{
					opponentIsAllIn = true;
					ShowMessage($"Opponent calls {actualCall} chips (ALL-IN!)");
					GD.Print($"Opponent calls {actualCall} (ALL-IN)");
				}
				else
				{
					ShowMessage($"Opponent calls {actualCall} chips");
					GD.Print($"Opponent calls {actualCall}, Opponent stack: {opponentChips}, Pot: {pot}");
				}
				break;

			case AIAction.Bet:
				int betSize = CalculateAIBetSize();
				int actualBet = Math.Min(betSize, opponentChips);
				opponentChips -= actualBet;
				opponentBet += actualBet;
				pot += actualBet;
				currentBet = opponentBet;

				// DON'T increment raisesThisStreet for initial bet
				if (opponentChips == 0)
				{
					opponentIsAllIn = true;
					ShowMessage($"Opponent bets {actualBet} chips (ALL-IN!)");
					GD.Print($"Opponent bets {actualBet} (ALL-IN)");
				}
				else
				{
					ShowMessage($"Opponent bets {actualBet} chips");
					GD.Print($"Opponent bets {actualBet}");
				}
				break;

			case AIAction.Raise:
				int raiseSize = CalculateAIBetSize();
				int totalRaise = currentBet + raiseSize;
				int toAdd = totalRaise - opponentBet;
				int actualRaise = Math.Min(toAdd, opponentChips);
				opponentChips -= actualRaise;
				opponentBet += actualRaise;
				pot += actualRaise;
				currentBet = opponentBet;
				raisesThisStreet++; // Only increment on actual raise

				if (opponentChips == 0)
				{
					opponentIsAllIn = true;
					ShowMessage($"Opponent raises to {opponentBet} chips (ALL-IN!)");
					GD.Print($"Opponent raises to {opponentBet} (ALL-IN)");
				}
				else
				{
					ShowMessage($"Opponent raises to {opponentBet} chips");
					GD.Print($"Opponent raises to {opponentBet}");
				}
				break;
		}

		UpdateHud();
	}

	private int CalculateAIBetSize()
	{
		float handStrength = EvaluateAIHandStrength();
		int minBet = bigBlind;

		// Use opponent's bet sizing factor from profile
		float sizeFactor = currentOpponent.BetSizeFactor;

		// Reduce bluff sizes
		if (aiBluffedThisHand && handStrength < 0.4f)
			sizeFactor *= 0.65f;

		int betSize = (int)(pot * sizeFactor);

		// Ensure we have a valid bet size (at least minBet)
		betSize = Math.Max(betSize, minBet);

		// Cap at opponent's remaining chips
		int maxBet = Math.Max(minBet, opponentChips);
		return Math.Min(betSize, maxBet);
	}

	private float EvaluateAIHandStrength()
	{
		if (communityCards.Count == 0)
		{
			// Preflop hand strength
			return EvaluatePreflopStrength(opponentHand);
		}

		// Postflop - use your HandEvaluator
		int aiRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);
		float baseStrength = GetStrengthFromRank(aiRank);

		// Adjust for board texture
		float boardModifier = AnalyzeBoardTexture(aiRank);

		return Math.Clamp(baseStrength * boardModifier, 0.05f, 1.0f);
	}

	// Convert hand rank to 0-1 strength (using your HandEvaluator thresholds)
	private float GetStrengthFromRank(int rank)
	{
		if (rank <= 10) return 1.0f;     // Straight flush
		if (rank <= 166) return 0.95f;   // Four of a kind
		if (rank <= 322) return 0.9f;    // Full house
		if (rank <= 1599) return 0.8f;   // Flush
		if (rank <= 1609) return 0.75f;  // Straight
		if (rank <= 2467) return 0.65f;  // Three of a kind
		if (rank <= 3325) return 0.5f;   // Two pair
		if (rank <= 4000) return 0.35f;  // Strong one pair
		if (rank <= 6185) return 0.2f;   // Weak one pair
		return 0.1f; // High card
	}

	// Board texture analysis for better hand evaluation
	private float AnalyzeBoardTexture(int handRank)
	{
		float modifier = 1.0f;

		// Check if board is paired (increases value of trips/boats, decreases value of pairs)
		bool boardPaired = IsBoardPaired();
		if (boardPaired)
		{
			if (handRank <= 322) modifier *= 1.15f;  // Full house or better - stronger
			else if (handRank <= 2467) modifier *= 1.05f;  // Trips - slightly stronger
			else if (handRank <= 6185) modifier *= 0.85f;  // Pairs - weaker (opponent could have trips)
		}

		// Check if board has flush possibility
		int maxSuitCount = GetMaxSuitCount(communityCards);
		if (maxSuitCount >= 3)
		{
			if (handRank <= 1599) modifier *= 1.1f;  // We have flush - stronger
			else modifier *= 0.9f;  // We don't have flush - weaker
		}

		// Check if board is connected (straight possibilities)
		bool boardConnected = IsBoardConnected();
		if (boardConnected)
		{
			if (handRank <= 1609) modifier *= 1.1f;  // We have straight - stronger
			else modifier *= 0.92f;  // We don't - weaker
		}

		return modifier;
	}

	// Helper methods for board texture analysis
	private bool IsBoardPaired()
	{
		if (communityCards.Count < 2) return false;

		for (int i = 0; i < communityCards.Count; i++)
		{
			for (int j = i + 1; j < communityCards.Count; j++)
			{
				if (communityCards[i].Rank == communityCards[j].Rank)
					return true;
			}
		}
		return false;
	}

	private int GetMaxSuitCount(List<Card> cards)
	{
		int[] suitCounts = new int[4];
		foreach (var card in cards)
		{
			suitCounts[(int)card.Suit]++;
		}
		return Math.Max(Math.Max(suitCounts[0], suitCounts[1]),
						Math.Max(suitCounts[2], suitCounts[3]));
	}

	private bool IsBoardConnected()
	{
		if (communityCards.Count < 3) return false;

		List<int> ranks = new List<int>();
		foreach (var card in communityCards)
		{
			ranks.Add((int)card.Rank);
		}
		ranks.Sort();

		// Check for 3+ cards in sequence
		int consecutive = 1;
		for (int i = 1; i < ranks.Count; i++)
		{
			if (ranks[i] == ranks[i - 1] + 1)
			{
				consecutive++;
				if (consecutive >= 3) return true;
			}
			else if (ranks[i] != ranks[i - 1]) // Not a pair
			{
				consecutive = 1;
			}
		}

		return false;
	}

	// Improved preflop evaluation with equity-based values
	private float EvaluatePreflopStrength(List<Card> hand)
	{
		if (hand.Count != 2) return 0.5f;

		int rank1 = (int)hand[0].Rank;
		int rank2 = (int)hand[1].Rank;
		bool suited = hand[0].Suit == hand[1].Suit;
		bool paired = rank1 == rank2;
		int highRank = Math.Max(rank1, rank2);
		int lowRank = Math.Min(rank1, rank2);
		int gap = highRank - lowRank;

		float strength = 0.0f;

		// Base value from high card
		strength += (highRank / 12.0f) * 0.3f;  // Ace=13 gives 0.325
		strength += (lowRank / 12.0f) * 0.15f;  // Kicker value

		// Pocket pair bonus (equity-based)
		if (paired)
		{
			if (highRank >= 12) strength = 0.95f;      // AA
			else if (highRank >= 11) strength = 0.92f; // KK
			else if (highRank >= 10) strength = 0.88f; // QQ
			else if (highRank >= 9) strength = 0.84f;  // JJ
			else if (highRank >= 8) strength = 0.78f;  // TT
			else if (highRank >= 6) strength = 0.70f;  // 99-77
			else if (highRank >= 4) strength = 0.62f;  // 66-55
			else strength = 0.55f;                     // 44-22
			return strength;
		}

		// Suited bonus
		if (suited) strength += 0.08f;

		// Connectedness bonus (potential for straights)
		if (gap == 0) strength += 0.08f;      // Connectors (e.g., 9-8)
		else if (gap == 1) strength += 0.05f; // One-gapper (e.g., 9-7)
		else if (gap == 2) strength += 0.02f; // Two-gapper (e.g., 9-6)

		// Premium hand adjustments
		if (highRank >= 12 && lowRank >= 11)       // AK, AQ, KQ
		{
			strength = suited ? 0.85f : 0.80f;
		}
		else if (highRank >= 12 && lowRank >= 10)  // AJ, KJ, QJ
		{
			strength = suited ? 0.75f : 0.68f;
		}

		return Math.Clamp(strength, 0.15f, 0.95f);
	}

	// FIXED: Player strength estimation with corrected pot calculation
	private float EstimatePlayerStrength()
	{
		float strength = 0.5f;

		// Count betting streets
		int bettingStreets = 0;
		foreach (var kvp in playerBetOnStreet)
		{
			if (kvp.Value) bettingStreets++;
		}

		// Multi-street betting indicates strength
		strength += bettingStreets * 0.12f;

		// Calculate ratio against pot BEFORE the bet was added
		if (playerBetOnStreet.ContainsKey(currentStreet) &&
			playerBetOnStreet[currentStreet] &&
			playerBetSizeOnStreet.ContainsKey(currentStreet))
		{
			int betSize = playerBetSizeOnStreet[currentStreet];
			// Approximate pot before betting round by removing current street bets
			int potBeforeBet = pot - playerBet - opponentBet;

			if (potBeforeBet > 0)
			{
				float betRatio = (float)betSize / potBeforeBet;
				if (betRatio > 1.0f) strength += 0.25f;      // Overbet = very strong
				else if (betRatio > 0.66f) strength += 0.15f; // Large bet
				else if (betRatio < 0.33f) strength -= 0.08f; // Small bet
			}
		}

		return Math.Clamp(strength, 0.1f, 1.0f);
	}

	private void UpdateButtonLabels()
	{
		int toCall = currentBet - playerBet;

		if (toCall == 0)
		{
			checkCallButton.Text = "Check";
			betRaiseButton.Text = $"Bet {betAmount}";
		}
		else
		{
			checkCallButton.Text = $"Call {toCall}";
			// Calculate actual raise amount (call + raise increment)
			int raiseTotal = currentBet + betAmount;
			int toAddForRaise = raiseTotal - playerBet;
			betRaiseButton.Text = $"Raise {toAddForRaise}";
		}

		// Disable raise button if max raises reached
		if (raisesThisStreet >= MAX_RAISES_PER_STREET && !waitingForNextGame)
		{
			betRaiseButton.Disabled = true;
			betRaiseButton.Text = "Max raises";
		}
	}

	private void UpdateHud()
	{
		if (waitingForNextGame)
		{
			// Check if game is over
			if (playerChips < smallBlind || opponentChips < bigBlind)
			{
				checkCallButton.Text = "GAME OVER";
				checkCallButton.Disabled = true;
			}
			else
			{
				checkCallButton.Text = $"Next Hand";
				checkCallButton.Disabled = false;
			}

			foldButton.Visible = false;
			betRaiseButton.Visible = false;
		}
		else
		{
			UpdateButtonLabels();
			foldButton.Visible = true;
			betRaiseButton.Visible = true;

			// Disable buttons during AI turn to prevent race conditions
			bool enableButtons = isPlayerTurn && handInProgress && !playerIsAllIn;
			foldButton.Disabled = !enableButtons;
			checkCallButton.Disabled = !enableButtons;

			// Special handling for raise button
			if (!enableButtons || raisesThisStreet >= MAX_RAISES_PER_STREET)
			{
				betRaiseButton.Disabled = true;
			}
			else
			{
				betRaiseButton.Disabled = false;
			}
		}

		playerStackLabel.Text = $"You: {playerChips}";
		opponentStackLabel.Text = $"Opp: {opponentChips}";
		potLabel.Text = $"Pot: {pot}";
	}

	private void ShowMessage(string text)
	{
		gameStateLabel.Text = text;
	}

	private void AdvanceStreet()
	{
		// Reset bets, action tracking, AND bluff flag for new street
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
		raisesThisStreet = 0;
		playerHasActedThisStreet = false;
		opponentHasActedThisStreet = false;
		aiBluffedThisHand = false; // CRITICAL FIX: Reset bluff flag each street
		isPlayerTurn = true;

		switch (currentStreet)
		{
			case Street.Preflop:
				DealCommunityCards(Street.Flop);
				RevealCommunityCards(Street.Flop);
				currentStreet = Street.Flop;
				break;
			case Street.Flop:
				DealCommunityCards(Street.Turn);
				RevealCommunityCards(Street.Turn);
				currentStreet = Street.Turn;
				break;
			case Street.Turn:
				DealCommunityCards(Street.River);
				RevealCommunityCards(Street.River);
				currentStreet = Street.River;
				break;
			case Street.River:
				ShowDown();
				return; // Don't update HUD/buttons, showdown handles it
		}

		// If both players are all-in, just keep dealing to showdown
		if (playerIsAllIn && opponentIsAllIn)
		{
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			return;
		}

		// If one player is all-in, skip betting and advance
		if (playerIsAllIn || opponentIsAllIn)
		{
			GetTree().CreateTimer(1.5).Timeout += AdvanceStreet;
			return;
		}

		UpdateHud();
		UpdateButtonLabels();
	}

	private void ShowDown()
	{
		GD.Print("\n=== Showdown ===");
		opponentCard1.ShowCard(opponentHand[0]);
		opponentCard2.ShowCard(opponentHand[1]);

		// Use your HandEvaluator
		int playerRank = HandEvaluator.EvaluateHand(playerHand, communityCards);
		int opponentRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);

		string playerHandName = HandEvaluator.GetHandName(playerRank);
		string opponentHandName = HandEvaluator.GetHandName(opponentRank);

		playerHandType.Text = playerHandName;
		opponentHandType.Text = opponentHandName;

		int result = HandEvaluator.CompareHands(playerRank, opponentRank);
		string message = "";

		if (result > 0)
		{
			GD.Print("\n🎉 PLAYER WINS! 🎉");
			message = $"You win with {playerHandName}!";
			if (aiBluffedThisHand && opponentRank > 6185)
			{
				message += " Opponent was bluffing!";
			}
			playerChips += pot;
		}
		else if (result < 0)
		{
			GD.Print("\n😞 OPPONENT WINS! 😞");
			message = $"Opponent wins with {opponentHandName}";
			if (aiBluffedThisHand)
			{
				message += " (was bluffing with weak hand!)";
			}
			else if (opponentRank < 1609)
			{
				message += " - Opponent had a strong hand!";
			}
			opponentChips += pot;
		}
		else
		{
			GD.Print("\n🤝 TIE! SPLIT POT! 🤝");
			message = "Split pot!";
			int split = pot / 2;
			playerChips += split;
			opponentChips += pot - split;
		}

		ShowMessage(message);
		GD.Print($"Stacks -> Player: {playerChips}, Opponent: {opponentChips}");

		pot = 0;
		handInProgress = false;
		waitingForNextGame = true;
		UpdateHud();
	}
}
