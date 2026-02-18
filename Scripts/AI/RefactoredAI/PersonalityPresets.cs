using Godot;

public static class PersonalityPresets
{
	// ========== CIRCUIT A: Beginner Opponents ==========

	public static PokerPersonality CreateSteve()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Steve",
			BaseAggression = 0.45f,
			BaseBluffFrequency = 0.20f,
			BaseFoldThreshold = 0.55f,
			BaseRiskTolerance = 0.60f,
			TiltSensitivity = 0.94f,
			RageQuitThreshold = 30.0f,
			SurrenderChipPercent = 0.40f,
			CallTendency = 0.50f,
			Chattiness = 0.70f,        
			Composure = 0.25f,
			VoicePitch = 1.09f,
			PatienceLevel = Patience.Low
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Happy", "Smirk" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Worried", "Sad" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Worried", "Neutral" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Nah mate, I'm out.",
			"Not worth it, I'll fold.",
			"You can have this one."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check, mate.",
			"Just checkin'.",
			"Let's see what happens.",
			"I'll check."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"Yeah, I'll call that.",
			"Alright, I'm in.",
			"Fair enough, let's see it."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll chuck some chips in.",
			"Betting here.",
			"Let's make it interesting."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise!",
			"Gonna bump it up a bit.",
			"Let's see if you're serious.",
			"More chips!",
			"Not enough in the pot yet."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in! Let's have a crack at it!",
			"Screw it, all in!",
			"Everything I've got, mate!"
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Beauty! I'll take that.",
			"Cheers for the chips!",
			"That worked out pretty well."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Ah, you got me there.",
			"Good on ya, mate.",
			"Well played."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Bloody hell, seriously?",
			"Can't catch a break...",
			"This is getting frustrating."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time, no worries.",
			"No rush, mate.",
			"Tough call?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"Now this is more like it!",
			"Finally, something decent!",
			"Mate, I'd fold if I were you."
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Hmm, not sure about this one...",
			"These cards are rubbish.",
			"Well, let's see what happens..."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"Yeah these cards are absolute gold, mate.",
			"Hahaha, you're in trouble now!",
			"Never been dealt better cards in me life."
		};

		personality.ResetCurrentStats();
		return personality;
	}

	public static PokerPersonality CreateAryll()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Aryll",
			BaseAggression = 0.30f,
			BaseBluffFrequency = 0.25f,
			BaseFoldThreshold = 0.40f,
			BaseRiskTolerance = 0.35f,
			CallTendency = 0.75f, 
			Chattiness = 0.80f,      
			Composure = 0.30f, 
			TiltSensitivity = 1.15f,
			RageQuitThreshold = 35f,
			SurrenderChipPercent = 0.24f,
			VoicePitch = 1.35f,
			PatienceLevel = Patience.Average
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Happy", "Surprised" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Sad", "Worried" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Worried", "Annoyed" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Okay fine, you can have this one.",
			"Ugh, I fold...",
			"Yeah, this hand is garbage."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check.",
			"I'll just check, I guess.",
			"Let's see what you do, eh?"
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I'll call, why not, eh?",
			"Okay, I'm curious!",
			"Sure, let's see another card."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll try betting, sorry!",
			"Let's see if this works.",
			"Betting! Please fold..."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise! I think?",
			"Um, yeah, I'll raise, sorry!",
			"Let's crank it up!"
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in!!!",
			"I'm putting it all in!",
			"HAHA! ALL IN!"
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Wait, I actually won?! Sorry!",
			"Yessss! That was awesome!",
			"Things are finally looking up, eh?"
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Aw man, I really thought I'd win that time.",
			"It's okay, next time I'll get you?",
			"Why do I always lose the fun ones?"
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"You've gotta be kidding me...",
			"This game hates me, I swear.",
			"I'm cursed today, aren't I?"
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Don't take forever, I'm too anxious.",
			"Oooh, big decision?",
			"I'm trying not to look at your stack..."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"Ohhh, this looks good!",
			"Finally, some real cards!",
			"Love love love these cards!"
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Ehh, this is kinda bad.",
			"Can we trade cards? Please?",
			"I should probably fold this..."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"You should fold. Please?",
			"Yeah, sure, I'll totally win this one.",
			"These are definitely the best cards, eh?"
		};

		personality.ResetCurrentStats();
		return personality;
	}

	public static PokerPersonality CreateBoyWizard()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Boy Wizard",
			BaseAggression = 0.75f,
			BaseBluffFrequency = 0.60f,
			BaseFoldThreshold = 0.65f,
			BaseRiskTolerance = 0.80f,
			CallTendency = 0.40f, 
			Chattiness = 0.7f,      
			Composure = 0.215f,
			TiltSensitivity = 0.90f,
			RageQuitThreshold = 28.0f,
			SurrenderChipPercent = 0.10f,
			VoicePitch = 1.15f,
			PatienceLevel = Patience.VeryLow
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Smirk", "Happy" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Annoyed", "Neutral" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Smirk", "Angry" }; 

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Fine, I fold. Happy now?",
			"Rubbish hand anyway.",
			"I'm only folding to be sporting."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check. I'm waiting...",
			"I'll check. Your move.",
			"Let's see what spell you cast."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I'll call. Show me something.",
			"Call. I'm rather curious.",
			"Very well, I'll see the next card."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll conjure up a bet.",
			"Betting. Simple as that.",
			"Let's turn up the pressure, shall we?"
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I'm not done yet—raise.",
			"Up we go! Keep up if you can."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in! This is my masterstroke!",
			"I'm going all in—try and stop me.",
			"This is my ultimate spell!"
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Told you. It's not luck, it's skill.",
			"Another pot for the prodigy.",
			"Did you really think you could outplay me?"
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"That's going on your permanent record.",
			"Alright, that one stung a bit.",
			"Enjoy it whilst it lasts."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"These cards are absolutely cursed.",
			"This deck needs proper enchanting.",
			"I'm not losing to *you* again."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Have you met my apprentice? She's learning from the best.",
			"Back home, I'm rather feared, you know.",
			"I already know what you're going to do."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"Oh brilliant. Finally.",
			"Oh, this is going to be good fun.",
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Not exactly legendary material...",
			"Even magic has its limits, apparently.",
			"*taps fingers impatiently*"
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"These cards? Absolutely brilliant. Totally.",
			"What do you mean bluffing? I never bluff.",
			"Nothing up my sleeve. Promise."
		};

		personality.ResetCurrentStats();
		return personality;
	}

	// ========== CIRCUIT B: Intermediate Opponents ==========

	public static PokerPersonality CreateCowboy()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Cowboy",
			BaseAggression = 0.55f,        
			BaseBluffFrequency = 0.35f,   
			BaseFoldThreshold = 0.50f,     
			BaseRiskTolerance = 0.55f,     
			CallTendency = 0.45f,          
			Chattiness = 0.65f,      
			Composure = 0.50f,  
			TiltSensitivity = 0.65f,
			RageQuitThreshold = 35.0f,
			SurrenderChipPercent = 0.25f,
			VoicePitch = 0.82f,
			PatienceLevel = Patience.High
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Smirk", "Neutral" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Worried", "Neutral" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Neutral", "Happy" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"I'm out, partner.",
			"Not worth saddlin' up for.",
			"You can have this one."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check, partner.",
			"I'll just sit tight.",
			"Your move, cowboy."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I'll call that.",
			"Let's ride this one out.",
			"Alright, I'm in."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll open her up.",
			"Let's put some chips in the middle.",
			"Betting, partner."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise, partner.",
			"Let's kick it up a notch.",
			"I'll bump it."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in, partner!",
			"Whole herd's goin' in.",
			"Let's see what you're really made of."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Much obliged.",
			"Got myself a nice little pot."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Well, you got me fair and square.",
			"Can't win 'em all.",
			"Guess that dog won't hunt."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Now hold on just a minute...",
			"That ain't right.",
			"Dagnabit!"
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time, partner.",
			"This reminds me of a game back in Tucson...",
			"Cards are like cattle—you gotta know when to move 'em."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"Got me a real good one here.",
			"This hand's ridin' high."
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"This one's a bit scrawny.",
			"*adjusts hat uneasily*",
			"Not sure I like this..."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"You sure about that, partner?",
			"*stone-faced stare*",
			"*slow, deliberate chip push*"
		};

		personality.ResetCurrentStats();
		return personality;
	}

	public static PokerPersonality CreateMalandro()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Malandro",
			BaseAggression = 0.45f,        
			BaseBluffFrequency = 0.85f,    
			BaseFoldThreshold = 0.48f,     
			BaseRiskTolerance = 0.50f,     
			CallTendency = 0.55f,          
			Chattiness = 0.7f,       
			Composure = 0.45f,  
			TiltSensitivity = 0.60f,
			RageQuitThreshold = 33.0f,
			SurrenderChipPercent = 0.45f,
			VoicePitch = 0.92f,
			PatienceLevel = Patience.High
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Happy", "Neutral" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Sad", "Neutral" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Happy", "Neutral" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Não, eu passo, meu amigo.",
			"This one's not for me.",
			"I fold. Tranquilo."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Just checking, meu amigo.",
			"Check. No pressure.",
			"Let's see what happens, né?"
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I'll call, seems right.",
			"Beleza, I'm in.",
			"Let's keep this smooth."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll put some chips in play.",
			"Betting here, meu parceiro.",
			"Let's add a little spice to this."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise it up, smooth and easy.",
			"Let's elevate this game.",
			"Time to turn up the charm."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in, meu amigo. Let's dance.",
			"Everything on the table now.",
			"Vai tudo! Feel the moment."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Ah, muito bom. Life is sweet.",
			"The cards smile on me today.",
			"That's how we do it, né?"
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Tudo bem, it's just part of the game.",
			"Chips come, chips go, meu amigo.",
			"No worries. I'll get it back."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Ai, that's a tough one...",
			"Trying to stay cool here.",
			"Calma, calma..."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time, sem pressa.",
			"No rush, my friend.",
			"Feeling the pressure?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"Ah sim, this hand has style.",
			"Now we're talking, meu parceiro.",
			"*smiles with quiet confidence*"
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Eh, not the best cards...",
			"*sighs lightly*",
			"Let's see where this goes, né?"
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"You really want to test me?",
			"*casual smile, relaxed posture*",
			"Trust me, meu amigo. You should fold."
		};

		personality.ResetCurrentStats();
		return personality;
	}

	public static PokerPersonality CreateApprentice()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Apprentice",
			BaseAggression = 0.50f,        
			BaseBluffFrequency = 0.28f,    
			BaseFoldThreshold = 0.57f,     
			BaseRiskTolerance = 0.52f,     
			CallTendency = 0.50f,          
			Chattiness = 0.65f,       
			Composure = 0.40f,  
			TiltSensitivity = 0.70f,
			RageQuitThreshold = 40.0f,
			SurrenderChipPercent = 0.30f,
			VoicePitch = 1.20f,
			PatienceLevel = Patience.Low
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Happy", "Neutral" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Worried", "Sad" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Neutral", "Surprised" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Sometimes wisdom is knowing when to let go.",
			"This hand is not my path.",
			"I fold. The river flows elsewhere."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"I will observe and wait.",
			"Check. Patience reveals truth.",
			"The next card may show the way."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I will follow this path with you.",
			"I call. Let us see what unfolds.",
			"We walk this street together."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll offer my chips to the pot.",
			"A measured wager.",
			"Testing these waters carefully."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I raise. Does your resolve waver?",
			"Let us raise the stakes of this moment.",
			"Higher, to reveal what is hidden."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"I commit everything to this hand.",
			"All in—with full conviction.",
			"This is where destiny speaks."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"The cards honored my discipline.",
			"This outcome feels... harmonious.",
			"The current carried me forward."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Even in loss, there is learning.",
			"This hand taught me something valuable.",
			"Another verse in the poem of poker."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Breathe. Center yourself.",
			"Frustration clouds the mind.",
			"I must not let emotion guide my hand."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time—the river does not rush.",
			"In silence, clarity emerges.",
			"What story will your chips tell?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"This hand feels aligned with fate.",
			"The pattern here is beautiful.",
			"*steady, focused gaze*"
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"This hand wavers like smoke.",
			"*subtle, contemplative frown*",
			"Perhaps this is meant to be released."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"Illusion can appear as reality.",
			"*calm, measured breathing*",
			"What you perceive may not be truth."
		};

		personality.ResetCurrentStats();
		return personality;
	}

	// ========== CIRCUIT C: Expert Opponents ==========

	public static PokerPersonality CreateKing()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "King",
			BaseAggression = 0.65f,        
			BaseBluffFrequency = 0.40f,    
			BaseFoldThreshold = 0.58f,     
			BaseRiskTolerance = 0.55f,     
			CallTendency = 0.42f,          
			Chattiness = 0.55f,      
			Composure = 0.60f, 
			TiltSensitivity = 0.65f,
			RageQuitThreshold = 32.0f,
			SurrenderChipPercent = 0.175f,
			VoicePitch = 0.70f,
			PatienceLevel = Patience.Average
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Smirk", "Angry" }; // Arrogant
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Annoyed", "Angry" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Smirk", "Neutral" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"I shall relinquish this hand.",
			"Not worthy of my attention.",
			"You may have this battle, not the war."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"I shall observe.",
			"Check. Show me your intent.",
			"I grant you the first move."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I will meet your wager.",
			"I call. Proceed.",
			"Very well, continue."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I shall lead this hand.",
			"A king dictates the pace.",
			"My chips, my command."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I raise. Kneel or stand firm.",
			"Your bet is insufficient.",
			"This is a proper wager."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in. Bow before my conviction.",
			"I stake my entire kingdom.",
			"This is my royal decree."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"As it should be.",
			"The throne remains mine.",
			"Your tribute is accepted."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Even kings may stumble.",
			"Enjoy this… fleeting victory.",
			"This changes nothing."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"This is becoming unacceptable.",
			"I will not be humiliated.",
			"Such insolence from fate itself..."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Are you trembling, or thinking?",
			"Do not waste my time.",
			"Decide swiftly."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"This hand befits royalty.",
			"Victory is assured.",
			"*smiles with quiet superiority*"
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"These cards are beneath me.",
			"*fingers drum impatiently*",
			"This hand insults my station."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"You wouldn't dare challenge a king.",
			"*piercing, commanding stare*",
			"Royal confidence is never misplaced."
		};

		personality.ResetCurrentStats();
		return personality;
	}

	public static PokerPersonality CreateOldWizard()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Old Wizard",
			BaseAggression = 0.55f,        
			BaseBluffFrequency = 0.40f,    
			BaseFoldThreshold = 0.45f,     
			BaseRiskTolerance = 0.58f,     
			CallTendency = 0.48f,          
			Chattiness = 0.5f,       
			Composure = 0.75f,  
			TiltSensitivity = 0.50f,
			RageQuitThreshold = 40.0f,
			SurrenderChipPercent = 0.30f,
			VoicePitch = 0.80f,
			PatienceLevel = Patience.VeryHigh
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Happy", "Neutral" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Neutral", "Worried" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Happy", "Neutral" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Sometimes an old man must yield.",
			"This path leads nowhere. I fold.",
			"I shall await a better omen."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"I shall observe for now.",
			"Check. The future is unclear.",
			"Patience… let the next card speak."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I will see your wager.",
			"Let us peer further into this hand.",
			"I call. The story continues."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"A modest inquiry, in chip form.",
			"Testing the currents of fate.",
			"I offer chips as a question."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I raise. Consider this a lesson.",
			"Let us escalate this experiment.",
			"More is required here, I believe."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in. Even I cannot see beyond this.",
			"I commit everything to this vision.",
			"My final incantation."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Ah, as foresight suggested.",
			"Experience prevails once more.",
			"The cards favored wisdom today."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Hmm, even prophecy can mislead.",
			"A fascinating outcome indeed.",
			"Another lesson from the grand experiment."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Steady now, old friend.",
			"Emotion clouds the mystic arts.",
			"Frustration serves no purpose."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time. I have plenty.",
			"The future branches infinitely.",
			"What choice will you make, I wonder?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"I have glimpsed a favorable outcome.",
			"These cards hum with potential.",
			"*strokes beard knowingly*"
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"The omens are… uncertain here.",
			"*adjusts spectacles thoughtfully*",
			"This hand may surprise, or disappoint."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"Not all that glitters, young one.",
			"*mysterious, knowing chuckle*",
			"Illusion is a powerful tool."
		};

		personality.ResetCurrentStats();
		return personality;
	}

	public static PokerPersonality CreateAkalite()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Akalite",
			BaseAggression = 0.48f,        
			BaseBluffFrequency = 0.50f,    
			BaseFoldThreshold = 0.46f,     
			BaseRiskTolerance = 0.42f,     
			CallTendency = 0.55f,          
			Chattiness = 0.36f,      
			Composure = 0.80f, 
			TiltSensitivity = 0.40f,
			RageQuitThreshold = 50.0f,    
			SurrenderChipPercent = 0.10f,
			VoicePitch = 1.12f,
			PatienceLevel = Patience.VeryHigh
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string> { "Neutral", "Neutral" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string> { "Neutral", "Annoyed" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string> { "Neutral", "Smirk" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Not my hand, darling.",
			"Using these would be unfair to you.",
			"Hmmm. Fold."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Tap tap.",
			"Check, dear.",
			"Tippy tappy."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"Are you alright?",
			"Might as well call.",
			"Ring ring, darling."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"Bet.",
			"Applying a touch of pressure.",
			"Exploiting probability, as one does."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"This pot feels awfully empty.",
			"Do praise me for this raise.",
			"More chips for the table, please."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"Don't panic, dear. All in.",
			"Shall we wrap this up?",
			"All in. How exciting."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Don't worry. Next time I'll go easier on you.",
			"Oh my, did I win again?",
			"Darling, would you help me? These chips are so heavy."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"I pity you thinking that wasn't intentional.",
			"Someone needs charity every now and then.",
			"You're better than you look, dear."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Quiet now.",
			"Well done. You're actually making me try.",
			"Perhaps you do have some talent."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"It's adorable watching you think.",
			"You look rather anxious, dear.",
			"Take your time. It won't matter anyway."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"You should fold, darling.",
			"Oh dear. This is unfortunate for you.",
			"Might as well concede now."
		};
		
		personality.Dialogue["MediumHand"] = new Godot.Collections.Array<string>
		{
			"Let's see what happens...",
			"Could be something here.",
			"Hmm, interesting spot.",
			"We'll find out soon enough."
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"If I were you, I'd fold immediately.",
			"This hand makes me positively giddy.",
			"You clearly don't know what you're doing."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"What's troubling you, dear?",
			"Hit? Stand? Oh, wrong game. My mistake.",
			"If you fold now, I'd appreciate that terribly."
		};

		personality.ResetCurrentStats();
		return personality;
	}
}
