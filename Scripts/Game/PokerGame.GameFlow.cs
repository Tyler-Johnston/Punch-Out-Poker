// PokerGame.GameFlow.cs
using Godot;
using System;
using System.Collections.Generic;

public partial class PokerGame
{
	// Game state fields
	private int playerChips = 1000;
	private int opponentChips = 1000;
	private bool handInProgress = false;
	private bool waitingForNextGame = false;
	private bool isPlayerTurn = true;
	private bool playerIsAllIn = false;
	private bool opponentIsAllIn = false;

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

		foldButton.Visible = true;
		betRaiseButton.Visible = true;
		playerHandType.Text = "";
		opponentHandType.Text = "";

		deck = new Deck();
		deck.Shuffle();
		deckDealAudioPlayer.Play();

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
		RefreshBetSlider();
	}

	private void AdvanceStreet()
	{
		ResetBettingRound();
		aiBluffedThisHand = false;
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

		UpdateHud();
		UpdateButtonLabels();
		RefreshBetSlider();
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
				message += " Opponent was bluffing!";
			playerChips += pot;
		}
		else if (result < 0)
		{
			GD.Print("\nOPPONENT WINS!");
			message = $"Opponent wins with {opponentHandName}";
			if (aiBluffedThisHand)
				message += " (was bluffing with weak hand!)";
			else if (opponentRank < 1609)
				message += " - Opponent had a strong hand!";
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
