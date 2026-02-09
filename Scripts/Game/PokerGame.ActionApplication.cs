using System;
using Godot;

public partial class PokerGame
{
	private readonly struct ActionApplyResult
	{
		public readonly int AmountMoved;      // Chips moved from stack into street pot (negative if refunded)
		public readonly bool BecameAllIn;
		public readonly bool IsBet;           // True if this was an opening bet on the street (currentBet was 0 before action)
		public readonly int NewActorBetTotal; // Actor's total bet this street AFTER applying action

		public ActionApplyResult(int amountMoved, bool becameAllIn, bool isBet, int newActorBetTotal)
		{
			AmountMoved = amountMoved;
			BecameAllIn = becameAllIn;
			IsBet = isBet;
			NewActorBetTotal = newActorBetTotal;
		}
	}

	private void SpendPlayerChips(int amount)
	{
		if (amount <= 0) return;
		if (amount > playerChips) amount = playerChips;
		playerChips -= amount;
	}

	private void AddPlayerChips(int delta)
	{
		if (delta == 0) return;
		playerChips += delta;
		if (playerChips < 0) playerChips = 0;
		
		RefreshAllInFlagsFromStacks();
	}

	private void RefreshAllInFlagsFromStacks()
	{
		playerIsAllIn = (playerChips <= 0);
		opponentIsAllIn = (opponentChips <= 0);
		if (aiOpponent != null) aiOpponent.IsAllIn = opponentIsAllIn;
	}

	private bool GetIsAllIn(bool isPlayer) => isPlayer ? playerIsAllIn : opponentIsAllIn;
	private int GetActorChips(bool isPlayer) => isPlayer ? playerChips : opponentChips;

	private ActionApplyResult ApplyAction(bool isPlayer, PlayerAction action, int raiseToTotal = 0)
	{
		bool wasAllIn = GetIsAllIn(isPlayer);

		// References to the actor's state
		ref int actorBet = ref (isPlayer ? ref playerBet : ref opponentBet);
		int actorChips = GetActorChips(isPlayer);

		// Derived
		int toCall = currentBet - actorBet;
		bool opening = (currentBet == 0);

		switch (action)
		{
			case PlayerAction.Fold:
				return new ActionApplyResult(0, false, false, actorBet);

			case PlayerAction.Check:
				return new ActionApplyResult(0, false, false, actorBet);

			case PlayerAction.Call:
			{
				if (toCall == 0)
					return new ActionApplyResult(0, false, false, actorBet);

				if (toCall < 0)
				{
					// Uncalled bet return / over-invested correction.
					int refund = -toCall;
					if (refund > actorBet) refund = actorBet;

					if (isPlayer) AddPlayerChips(refund);
					else AddOpponentChips(refund);

					actorBet -= refund;
					UncommitFromStreetPot(isPlayer, refund);

					RefreshAllInFlagsFromStacks();
					bool isAllInNow = GetIsAllIn(isPlayer);
					bool becameAllIn = (!wasAllIn && isAllInNow);

					return new ActionApplyResult(-refund, becameAllIn, false, actorBet);
				}

				// Normal call (may be all-in for less)
				int callAmount = Math.Min(toCall, actorChips);
				if (callAmount <= 0)
					return new ActionApplyResult(0, false, false, actorBet);

				if (isPlayer) SpendPlayerChips(callAmount);
				else SpendOpponentChips(callAmount);

				CommitToStreetPot(isPlayer, callAmount);
				actorBet += callAmount;

				RefreshAllInFlagsFromStacks();
				bool isAllInNow2 = GetIsAllIn(isPlayer);
				bool becameAllIn2 = (!wasAllIn && isAllInNow2);

				return new ActionApplyResult(callAmount, becameAllIn2, false, actorBet);
			}

			case PlayerAction.AllIn:
			{
				int shove = actorChips;
				if (shove <= 0)
					return new ActionApplyResult(0, false, false, actorBet);

				if (isPlayer) SpendPlayerChips(shove);
				else SpendOpponentChips(shove);

				CommitToStreetPot(isPlayer, shove);
				actorBet += shove;

				// All-in can be a bet or a raise depending on currentBet.
				previousBet = currentBet;
				currentBet = Math.Max(currentBet, actorBet);
				if (isPlayer) opponentHasActedThisStreet = false; else playerHasActedThisStreet = false;

				RefreshAllInFlagsFromStacks();
				bool isAllInNow = GetIsAllIn(isPlayer);
				bool becameAllIn = (!wasAllIn && isAllInNow);

				return new ActionApplyResult(shove, becameAllIn, opening, actorBet);
			}

			case PlayerAction.Raise:
			{
				// raiseToTotal is the actor's intended FINAL total bet this street.
				// Must be at least a call; otherwise we can leave actorBet < currentBet (illegal partial raise).
			  	GD.Print($"[APPLY RAISE] raiseToTotal={raiseToTotal}, currentBet={currentBet}, actorBet={actorBet}, actorChips={actorChips}");
				if (raiseToTotal < currentBet)
				{
					GD.Print($"[APPLY RAISE] ⚠️ raiseToTotal ({raiseToTotal}) < currentBet ({currentBet}), converting to CALL");
					return ApplyAction(isPlayer, PlayerAction.Call);
				}

				// No-op guard (covers raiseToTotal == actorBet and raiseToTotal < actorBet).
				if (raiseToTotal <= actorBet)
				{
					GD.Print($"[APPLY RAISE] ⚠️ raiseToTotal ({raiseToTotal}) <= actorBet ({actorBet}), clamping to actorBet (no-op)");
					raiseToTotal = actorBet;
				}

				int toAdd = raiseToTotal - actorBet;
				if (toAdd <= 0)
				{
					GD.Print($"[APPLY RAISE] ❌ toAdd = {toAdd}, returning 0 chips moved");
					return new ActionApplyResult(0, false, false, actorBet);
				}
				GD.Print($"[APPLY RAISE] ✅ Moving {toAdd} chips from stack to pot");
				
				int add = Math.Min(toAdd, actorChips);
				if (add <= 0)
					return new ActionApplyResult(0, false, false, actorBet);

				if (isPlayer) SpendPlayerChips(add);
				else SpendOpponentChips(add);

				CommitToStreetPot(isPlayer, add);
				actorBet += add;

				previousBet = currentBet;
				currentBet = Math.Max(currentBet, actorBet);

				// A bet/raise re-opens action for the other player
				if (isPlayer) opponentHasActedThisStreet = false; else playerHasActedThisStreet = false;

				RefreshAllInFlagsFromStacks();
				bool isAllInNow = GetIsAllIn(isPlayer);
				bool becameAllIn = (!wasAllIn && isAllInNow);

				return new ActionApplyResult(add, becameAllIn, opening, actorBet);
			}

			default:
				return new ActionApplyResult(0, false, false, actorBet);
		}
	}
}
