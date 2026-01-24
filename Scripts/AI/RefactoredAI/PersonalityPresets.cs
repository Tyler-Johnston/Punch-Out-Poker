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
			BaseBluffFrequency = 0.33f,
			BaseFoldThreshold = 0.55f,
			BaseRiskTolerance = 0.60f,
			
			TiltSensitivity = 0.50f,
			RageQuitThreshold = 20.0f,
			SurrenderChipPercent = 0.40f,
			
			CallTendency = 0.50f,
			Chattiness = 0.70f,        
			TellReliability = 0.85f   
		};

		// === TELLS (behavioral cues) ===
		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "relaxed_posture", "casual_bet" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "checks_cards_again", "hesitant" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "nervous_laugh", "quick_decision" };

		// === DIALOGUE LINES ===
		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Yeah, I'll let this one go.",
			"Not my hand.",
			"I fold. Good luck!"
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check.",
			"I'll check it.",
			"Let's see what happens."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I'll call.",
			"Okay, I'm in.",
			"Let's see the next card."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll put some chips in.",
			"Betting here.",
			"Let's make it interesting."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise!",
			"I'm gonna bump it up.",
			"Let's see if you mean it."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in! Let's do this!",
			"I'm putting it all on the line!",
			"Everything I've got!"
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Nice! I'll take that.",
			"Thanks for the chips!",
			"That worked out well."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Ah, you got me.",
			"Good hand.",
			"Well played."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Come on, really?",
			"I can't catch a break...",
			"This is frustrating."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time.",
			"No rush.",
			"Tough decision?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"I like this hand.",
			"Finally, something good!",
			"*smiles confidently*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Hmm, not sure about this...",
			"*sighs*",
			"Well, let's see..."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"*clears throat nervously*",
			"Uh, yeah, I bet.",
			"*fidgets with chips*"
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
			TellReliability = 0.75f, 

			TiltSensitivity = 0.70f,
			RageQuitThreshold = 26.0f,
			SurrenderChipPercent = 0.24f
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "excited_expression", "confident_smile" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "uncertain_look", "bites_lip" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "avoids_eye_contact", "fidgets" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Okay, fine, you can have this one.",
			"Ugh, I fold...",
			"Yeah, this hand is trash."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check.",
			"I'll just check.",
			"Let's see what you do."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I'll call, why not.",
			"Okay, I'm curious!",
			"Sure, let's see another card."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I'll try betting.",
			"Let’s see if this works.",
			"Betting! Please fold..."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise! I think.",
			"Um, yeah, I’ll raise.",
			"Let’s crank it up!"
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in! Oh no, what am I doing?",
			"I’m putting it all in!",
			"Everything! Please don’t have it..."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Wait, I actually won?!",
			"Yesss! That was awesome!",
			"See? I kinda know what I’m doing!"
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Aw man, I really wanted that pot.",
			"Okay, that hurt.",
			"Why do I always lose the fun ones?"
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"You’ve got to be kidding me...",
			"This game hates me.",
			"I swear I’m cursed today."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Don't take forever, I’m too anxious.",
			"Oooh, big decision?",
			"I’m trying not to look at your stack..."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"Ohhh, this looks good!",
			"Finally, some real cards!",
			"*grins way too big*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Ehh, this is kinda bad.",
			"*chews lip nervously*",
			"I should probably fold this..."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"*avoids eye contact*",
			"Yeah, sure, I’m totally strong.",
			"*fidgets with chips a lot*"
		};

		personality.ResetCurrentStats();
		return personality;
	}

	public static PokerPersonality CreateBoyWizard()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Boy Wizard",
			BaseAggression = 0.85f,
			BaseBluffFrequency = 0.70f,
			BaseFoldThreshold = 0.65f,
			BaseRiskTolerance = 0.80f,
			CallTendency = 0.40f, 
			Chattiness = 0.7f,      
			TellReliability = 0.53f,

			// BALANCED FOR GAMEPLAY: The Ego-Tilter
			TiltSensitivity = 0.70f,
			RageQuitThreshold = 35.0f,
			SurrenderChipPercent = 0.10f
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "smirks", "pushes_chips_forward_confidently" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "taps_fingers", "looks_away" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "overconfident_speech", "aggressive_posture" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"I’ll let you have this one... for now.",
			"Even I can’t magic this hand into a winner.",
			"Fine, I fold. Enjoy it."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check. I’m waiting...",
			"I’ll check. Your move.",
			"Let’s see what spell you cast."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I’ll call. Show me something.",
			"Call. I’m curious.",
			"Sure, I’ll see the next card."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I’ll conjure up a bet.",
			"Betting. Abracadabra.",
			"Let’s turn up the pressure."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise. Let’s see if you’re brave.",
			"I’m not done yet—raise.",
			"Up we go!"
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in. Time for a miracle.",
			"I’m going all in—try and stop me.",
			"This is my ultimate spell!"
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Told you, it’s not luck, it’s magic.",
			"Another pot for the prodigy.",
			"Did you really think you could outplay me?"
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Huh. That wasn’t in the prophecy.",
			"Okay, that one stung.",
			"Enjoy it while it lasts."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"These cards are cursed.",
			"This deck needs a new enchantment.",
			"I’m not losing to *you* again."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Thinking hard, are we?",
			"Need a spell to help decide?",
			"I already know what you’re going to do."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"This hand is pure magic.",
			"Oh, this is going to be fun.",
			"*smirks knowingly*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Not exactly legendary...",
			"Even magic has limits.",
			"*taps fingers impatiently*"
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"Let’s see if you believe in magic.",
			"*overconfident grin*",
			"Nothing up my sleeve. Probably."
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
			BaseBluffFrequency = 0.30f,   
			BaseFoldThreshold = 0.50f,     
			BaseRiskTolerance = 0.55f,     
			CallTendency = 0.45f,          
			Chattiness = 0.65f,      
			TellReliability = 0.60f,  
			TiltSensitivity = 0.55f,
			RageQuitThreshold = 35.0f,
			SurrenderChipPercent = 0.25f  
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "tips_hat", "leans_back_confidently" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "adjusts_hat", "shifts_weight" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "stone_faced", "deliberate_bet" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"I’m out, partner.",
			"Not worth saddlin’ up for.",
			"You can have this one."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check, partner.",
			"I’ll just sit tight.",
			"Your move, cowboy."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I’ll call that.",
			"Let’s ride this one out.",
			"Alright, I’m in."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I’ll open her up.",
			"Let’s put some chips in the middle.",
			"Betting, partner."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise, partner.",
			"Let’s kick it up a notch.",
			"I’ll bump it."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in, partner!",
			"Whole herd’s goin’ in.",
			"Let’s see what you’re really made of."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Much obliged.",
			"That’s how it’s done out West.",
			"Got myself a nice little pot."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Well, you got me fair and square.",
			"Can’t win ‘em all.",
			"Guess that dog won’t hunt."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Now hold on just a minute...",
			"That ain’t right.",
			"Dagnabit!"
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time, partner.",
			"This reminds me of a game back in Tucson...",
			"Cards are like cattle—you gotta know when to move ‘em."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"Got me a real good one here.",
			"This hand’s ridin’ high.",
			"*tips hat with a grin*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"This one’s a bit scrawny.",
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

	public static PokerPersonality CreateHippie()
	{
		var personality = new PokerPersonality
		{
			CharacterName = "Hippie",
			BaseAggression = 0.45f,        
			BaseBluffFrequency = 0.25f,    
			BaseFoldThreshold = 0.48f,     
			BaseRiskTolerance = 0.50f,     
			CallTendency = 0.55f,          
			Chattiness = 0.7f,       
			TellReliability = 0.55f,  

			// BALANCED FOR GAMEPLAY: The Vibe Check
			TiltSensitivity = 0.25f,
			RageQuitThreshold = 25.0f,
			SurrenderChipPercent = 0.45f  
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "peaceful_smile", "relaxed_breathing" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "sighs", "scratches_beard" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "forced_calmness", "closes_eyes_briefly" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Nah, I’ll sit this one out.",
			"Let this hand flow to you, man.",
			"I fold. Peace."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"I’ll just check, go with the flow.",
			"Check. No rush.",
			"Let’s see what the universe brings."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I’ll call, feels right.",
			"Yeah, I’m in.",
			"Let’s keep the vibes going."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I’ll send some chips out there.",
			"Betting, man.",
			"Let’s add a little energy to the pot."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I’ll raise it up.",
			"Let’s elevate this hand.",
			"Time to vibe a little higher."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in, man. Trust the universe.",
			"I’m putting it all out there.",
			"Everything on the line—feel the energy."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Nice, the universe provides.",
			"Good vibes, good chips.",
			"That flowed perfectly."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"It’s all part of the journey, man.",
			"Chips come, chips go.",
			"Just breathe it out."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Whoa, that was harsh.",
			"Trying not to let this harsh my mellow.",
			"Deep breaths..."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time, man.",
			"No stress, just vibes.",
			"You feel that? The tension in the air?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"This hand feels really good.",
			"Yeah, this is a nice groove.",
			"*smiles peacefully*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Not loving this energy.",
			"*sighs softly*",
			"We’ll see where this goes."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"*forces a calm smile*",
			"Just gotta trust the vibe...",
			"*closes eyes briefly, then bets*"
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
			BaseBluffFrequency = 0.35f,    
			BaseFoldThreshold = 0.47f,     
			BaseRiskTolerance = 0.52f,     
			CallTendency = 0.50f,          
			Chattiness = 0.65f,       
			TellReliability = 0.50f,  

			// BALANCED FOR GAMEPLAY: Standard
			TiltSensitivity = 0.40f,
			RageQuitThreshold = 45.0f,
			SurrenderChipPercent = 0.30f
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "steady_gaze", "controlled_breathing" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "glances_at_chips", "subtle_frown" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "maintains_composure", "measured_speech" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Sometimes, wisdom is in letting go.",
			"This hand is not my path.",
			"I fold. The river will carry this one."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"I will wait and see.",
			"Check. Patience.",
			"The next card may reveal the truth."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I will follow you there.",
			"I’ll call and listen to the cards.",
			"Let us walk this street together."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I’ll speak with chips instead.",
			"My wager is my poem.",
			"I’ll test the waters."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I raise. Your heart, does it tremble?",
			"Let’s raise the stakes of this story.",
			"Higher, to see what is hidden."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"I give everything to this moment.",
			"All in—no half measures.",
			"This is where fate is decided."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Fortune smiled upon this verse.",
			"This ending felt inevitable.",
			"The river carries my chips to me."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Even loss has its own beauty.",
			"This hand was a lesson, not a failure.",
			"Another verse in the poem of variance."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Calm the heart, calm the mind.",
			"Frustration is a fleeting cloud.",
			"I must not let emotion play the cards."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time—the river does not rush.",
			"In silence, the truth of the hand emerges.",
			"What story do your chips tell?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"This hand feels like destiny.",
			"The pattern here is beautiful.",
			"*holds a steady, knowing gaze*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"This hand wavers like a shadow.",
			"*subtle frown*",
			"Perhaps this one is meant to be folded."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"Sometimes a void can look like fullness.",
			"*measured, calm voice*",
			"What you see may not be what is."
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
			BaseBluffFrequency = 0.45f,    
			BaseFoldThreshold = 0.52f,     
			BaseRiskTolerance = 0.55f,     
			CallTendency = 0.42f,          
			Chattiness = 0.55f,      
			TellReliability = 0.45f, 

			// BALANCED FOR GAMEPLAY: The Entitled Boss
			TiltSensitivity = 0.65f,
			RageQuitThreshold = 65.0f,
			SurrenderChipPercent = 0.175f 
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "regal_posture", "dismissive_gesture" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "fingers_drumming", "glances_at_exit" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "authoritative_tone", "commanding_stare" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"I shall relinquish this hand.",
			"Not worthy of a king’s attention.",
			"You may have this battle, not the war."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"I shall observe.",
			"Check. Show me your intent.",
			"I’ll grant you the first move."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I will meet your wager.",
			"I call. Continue.",
			"Very well, I’ll see the next card."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I shall lead.",
			"A king must dictate the pace.",
			"My chips, my decree."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I raise. Kneel or stand firm.",
			"Your bet is insufficient.",
			"I’ll show you a proper wager."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in. Bow before my resolve.",
			"I stake my entire kingdom.",
			"This is my royal decree."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"As it should be.",
			"The throne remains mine.",
			"Your tribute is appreciated."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Even kings may stumble.",
			"Enjoy this… brief victory.",
			"This changes nothing."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"This is becoming unacceptable.",
			"I will not be made a fool of.",
			"Such insolence from the deck..."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Are you trembling, or merely thinking?",
			"Do not waste my time.",
			"A swift decision befits a worthy opponent."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"This hand is fit for a king.",
			"Victory is all but assured.",
			"*smiles with quiet superiority*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"These cards insult me.",
			"*fingers drum impatiently*",
			"This hand is beneath my station."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"You wouldn’t dare challenge me.",
			"*piercing, commanding stare*",
			"A king’s confidence is never false... or is it?"
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
			TellReliability = 0.35f,  

			TiltSensitivity = 0.25f,
			RageQuitThreshold = 50.0f,
			SurrenderChipPercent = 0.30f 
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "knowing_smile", "strokes_beard_confidently" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "contemplative_pause", "adjusts_spectacles" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "mysterious_chuckle", "cryptic_comment" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Even an old sage must sometimes yield.",
			"This path leads nowhere. I fold.",
			"I shall await a better omen."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"I shall simply observe.",
			"Check. The future is not yet clear.",
			"Patience… let the next card speak."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"I will see your wager.",
			"Let us peer a little further into fate.",
			"I call. The story continues."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"I shall test the waters.",
			"A small nudge to the tapestry of fate.",
			"I offer a question in the form of chips."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"I raise. Consider this a lesson.",
			"Let us raise the stakes of this experiment.",
			"I believe more is required here."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in. Even I cannot see past this.",
			"I commit everything to this vision.",
			"This is my final incantation."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"As foresight suggested.",
			"Experience triumphs once more.",
			"The cards favored wisdom today."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Ah, even prophecy can be misread.",
			"A fascinating outcome.",
			"Another data point for the grand experiment."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"Calm, old friend. Do not drift.",
			"Emotion clouds the arcane.",
			"Frustration is a poor advisor."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"Take your time. Eternity is patient.",
			"The future branches in infinite directions.",
			"What choice will you inscribe into reality?"
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"I have foreseen a favorable outcome.",
			"These cards hum with potential.",
			"*strokes beard with a knowing smile*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"The omens are… uncertain.",
			"*adjusts spectacles thoughtfully*",
			"This hand may yet surprise, or disappoint."
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"Not all that glitters is gold.",
			"*mysterious chuckle*",
			"Sometimes the illusion is all that matters."
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
			Chattiness = 0.25f,      
			TellReliability = 0.20f, 

			// BALANCED FOR GAMEPLAY:
			TiltSensitivity = 0.10f,
			RageQuitThreshold = 80.0f,    
			SurrenderChipPercent = 0.10f
		};

		personality.Tells["strong_hand"] = new Godot.Collections.Array<string>
			{ "emotionless_stare", "precise_chip_placement" };
		personality.Tells["weak_hand"] = new Godot.Collections.Array<string>
			{ "micro_hesitation", "calculated_pause" };
		personality.Tells["bluffing"] = new Godot.Collections.Array<string>
			{ "perfect_timing", "unwavering_focus" };

		personality.Dialogue["OnFold"] = new Godot.Collections.Array<string>
		{
			"Fold.",
			"Negative expectation.",
			"..."
		};

		personality.Dialogue["OnCheck"] = new Godot.Collections.Array<string>
		{
			"Check.",
			"...",
			"Information preserved."
		};

		personality.Dialogue["OnCall"] = new Godot.Collections.Array<string>
		{
			"Call.",
			"Pot odds sufficient.",
			"Continuing line."
		};

		personality.Dialogue["OnBet"] = new Godot.Collections.Array<string>
		{
			"Bet.",
			"Applying pressure.",
			"Exploit probability."
		};

		personality.Dialogue["OnRaise"] = new Godot.Collections.Array<string>
		{
			"Raise.",
			"Adjusting strategy.",
			"Equilibrium shift."
		};

		personality.Dialogue["OnAllIn"] = new Godot.Collections.Array<string>
		{
			"All in.",
			"Maximum leverage.",
			"Commitment reached."
		};

		personality.Dialogue["OnWinPot"] = new Godot.Collections.Array<string>
		{
			"Expected outcome.",
			"Result: favorable.",
			"..."
		};

		personality.Dialogue["OnLosePot"] = new Godot.Collections.Array<string>
		{
			"Variance.",
			"Result: suboptimal.",
			"Data logged."
		};

		personality.Dialogue["OnTilt"] = new Godot.Collections.Array<string>
		{
			"...",
			"Emotional deviation detected. Suppressing.",
			"Recalibrating."
		};

		personality.Dialogue["WhileWaiting"] = new Godot.Collections.Array<string>
		{
			"...",
			"Decision time exceeding average.",
			"Analyzing your range."
		};

		personality.Dialogue["StrongHand"] = new Godot.Collections.Array<string>
		{
			"High equity.",
			"Strong configuration.",
			"*unblinking stare*"
		};

		personality.Dialogue["WeakHand"] = new Godot.Collections.Array<string>
		{
			"Low equity.",
			"Marginal configuration.",
			"*brief pause*"
		};

		personality.Dialogue["Bluffing"] = new Godot.Collections.Array<string>
		{
			"Perceived strength adjusted.",
			"Line: exploitative.",
			"Signal: misleading."
		};

		personality.ResetCurrentStats();
		return personality;
	}
}
