using Godot;
using System.Text.Json;
using System.IO;

public partial class PlayerStats : Node
{
	// --- Raw Counters ---
	public int TotalHands       { get; private set; } = 0;
	public int TotalAllIns      { get; private set; } = 0;
	public int TotalRaises      { get; private set; } = 0;
	public int TotalBets        { get; private set; } = 0;
	public int TotalCalls       { get; private set; } = 0;
	public int TotalFolds       { get; private set; } = 0;
	public int TotalChecks      { get; private set; } = 0;
	public int TotalActions     { get; private set; } = 0;
	public int HandsVPIP        { get; private set; } = 0; // voluntarily put $ in

	// --- Derived Frequencies (safe division) ---
	public float AllInFrequency  => Freq(TotalAllIns,          TotalHands);
	public float VPIP            => Freq(HandsVPIP,            TotalHands);
	public float FoldFrequency   => Freq(TotalFolds,           TotalActions);
	public float CallFrequency   => Freq(TotalCalls,           TotalActions);
	public float RaiseFrequency  => Freq(TotalRaises + TotalBets, TotalActions);

	// High = very aggressive. (Bets+Raises) / Calls
	public float AggressionFactor =>
		TotalCalls > 0
			? (float)(TotalRaises + TotalBets) / TotalCalls
			: (TotalRaises + TotalBets > 0 ? 10f : 0f);

	// --- Minimum sample size before stats are trusted ---
	public bool HasEnoughData(int minHands = 5) => TotalHands >= minHands;

	private static float Freq(int count, int total) =>
		total > 0 ? (float)count / total : 0f;

	// -------------------------------------------------------
	// Recording
	// -------------------------------------------------------

	// Call this whenever the player takes an action
	public void RecordAction(string action, bool isAllIn = false)
	{
		TotalActions++;
		if (isAllIn) TotalAllIns++;

		switch (action)
		{
			case "Raise": TotalRaises++; break;
			case "Bet":   TotalBets++;   break;
			case "Call":  TotalCalls++;  break;
			case "Fold":  TotalFolds++;  break;
			case "Check": TotalChecks++; break;
		}
	}
	
	public float GetAllInCallAdjustment()
	{
		if (!HasEnoughData()) return 0f; // not enough info yet

		// AllInFrequency: 0.0 (never) → 1.0 (every hand)
		// Adjustment range: -0.10 (AI tightens if player never shoves)
		//                    0.00 (neutral baseline ~20% shove freq)
		//                   +0.20 (AI calls much looser if player shoves every hand)

		float baseline = 0.20f;
		float delta = AllInFrequency - baseline;
		return Mathf.Clamp(delta * 0.6f, -0.10f, 0.20f);
	}

	// Call this at the end of each hand
	public void EndHand(bool playerVPIP)
	{
		TotalHands++;
		if (playerVPIP) HandsVPIP++;
	}

	public void Reset()
	{
		TotalHands = TotalAllIns = TotalRaises = TotalBets = 0;
		TotalCalls = TotalFolds = TotalChecks = TotalActions = HandsVPIP = 0;
	}
}
