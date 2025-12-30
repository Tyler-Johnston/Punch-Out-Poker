// PokerGame.GameFlow.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	// Game state fields
	private bool handInProgress = false;
	private bool waitingForNextGame = false;
	private bool isPlayerTurn = true;
	private bool playerIsAllIn = false;
	private bool opponentIsAllIn = false;
	private bool playerHasButton = false; // gets switched to true during new game initialization

	private bool IsGameOver()
	{
		return playerChips < smallBlind || opponentChips < bigBlind;
	}

	private void HandleGameOver()
	{
		if (playerChips < smallBlind)
		{
			ShowMessage("GAME OVER - You ran out of chips!");
			GD.Print("GAME OVER - Player has no chips");
		}
		else if (opponentChips < bigBlind)
		{
			ShowMessage("YOU WIN - Opponent ran out of chips!");
			GD.Print("GAME OVER - Opponent has no chips");
		}

		handInProgress = false;
		waitingForNextGame = true;
		checkCallButton.Text = "GAME OVER";
		checkCallButton.Disabled = true;
		foldButton.Visible = false;
		betRaiseButton.Visible = false;
		UpdateHud();
	}

	private void StartNewHand()
	{
		if (IsGameOver())
		{
			HandleGameOver();
			return;
		}

		GD.Print("\n=== New Hand ===");
		ShowMessage("New hand starting...");

		betSlider.Visible = true;
		foldButton.Visible = true;
		betRaiseButton.Visible = true;
		betSliderLabel.Visible = true;
		potLabel.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";

		deck = new Deck();
		deck.Shuffle();
		deckDealAudioPlayer.Play();

		pot = 0;
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

		// Alternate button each hand
		playerHasButton = !playerHasButton;

		if (playerHasButton)
		{
			// Player has button = small blind
			playerChips -= smallBlind;
			opponentChips -= bigBlind;
			pot += smallBlind + bigBlind;
			playerBet = smallBlind;
			opponentBet = bigBlind;
			currentBet = bigBlind;

			// Player acts first preflop when on button
			isPlayerTurn = true;
			ShowMessage($"Blinds: You {smallBlind}, Opponent {bigBlind}");
			GD.Print($"Player has button - Blinds posted: Player {smallBlind}, Opponent {bigBlind}, Pot: {pot}");
		}
		else
		{
			// Opponent has button = player is big blind
			playerChips -= bigBlind;
			opponentChips -= smallBlind;
			pot += smallBlind + bigBlind;
			playerBet = bigBlind;
			opponentBet = smallBlind;
			currentBet = bigBlind;

			// Opponent acts first preflop when on button
			isPlayerTurn = false;
			ShowMessage($"Blinds: You {bigBlind}, Opponent {smallBlind}");
			GD.Print($"Opponent has button - Blinds posted: Player {bigBlind}, Opponent {smallBlind}, Pot: {pot}");
		}

		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
		GetTree().CreateTimer(2.0).Timeout += () => CheckAndProcessAITurn();
	}

	private void AdvanceStreet()
	{
		ResetBettingRound();
		aiBluffedThisHand = false;

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
				return;
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

		// Post-flop: non-button player acts first
		if (playerHasButton)
		{
			isPlayerTurn = false; // Opponent acts first post-flop
		}
		else
		{
			isPlayerTurn = true; // Player acts first post-flop
		}

		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();

		// Trigger AI action if it's their turn
		CheckAndProcessAITurn();
	}

	private void CheckAndProcessAITurn()
	{
		if (!isPlayerTurn && handInProgress && !waitingForNextGame && !playerIsAllIn && !opponentIsAllIn)
		{
			ShowMessage($"{currentOpponent.Name} is thinking...");
					
			float handStrength = EvaluateAIHandStrength();
			int toCall = currentBet - opponentBet;
			bool facingBet = toCall > 0;
			
			float baseThinkTime;
			
			// Strong hands (nuts, near-nuts) = quick decision
			if (handStrength >= 0.85f)
			{
				baseThinkTime = GD.Randf() * 0.5f + 0.4f; // 0.4-0.9 seconds
			}
			// Very weak hands (obvious folds) = quick decision
			else if (handStrength <= 0.15f && facingBet)
			{
				baseThinkTime = GD.Randf() * 0.6f + 0.5f; // 0.5-1.1 seconds
			}
			// Marginal/medium hands (difficult decisions) = longer think
			else
			{
				baseThinkTime = GD.Randf() * 1.5f + 1.2f; // 1.2-2.7 seconds
			}
			
			// Add extra time if facing a bet (more complex decision)
			if (facingBet)
			{
				baseThinkTime += GD.Randf() * 0.4f + 0.2f; // +0.2-0.6 seconds
			}
			
			// Bluffs take longer - pretending to have a difficult decision
			// Check opponent's bluff tendency to see if they might bluff
			float bluffChance = currentOpponent.Bluffiness;
			bool mightBluff = handStrength < 0.4f && GD.Randf() < bluffChance;
			
			if (mightBluff)
			{
				// Bluffs take longer to look convincing
				baseThinkTime += GD.Randf() * 0.8f + 0.5f; // +0.5-1.3 seconds extra
			}
			
			GetTree().CreateTimer(baseThinkTime).Timeout += () => ProcessOpponentTurn();
		}
	}



	private void ProcessOpponentTurn()
	{
		// Make sure it's still the opponent's turn (in case game state changed)
		if (isPlayerTurn || !handInProgress)
			return;

		// Mark that opponent has acted this street
		opponentHasActedThisStreet = true;

		// Get AI decision using existing logic
		AIAction action = DecideAIAction();

		// Execute the action
		ExecuteAIAction(action);

		// Check if betting round is complete
		if (action == AIAction.Fold)
		{
			// Hand is over, player wins
			return;
		}

		bool betsEqual = (playerBet == opponentBet);
		bool bothActed = playerHasActedThisStreet && opponentHasActedThisStreet;

		GD.Print($"Betting continues: betsEqual={betsEqual}, bothActed={bothActed}");

		if (betsEqual && bothActed)
		{
			// Betting round complete, advance to next street
			GD.Print($"Betting round complete: betsEqual={betsEqual}, bothActed={bothActed}, bothAllIn={playerIsAllIn && opponentIsAllIn}");
			AdvanceStreet();
		}
		else
		{
			// Action back to player
			isPlayerTurn = true;
			UpdateHud();
			UpdateButtonLabels();
			RefreshBetSlider();
		}
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
		string message;

		if (result > 0)
		{
			GD.Print("\nPLAYER WINS!");
			message = $"You win with {playerHandName}!";
			if (aiBluffedThisHand && opponentRank > 6185)
				GD.Print("Opponent was bluffing!");
			playerChips += pot;
		}
		else if (result < 0)
		{
			GD.Print("\nOPPONENT WINS!");
			message = $"Opponent wins with {opponentHandName}";
			if (aiBluffedThisHand)
				GD.Print("Opponent was bluffing with weak hand!");
			else if (opponentRank < 1609)
				GD.Print("Opponent had a strong hand!");
			opponentChips += pot;
		}
		else
		{
			GD.Print("\nSPLIT POT!");
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

		if (IsGameOver())
		{
			HandleGameOver();
		}
		else
		{
			UpdateHud();
			RefreshBetSlider();
		}
	}
}
