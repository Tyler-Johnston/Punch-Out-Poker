using System;
using Godot;

public partial class PokerGame
{
	public readonly struct ActionApplyResult
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
				if (isPlayer)
					playerCanReopenBetting = false;
				else
					opponentCanReopenBetting = false;
				
				return new ActionApplyResult(0, false, false, actorBet);

			case PlayerAction.Call:
			{
				if (toCall == 0)
				{
					if (isPlayer)
						playerCanReopenBetting = false;
					else
						opponentCanReopenBetting = false;
					
					return new ActionApplyResult(0, false, false, actorBet);
				}

				if (toCall < 0)
				{
					var refundResult = PokerRules.CalculateRefund(toCall, actorBet);
					int refund = refundResult.RefundAmount;

					if (isPlayer) AddPlayerChips(refund);
					else AddOpponentChips(refund);

					actorBet -= refund;
					
					// Remove the specific amount from the street pot tracking
					UncommitFromStreetPot(isPlayer, refundResult.FromStreet);

					RefreshAllInFlagsFromStacks();
					bool isAllInNow = GetIsAllIn(isPlayer);
					bool becameAllIn = (!wasAllIn && isAllInNow);

					if (isPlayer)
						playerCanReopenBetting = false;
					else
						opponentCanReopenBetting = false;

					return new ActionApplyResult(-refund, becameAllIn, false, actorBet);
				}

				// Normal call (may be all-in for less)
				int callAmount = Math.Min(toCall, actorChips);
				if (callAmount <= 0)
				{
					if (isPlayer)
						playerCanReopenBetting = false;
					else
						opponentCanReopenBetting = false;
					
					return new ActionApplyResult(0, false, false, actorBet);
				}

				if (isPlayer) SpendPlayerChips(callAmount);
				else SpendOpponentChips(callAmount);

				CommitToStreetPot(isPlayer, callAmount);
				actorBet += callAmount;

				RefreshAllInFlagsFromStacks();
				bool isAllInNow2 = GetIsAllIn(isPlayer);
				bool becameAllIn2 = (!wasAllIn && isAllInNow2);

				if (isPlayer)
					playerCanReopenBetting = false;
				else
					opponentCanReopenBetting = false;

				return new ActionApplyResult(callAmount, becameAllIn2, false, actorBet);
			}
			
			case PlayerAction.Raise:
			{
				GameManager.LogVerbose($"[APPLY RAISE] raiseToTotal={raiseToTotal}, currentBet={currentBet}, actorBet={actorBet}, actorChips={actorChips}");
				
				if (raiseToTotal < currentBet)
				{
					GameManager.LogVerbose($"[APPLY RAISE] raiseToTotal ({raiseToTotal}) < currentBet ({currentBet}), converting to CALL");
					return ApplyAction(isPlayer, PlayerAction.Call);
				}
				
				if (raiseToTotal <= actorBet)
				{
					GameManager.LogVerbose($"[APPLY RAISE] raiseToTotal ({raiseToTotal}) <= actorBet ({actorBet}), clamping to actorBet (no-op)");
					raiseToTotal = actorBet;
				}
				
				int toAdd = raiseToTotal - actorBet;
				if (toAdd <= 0)
				{
					GameManager.LogVerbose($"[APPLY RAISE] toAdd = {toAdd}, returning 0 chips moved");
					return new ActionApplyResult(0, false, false, actorBet);
				}
				
				GameManager.LogVerbose($"[APPLY RAISE] Moving {toAdd} chips from stack to pot");
				int add = Math.Min(toAdd, actorChips);
				if (add <= 0)
					return new ActionApplyResult(0, false, false, actorBet);
				
				if (isPlayer) SpendPlayerChips(add);
				else SpendOpponentChips(add);
				
				CommitToStreetPot(isPlayer, add);
				actorBet += add;
				
				int raiseIncrement = raiseToTotal - currentBet;
				int minRaiseIncrement = (lastRaiseAmount > 0) ? lastRaiseAmount : bigBlind;
				
				// [INTEGRATION] Use PokerRules to validate Full Raise
				bool isFullRaise = PokerRules.IsFullRaise(raiseIncrement, minRaiseIncrement);
				
				if (isFullRaise)
				{
					// Full raise: update lastRaiseAmount and reopen betting for opponent
					lastRaiseAmount = raiseIncrement;
					if (isPlayer)
					{
						opponentCanReopenBetting = true;
						playerCanReopenBetting = false; // raiser can't act again
					}
					else
					{
						playerCanReopenBetting = true;
						opponentCanReopenBetting = false;
					}
					
					if (isPlayer) opponentHasActedThisStreet = false; 
					else playerHasActedThisStreet = false;
					
					GameManager.LogVerbose($"[FULL RAISE] Increment={raiseIncrement}, reopening for opponent");
				}
				else
				{
					// Under-raise (short all-in): does NOT reopen betting
					if (isPlayer)
						playerCanReopenBetting = false;
					else
						opponentCanReopenBetting = false;
					
					GameManager.LogVerbose($"[UNDER-RAISE] Increment={raiseIncrement} < min={minRaiseIncrement}, NOT reopening");
				}
				
				previousBet = currentBet;
				currentBet = Math.Max(currentBet, actorBet);
				
				RefreshAllInFlagsFromStacks();
				bool isAllInNow = GetIsAllIn(isPlayer);
				bool becameAllIn = (!wasAllIn && isAllInNow);
				
				return new ActionApplyResult(add, becameAllIn, opening, actorBet);
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
				
				int raiseIncrement = shove;
				int minRaiseIncrement = (lastRaiseAmount > 0) ? lastRaiseAmount : bigBlind;
				
				bool isFullRaise = PokerRules.IsFullRaise(raiseIncrement, minRaiseIncrement) && (actorBet > currentBet);
				
				if (isFullRaise)
				{
					lastRaiseAmount = raiseIncrement;
					if (isPlayer)
					{
						opponentCanReopenBetting = true;
						playerCanReopenBetting = false;
					}
					else
					{
						playerCanReopenBetting = true;
						opponentCanReopenBetting = false;
					}
					
					if (isPlayer) opponentHasActedThisStreet = false; 
					else playerHasActedThisStreet = false;
					
					GameManager.LogVerbose($"[FULL ALL-IN] Increment={raiseIncrement}, reopening for opponent");
				}
				else
				{
					if (isPlayer)
						playerCanReopenBetting = false;
					else
						opponentCanReopenBetting = false;
					
					GameManager.LogVerbose($"[UNDER-RAISE ALL-IN] Increment={raiseIncrement} < min={minRaiseIncrement}, NOT reopening");
				}
				
				previousBet = currentBet;
				currentBet = Math.Max(currentBet, actorBet);
				
				RefreshAllInFlagsFromStacks();
				bool isAllInNow = GetIsAllIn(isPlayer);
				bool becameAllIn = (!wasAllIn && isAllInNow);
				
				return new ActionApplyResult(shove, becameAllIn, opening, actorBet);
			}

			default:
				return new ActionApplyResult(0, false, false, actorBet);
		}

	}
}
