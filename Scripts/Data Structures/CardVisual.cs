using Godot;
using System;
using System.Collections.Generic;

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
		
		// Load card back (use joker or create a separate back image)
		cardBackTexture = GD.Load<Texture2D>("res://Assets/Textures/card_pngs/card_backs/card_back_1.png");
		
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
		// Convert enum to filename format: "2_of_clubs.png"
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
	
	// Optional: Animated card reveal
	public async void RevealCard(Card card)
	{
		// Flip animation
		var tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(cardTexture, "scale:x", 0.0f, 0.15f);
		await ToSignal(tween, Tween.SignalName.Finished);
		
		ShowCard(card);
		
		tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(cardTexture, "scale:x", 1.0f, 0.15f);
	}
}
