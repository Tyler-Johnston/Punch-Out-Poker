using System.Collections.Generic;

public static class HandRanges
{
	// ~85% of hands - ultra-wide maniac range
	public static HashSet<string> ManiacRange = new HashSet<string>
	{
		// All pairs
		"AA", "KK", "QQ", "JJ", "TT", "99", "88", "77", "66", "55", "44", "33", "22",
		
		// All suited hands
		"AKs", "AQs", "AJs", "ATs", "A9s", "A8s", "A7s", "A6s", "A5s", "A4s", "A3s", "A2s",
		"KQs", "KJs", "KTs", "K9s", "K8s", "K7s", "K6s", "K5s", "K4s", "K3s", "K2s",
		"QJs", "QTs", "Q9s", "Q8s", "Q7s", "Q6s", "Q5s", "Q4s", "Q3s", "Q2s",
		"JTs", "J9s", "J8s", "J7s", "J6s", "J5s", "J4s",
		"T9s", "T8s", "T7s", "T6s", "T5s",
		"98s", "97s", "96s", "95s",
		"87s", "86s", "85s",
		"76s", "75s", "74s",
		"65s", "64s", "54s", "53s",
		
		// Most offsuit hands
		"AKo", "AQo", "AJo", "ATo", "A9o", "A8o", "A7o", "A6o", "A5o", "A4o",
		"KQo", "KJo", "KTo", "K9o", "K8o", "K7o", "K6o",
		"QJo", "QTo", "Q9o", "Q8o", "Q7o",
		"JTo", "J9o", "J8o", "J7o",
		"T9o", "T8o", "T7o",
		"98o", "97o", "87o", "86o", "76o"
	};

	// ~50% of hands - loose range
	public static HashSet<string> LooseRange = new HashSet<string>
	{
		"AA", "KK", "QQ", "JJ", "TT", "99", "88", "77", "66", "55", "44", "33", "22",
		"AKs", "AQs", "AJs", "ATs", "A9s", "A8s", "A7s", "A6s", "A5s", "A4s", "A3s", "A2s",
		"KQs", "KJs", "KTs", "K9s", "K8s", "K7s", "K6s", "K5s",
		"QJs", "QTs", "Q9s", "Q8s", "Q7s",
		"JTs", "J9s", "J8s", "J7s",
		"T9s", "T8s", "T7s",
		"98s", "97s", "87s", "86s", "76s", "75s", "65s", "54s",
		"AKo", "AQo", "AJo", "ATo", "A9o", "A8o", "A7o",
		"KQo", "KJo", "KTo", "K9o",
		"QJo", "QTo", "Q9o",
		"JTo", "J9o", "T9o", "98o"
	};

	// ~35% of hands - balanced range
	public static HashSet<string> BalancedRange = new HashSet<string>
	{
		"AA", "KK", "QQ", "JJ", "TT", "99", "88", "77", "66", "55", "44", "33", "22",
		"AKs", "AQs", "AJs", "ATs", "A9s", "A8s", "A7s", "A6s", "A5s", "A4s", "A3s", "A2s",
		"KQs", "KJs", "KTs", "K9s", "K8s",
		"QJs", "QTs", "Q9s",
		"JTs", "J9s", "T9s", "T8s",
		"98s", "87s", "76s", "65s", "54s",
		"AKo", "AQo", "AJo", "ATo", "A9o",
		"KQo", "KJo", "KTo",
		"QJo", "QTo", "JTo"
	};

	// ~15% of hands - tight range
	public static HashSet<string> TightRange = new HashSet<string>
	{
		"AA", "KK", "QQ", "JJ", "TT", "99", "88", "77",
		"AKs", "AQs", "AJs", "ATs", "A5s", "A4s",
		"KQs", "KJs", "KTs",
		"QJs", "QTs", "JTs",
		"AKo", "AQo", "AJo", "ATo",
		"KQo"
	};

	// Postflop ranges based on opponent strength
	public static HashSet<string> StrongPostflopRange = new HashSet<string>
	{
		"AA", "KK", "QQ", "JJ", "TT", "99", "88", "77",
		"AKs", "AQs", "AJs", "ATs", "A5s", "A4s",
		"KQs", "KJs", "QJs", "JTs", "T9s", "98s", "87s", "76s",
		"AKo", "AQo", "AJo"
	};

	public static HashSet<string> MediumPostflopRange = new HashSet<string>
	{
		"AA", "KK", "QQ", "JJ", "TT", "99", "88", "77", "66", "55", "44", "33", "22",
		"AKs", "AQs", "AJs", "ATs", "A9s", "A8s", "A7s", "A6s", "A5s", "A4s", "A3s", "A2s",
		"KQs", "KJs", "KTs", "K9s", "QJs", "QTs", "Q9s",
		"JTs", "J9s", "T9s", "T8s", "98s", "87s", "76s", "65s", "54s",
		"AKo", "AQo", "AJo", "ATo", "A9o",
		"KQo", "KJo", "QJo"
	};

	public static HashSet<string> WeakPostflopRange = new HashSet<string>
	{
		"AA", "KK", "QQ", "JJ", "TT", "99", "88", "77", "66", "55", "44", "33", "22",
		"AKs", "AQs", "AJs", "ATs", "A9s", "A8s", "A7s", "A6s", "A5s", "A4s", "A3s", "A2s",
		"KQs", "KJs", "KTs", "K9s", "K8s", "K7s", "K6s", "K5s",
		"QJs", "QTs", "Q9s", "Q8s", "Q7s", "Q6s",
		"JTs", "J9s", "J8s", "J7s", "J6s",
		"T9s", "T8s", "T7s", "T6s",
		"98s", "97s", "96s", "87s", "86s", "76s", "75s", "65s", "64s", "54s", "53s",
		"AKo", "AQo", "AJo", "ATo", "A9o", "A8o", "A7o", "A6o", "A5o",
		"KQo", "KJo", "KTo", "K9o", "K8o", "K7o",
		"QJo", "QTo", "Q9o", "Q8o",
		"JTo", "J9o", "J8o", "T9o", "T8o", "98o", "87o"
	};
	
	// NEW: Ultra-narrow range for river all-in shoves (~8-10% of hands)
	// Only premium made hands and strong draws that got there
	public static HashSet<string> RiverShoveRange = new HashSet<string>
	{
		// Premium pairs (for overpairs/sets)
		"AA", "KK", "QQ", "JJ", "TT", "99", "88",
		
		// Premium broadway (for top pairs, two pairs, straights)
		"AKs", "AQs", "AJs", "ATs",
		"AKo", "AQo", "AJo",
		
		// Suited connectors (for made straights/flushes/two pairs)
		"KQs", "KJs", "KTs",
		"QJs", "QTs",
		"JTs", "T9s", "98s", "87s", "76s", "65s"
	};
}
