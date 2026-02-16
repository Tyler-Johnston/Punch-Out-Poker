using Godot;
using System;
using System.Threading.Tasks;

public partial class CardVisual : Control
{
	private TextureRect cardTexture;
	
	public override void _Ready()
	{
		// 1. Setup UI Nodes
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
		
		// 2. Set Initial Size
		// We get a "dummy" texture just to know how big the cards are.
		// Since GameManager has them cached, this is instant.
		Texture2D sampleCard = GameManager.Instance.GetCardTexture(Rank.Ace, Suit.Spades);
		
		if (sampleCard != null)
		{
			CustomMinimumSize = sampleCard.GetSize();
			cardTexture.CustomMinimumSize = sampleCard.GetSize();
		}
	}
	
	// Updates the texture to show the specific card face
	public void ShowCard(Card card)
	{
		// Get cached texture from Manager
		Texture2D texture = GameManager.Instance.GetCardTexture(card.Rank, card.Suit);
		
		if (texture != null)
		{
			cardTexture.Texture = texture;
		}
		else
		{
			GD.PrintErr($"Card texture missing for: {card.Rank} of {card.Suit}");
			ShowBack();
		}
	}
	
	// Shows the card back
	public void ShowBack()
	{
		cardTexture.Texture = GameManager.Instance.GetCardBackTexture();
	}
	
	// Animation logic
	public async Task RevealCard(Card card)
	{
		cardTexture.PivotOffset = cardTexture.Size / 2;

		// Phase 1: Flip halfway (Show Back -> Edge)
		var tween = CreateTween();
		tween.SetEase(Tween.EaseType.In); 
		tween.SetTrans(Tween.TransitionType.Sine); 
		
		// Scale X to 0 (make it thin like a line)
		tween.TweenProperty(cardTexture, "scale:x", 0.0f, 0.15f);
		
		await ToSignal(tween, Tween.SignalName.Finished);

		// Phase 2: Swap Texture (The magic trick)
		ShowCard(card);

		// Phase 3: Flip rest of the way (Edge -> Show Front)
		tween = CreateTween();
		tween.SetEase(Tween.EaseType.Out); 
		tween.SetTrans(Tween.TransitionType.Sine); 
		
		// Scale X back to 1 (full width)
		tween.TweenProperty(cardTexture, "scale:x", 1.0f, 0.15f);
		await ToSignal(tween, Tween.SignalName.Finished);
	}
}
