using System;
using Godot;

public partial class PokerGame
{
	public readonly struct ActionApplyResult
	{
		public readonly int AmountMoved;
		public readonly bool BecameAllIn;
		public readonly bool IsBet;
		public readonly int NewActorBetTotal;

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
	private int GetActorBet(bool isPlayer) => isPlayer ? potManager.PlayerStreetBet : potManager.OpponentStreetBet;

	private void SetCanReopenBetting(bool isPlayer, bool canReopen)
	{
		if (isPlayer) playerCanReopenBetting = canReopen;
		else opponentCanReopenBetting = canReopen;
	}

	private void HandleReopenBetting(bool isPlayer, bool isFullRaise)
	{
		if (isFullRaise)
		{
			if (isPlayer)
			{
				opponentCanReopenBetting = true;
				playerCanReopenBetting = false;
				opponentHasActedThisStreet = false;
			}
			else
			{
				playerCanReopenBetting = true;
				opponentCanReopenBetting = false;
				playerHasActedThisStreet = false;
			}
			GameManager.LogVerbose($"[FULL RAISE] Reopening for opponent");
		}
		else
		{
			SetCanReopenBetting(isPlayer, false);
			GameManager.LogVerbose($"[UNDER-RAISE] NOT reopening");
		}
	}

	private ActionApplyResult ApplyAction(bool isPlayer, PlayerAction action, int raiseToTotal = 0)
	{
		bool wasAllIn = GetIsAllIn(isPlayer);
		int actorChips = GetActorChips(isPlayer);
		int actorBet = GetActorBet(isPlayer); // snapshot — read-only from here
		int toCall = potManager.CurrentBet - actorBet;
		bool opening = (potManager.CurrentBet == 0);

		switch (action)
		{
			case PlayerAction.Fold:
				return new ActionApplyResult(0, false, false, actorBet);

			case PlayerAction.Check:
				SetCanReopenBetting(isPlayer, false);
				return new ActionApplyResult(0, false, false, actorBet);

			case PlayerAction.Call:
			{
				if (toCall == 0)
				{
					SetCanReopenBetting(isPlayer, false);
					return new ActionApplyResult(0, false, false, actorBet);
				}

				if (toCall < 0)
				{
					// Actor over-contributed relative to currentBet — refund the excess
					var refundResult = PokerRules.CalculateRefund(-toCall, actorBet);
					int refund = refundResult.RefundAmount;

					if (isPlayer) AddPlayerChips(refund);
					else AddOpponentChips(refund);

					potManager.RemoveBet(isPlayer, refundResult.FromStreet);
					RefreshAllInFlagsFromStacks();
					SetCanReopenBetting(isPlayer, false);

					bool becameAllIn = !wasAllIn && GetIsAllIn(isPlayer);
					return new ActionApplyResult(-refund, becameAllIn, false, GetActorBet(isPlayer));
				}

				// Normal call (may be all-in for less)
				int callAmount = Math.Min(toCall, actorChips);
				if (callAmount <= 0)
				{
					SetCanReopenBetting(isPlayer, false);
					return new ActionApplyResult(0, false, false, actorBet);
				}

				if (isPlayer) SpendPlayerChips(callAmount);
				else SpendOpponentChips(callAmount);

				potManager.AddBet(isPlayer, callAmount);
				RefreshAllInFlagsFromStacks();
				SetCanReopenBetting(isPlayer, false);

				bool becameAllIn2 = !wasAllIn && GetIsAllIn(isPlayer);
				return new ActionApplyResult(callAmount, becameAllIn2, false, GetActorBet(isPlayer));
			}

			case PlayerAction.Raise:
			{
				GameManager.LogVerbose($"[APPLY RAISE] raiseToTotal={raiseToTotal}, currentBet={potManager.CurrentBet}, actorBet={actorBet}, actorChips={actorChips}");

				if (raiseToTotal < potManager.CurrentBet)
				{
					GameManager.LogVerbose($"[APPLY RAISE] raiseToTotal ({raiseToTotal}) < currentBet ({potManager.CurrentBet}), converting to CALL");
					return ApplyAction(isPlayer, PlayerAction.Call);
				}

				if (raiseToTotal <= actorBet)
					raiseToTotal = actorBet;

				int toAdd = raiseToTotal - actorBet;
				if (toAdd <= 0)
				{
					GameManager.LogVerbose($"[APPLY RAISE] toAdd={toAdd}, no-op");
					return new ActionApplyResult(0, false, false, actorBet);
				}

				int add = Math.Min(toAdd, actorChips);
				if (add <= 0)
					return new ActionApplyResult(0, false, false, actorBet);

				// Capture BEFORE AddBet mutates the trackers
				int currentBetBefore = potManager.CurrentBet;
				int minRaiseIncrement = potManager.LastRaiseAmount > 0 ? potManager.LastRaiseAmount : bigBlind;
				int raiseIncrement = raiseToTotal - currentBetBefore;

				if (isPlayer) SpendPlayerChips(add);
				else SpendOpponentChips(add);

				potManager.AddBet(isPlayer, add);
				// potManager.CurrentBet, PreviousBet, LastRaiseAmount are now updated

				bool isFullRaise = PokerRules.IsFullRaise(raiseIncrement, minRaiseIncrement);
				HandleReopenBetting(isPlayer, isFullRaise);

				RefreshAllInFlagsFromStacks();
				bool becameAllIn = !wasAllIn && GetIsAllIn(isPlayer);
				return new ActionApplyResult(add, becameAllIn, opening, GetActorBet(isPlayer));
			}

			case PlayerAction.AllIn:
			{
				int shove = actorChips;
				if (shove <= 0)
					return new ActionApplyResult(0, false, false, actorBet);

				// Capture BEFORE AddBet mutates trackers
				int currentBetBefore = potManager.CurrentBet;
				int minRaiseIncrement = potManager.LastRaiseAmount > 0 ? potManager.LastRaiseAmount : bigBlind;

				if (isPlayer) SpendPlayerChips(shove);
				else SpendOpponentChips(shove);

				potManager.AddBet(isPlayer, shove);

				int newActorBet = GetActorBet(isPlayer);
				int raiseIncrement = newActorBet - currentBetBefore;
				bool isFullRaise = PokerRules.IsFullRaise(raiseIncrement, minRaiseIncrement) && (newActorBet > currentBetBefore);
				HandleReopenBetting(isPlayer, isFullRaise);

				RefreshAllInFlagsFromStacks();
				bool becameAllIn = !wasAllIn && GetIsAllIn(isPlayer);
				return new ActionApplyResult(shove, becameAllIn, opening, newActorBet);
			}

			default:
				return new ActionApplyResult(0, false, false, actorBet);
		}
	}
}
