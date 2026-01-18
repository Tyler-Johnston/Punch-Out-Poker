using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class CardVisual : Control
{
	private TextureRect cardTexture;
	private Dictionary<(Rank, Suit), Texture2D> cardTextures;
	private Texture2D cardBackTexture;
	
	public override void _Ready()
	{
		// Create or get TextureRect
		if (!HasNode("CardTexture"))
		{
			cardTexture = new TextureRect();
			cardTexture.Name = "CardTexture";
			cardTexture.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			cardTexture.StretchMode = TextureRect.StretchModeEnum.Keep;
			AddChild(cardTexture);
		}
		else
		{
			cardTexture = GetNode<TextureRect>("CardTexture");
		}
		
		// Preload all card textures
		LoadAllCardTextures();
		// Load card back
		cardBackTexture = GD.Load<Texture2D>($"res://Assets/Textures/card_pngs/card_backs/card_back_{GameManager.Instance.GetCircuitType() + 1}.png");
		
		// Set initial size based on card texture
		if (cardTextures.Count > 0)
		{
			var sampleCard = cardTextures[(Rank.Two, Suit.Clubs)];
			CustomMinimumSize = sampleCard.GetSize();
			cardTexture.CustomMinimumSize = sampleCard.GetSize();
		}
	}
	
	private void LoadAllCardTextures()
	{
		cardTextures = new Dictionary<(Rank, Suit), Texture2D>();
		
		// Load all 52 cards
		foreach (Suit suit in Enum.GetValues(typeof(Suit)))
		{
			foreach (Rank rank in Enum.GetValues(typeof(Rank)))
			{
				string filename = GetCardFilename(rank, suit);
				string cardFacePath = $"res://Assets/Textures/card_pngs/card_faces/{filename}";
				
				Texture2D texture = GD.Load<Texture2D>(cardFacePath);
				if (texture != null)
				{
					cardTextures[(rank, suit)] = texture;
				}
				else
				{
					GD.PrintErr($"Failed to load card texture: {cardFacePath}");
				}
			}
		}
		
		GD.Print($"Loaded {cardTextures.Count} card textures");
	}
	
	private string GetCardFilename(Rank rank, Suit suit)
	{
		string rankStr = rank switch
		{
			Rank.Two => "2",
			Rank.Three => "3",
			Rank.Four => "4",
			Rank.Five => "5",
			Rank.Six => "6",
			Rank.Seven => "7",
			Rank.Eight => "8",
			Rank.Nine => "9",
			Rank.Ten => "10",
			Rank.Jack => "jack",
			Rank.Queen => "queen",
			Rank.King => "king",
			Rank.Ace => "ace",
			_ => "2"
		};
		
		string suitStr = suit switch
		{
			Suit.Clubs => "clubs",
			Suit.Diamonds => "diamonds",
			Suit.Hearts => "hearts",
			Suit.Spades => "spades",
			_ => "clubs"
		};
		
		return $"{rankStr}_of_{suitStr}.png";
	}
	
	public void ShowCard(Card card)
	{
		if (cardTextures.TryGetValue((card.Rank, card.Suit), out Texture2D texture))
		{
			cardTexture.Texture = texture;
		}
		else
		{
			GD.PrintErr($"Card texture not found: {card.Rank} of {card.Suit}");
			ShowBack();
		}
	}
	
	public void ShowBack()
	{
		cardTexture.Texture = cardBackTexture;
	}
	
	public async Task RevealCard(Card card)
	{
		cardTexture.PivotOffset = cardTexture.Size / 2;

		// Phase 1: Flip halfway (Show Back -> Edge)
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.In); // Start slow, speed up
		tween.SetTrans(Tween.TransitionType.Sine); // Smooth sine wave
		
		// Scale X to 0 (make it thin like a line)
		tween.TweenProperty(cardTexture, "scale:x", 0.0f, 0.15f);
		
		await ToSignal(tween, Tween.SignalName.Finished);

		// Phase 2: Swap Texture (The magic trick)
		ShowCard(card);

		// Phase 3: Flip rest of the way (Edge -> Show Front)
		tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out); // Start fast, slow down to stop
		tween.SetTrans(Tween.TransitionType.Sine);
		
		// Scale X back to 1 (full width)
		tween.TweenProperty(cardTexture, "scale:x", 1.0f, 0.15f);

		await ToSignal(tween, Tween.SignalName.Finished);
	}


}
