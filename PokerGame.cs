using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame : Node2D
{
	private enum Street
	{
		Preflop,
		Flop,
		Turn,
		River
	}

	private enum AIAction
	{
		Fold,
		Check,
		Call,
		Bet,
		Raise
	}

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
	
	// AI System Variables
	private float aiAggression = 0.5f; // 0.0-1.0
	private bool aiBluffedThisHand = false;
	private Dictionary<Street, bool> playerBetOnStreet = new Dictionary<Street, bool>();
	private Dictionary<Street, int> playerBetSizeOnStreet = new Dictionary<Street, int>();
	private int playerTotalBetsThisHand = 0;

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
		
		// Randomize AI aggression for variety (can be set manually too)
		aiAggression = (float)GD.RandRange(0.0, 1.0);
		GD.Print($"AI Aggression Level: {aiAggression:F2}");
		
		UpdateHud();
		StartNewHand();
	}

	private void StartNewHand()
	{
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
			
			ShowMessage($"You call {actualCall} chips");
			GD.Print($"Player calls {actualCall}, Player stack: {playerChips}, Pot: {pot}");
		}
		
		isPlayerTurn = false;
		UpdateHud();
		
		// Delay AI action slightly
		GetTree().CreateTimer(0.8).Timeout += ProcessAIAction;
	}

	private void OnBetRaisePressed()
	{
		if (!handInProgress || !isPlayerTurn) return;
		
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
		
		if (playerTotalBetsThisHand == 1)
		{
			ShowMessage($"You bet {actualBet} chips");
			GD.Print($"Player bets {actualBet}");
		}
		else
		{
			ShowMessage($"You raise to {playerBet} chips");
			GD.Print($"Player raises to {playerBet}");
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
		
		// Check if hand ended (AI folded or won)
		if (!handInProgress) return;
		
		// Check if betting round is complete
		if (playerBet == opponentBet)
		{
			// Add delay before advancing street for better UX
			GetTree().CreateTimer(1.0).Timeout += AdvanceStreet;
		}
		else
		{
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
		
		GD.Print($"AI Decision - Hand Strength: {handStrength:F2}, Aggression: {aiAggression:F2}, To Call: {toCall}");
		
		// Determine AI personality thresholds
		float foldThreshold, callThreshold, raiseThreshold, bluffChance;
		
		if (aiAggression <= 0.3f) // Low aggression - Conservative
		{
			foldThreshold = 0.15f;  // LOWERED from 0.3
			callThreshold = 0.35f;   // LOWERED from 0.5
			raiseThreshold = 0.6f;   // LOWERED from 0.7
			bluffChance = 0.0f;
		}
		else if (aiAggression <= 0.6f) // Medium aggression - Balanced
		{
			foldThreshold = 0.12f;   // LOWERED from 0.2
			callThreshold = 0.3f;    // LOWERED from 0.4
			raiseThreshold = 0.55f;  // LOWERED from 0.6
			bluffChance = 0.15f;
		}
		else // High aggression - Aggressive
		{
			foldThreshold = 0.08f;   // LOWERED from 0.1
			callThreshold = 0.2f;    // LOWERED from 0.25
			raiseThreshold = 0.45f;  // LOWERED from 0.4
			bluffChance = 0.4f;
		}
		
		// Adjust thresholds based on player betting patterns
		float playerStrength = EstimatePlayerStrength();
		GD.Print($"Estimated Player Strength: {playerStrength:F2}");
		
		if (playerStrength > 0.7f)
		{
			foldThreshold += 0.1f;
			raiseThreshold += 0.15f;
		}
		
		// Adjust based on bet size
		if (facingBet)
		{
			float betSizeRatio = (float)toCall / Math.Max(pot, 1);
			if (betSizeRatio > 0.75f) // Large bet
			{
				foldThreshold += 0.15f;
				callThreshold += 0.1f;
			}
		}
		
		// Decision logic
		if (facingBet)
		{
			// Facing a bet - decide fold/call/raise
			if (handStrength < foldThreshold)
			{
				// Check if we should bluff-raise instead
				if (GD.Randf() < bluffChance * 0.5f)
				{
					aiBluffedThisHand = true;
					return AIAction.Raise;
				}
				return AIAction.Fold;
			}
			else if (handStrength < raiseThreshold)
			{
				// Occasionally bluff-raise with medium hands
				if (GD.Randf() < bluffChance * 0.3f && handStrength > callThreshold)
				{
					aiBluffedThisHand = true;
					return AIAction.Raise;
				}
				return AIAction.Call;
			}
			else
			{
				// Strong hand - usually raise for value
				if (GD.Randf() < 0.7f)
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
				ShowMessage($"Opponent calls {actualCall} chips");
				GD.Print($"Opponent calls {actualCall}, Opponent stack: {opponentChips}, Pot: {pot}");
				break;
				
			case AIAction.Bet:
				int betSize = CalculateAIBetSize();
				int actualBet = Math.Min(betSize, opponentChips);
				opponentChips -= actualBet;
				opponentBet += actualBet;
				pot += actualBet;
				currentBet = opponentBet;
				ShowMessage($"Opponent bets {actualBet} chips");
				GD.Print($"Opponent bets {actualBet}");
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
				ShowMessage($"Opponent raises to {opponentBet} chips");
				GD.Print($"Opponent raises to {opponentBet}");
				break;
		}
		
		UpdateHud();
	}

	private int CalculateAIBetSize()
	{
		float handStrength = EvaluateAIHandStrength();
		int minBet = bigBlind;
		int maxBet = pot;
		
		// Aggressive AI bets bigger
		float sizeFactor = 0.5f + (aiAggression * 0.5f);
		
		// Bluffs tend to be smaller
		if (aiBluffedThisHand && handStrength < 0.4f)
			sizeFactor *= 0.6f;
		
		int betSize = (int)(pot * sizeFactor);
		return Math.Clamp(betSize, minBet, Math.Min(maxBet, opponentChips));
	}

	private float EvaluateAIHandStrength()
	{
		if (communityCards.Count == 0)
		{
			// Preflop hand strength (simplified)
			return EvaluatePreflopStrength(opponentHand);
		}
		
		// Postflop - evaluate actual hand
		int aiRank = HandEvaluator.EvaluateHand(opponentHand, communityCards);
		
		// Convert rank to 0-1 strength (lower rank = better hand)
		// Straight Flush: 1-10, Four of a Kind: 11-166, Full House: 167-322
		// Flush: 323-1599, Straight: 1600-1609, Three of a Kind: 1610-2467
		// Two Pair: 2468-3325, One Pair: 3326-6185, High Card: 6186-7462
		
		if (aiRank <= 10) return 1.0f; // Straight flush
		if (aiRank <= 166) return 0.95f; // Four of a kind
		if (aiRank <= 322) return 0.9f; // Full house
		if (aiRank <= 1599) return 0.8f; // Flush
		if (aiRank <= 1609) return 0.75f; // Straight
		if (aiRank <= 2467) return 0.65f; // Three of a kind
		if (aiRank <= 3325) return 0.5f; // Two pair
		if (aiRank <= 4000) return 0.35f; // Strong one pair
		if (aiRank <= 6185) return 0.2f; // Weak one pair
		return 0.1f; // High card
	}

	private float EvaluatePreflopStrength(List<Card> hand)
	{
		if (hand.Count != 2) return 0.5f;
		
		int rank1 = (int)hand[0].Rank;
		int rank2 = (int)hand[1].Rank;
		bool suited = hand[0].Suit == hand[1].Suit;
		bool paired = rank1 == rank2;
		
		int highRank = Math.Max(rank1, rank2);
		int lowRank = Math.Min(rank1, rank2);
		
		// Premium pairs
		if (paired && highRank >= 10) return 0.95f; // AA, KK, QQ, JJ
		if (paired && highRank >= 7) return 0.8f; // TT, 99, 88
		if (paired) return 0.6f; // Other pairs
		
		// High cards
		if (highRank >= 12 && lowRank >= 10) return 0.85f; // AK, AQ, AJ, KQ
		if (highRank >= 12 && lowRank >= 8) return suited ? 0.7f : 0.6f; // Ace with medium card
		if (highRank >= 11 && lowRank >= 9) return suited ? 0.65f : 0.55f; // King with good kicker
		
		// Suited connectors and medium cards
		if (suited && Math.Abs(rank1 - rank2) <= 2) return 0.5f;
		if (highRank >= 8) return 0.4f;
		
		return 0.25f; // Weak hand
	}

	private float EstimatePlayerStrength()
	{
		float strength = 0.5f;
		
		// Count betting streets
		int bettingStreets = 0;
		foreach (var kvp in playerBetOnStreet)
		{
			if (kvp.Value) bettingStreets++;
		}
		
		// Player betting on multiple streets = stronger hand
		strength += bettingStreets * 0.15f;
		
		// Check bet sizing on current street
		if (playerBetOnStreet.ContainsKey(currentStreet) && playerBetOnStreet[currentStreet])
		{
			if (playerBetSizeOnStreet.ContainsKey(currentStreet))
			{
				float betRatio = (float)playerBetSizeOnStreet[currentStreet] / Math.Max(pot, 1);
				if (betRatio > 0.75f) strength += 0.2f; // Large bet
				else if (betRatio < 0.33f) strength -= 0.1f; // Small bet (could be weak)
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
			betRaiseButton.Text = $"Raise {betAmount}";
		}
	}

	private void UpdateHud()
	{
		if (waitingForNextGame)
		{
			checkCallButton.Text = $"Next Hand";
			foldButton.Visible = false;
			betRaiseButton.Visible = false;
			checkCallButton.Disabled = false;
		}
		else
		{
			UpdateButtonLabels();
			foldButton.Visible = true;
			betRaiseButton.Visible = true;
			
			// Disable buttons during AI turn to prevent race conditions
			bool enableButtons = isPlayerTurn && handInProgress;
			foldButton.Disabled = !enableButtons;
			checkCallButton.Disabled = !enableButtons;
			betRaiseButton.Disabled = !enableButtons;
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
		// Reset bets for new street
		playerBet = 0;
		opponentBet = 0;
		currentBet = 0;
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
		
		UpdateHud();
		UpdateButtonLabels();
	}

	private void ShowDown()
	{
		GD.Print("\n=== Showdown ===");
		
		opponentCard1.ShowCard(opponentHand[0]);
		opponentCard2.ShowCard(opponentHand[1]);
		
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
