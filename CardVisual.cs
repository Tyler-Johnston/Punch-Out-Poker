using Godot;
using System;

public partial class CardVisual : Control
{
	private Label rankLabel;
	private Label suitLabel;
	private ColorRect cardFront;
	private ColorRect cardBack;
	
	public override void _Ready()
	{
		rankLabel = GetNode<Label>("RankLabel");
		suitLabel = GetNode<Label>("SuitLabel");
		cardFront = GetNode<ColorRect>("CardFront");
		cardBack = GetNode<ColorRect>("CardBack");
	}
	
	public void ShowCard(Card card)
	{
		cardBack.Visible = false;
		
		rankLabel.Text = card.GetRankSymbol();
		suitLabel.Text = card.GetSuitSymbol();
		
		//// Color based on suit
		//Color cardColor = (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds) 
			//? Colors.Red 
			//: Colors.Black;
			//
		//rankLabel.AddThemeColorOverride("font_color", cardColor);
		//suitLabel.AddThemeColorOverride("font_color", cardColor);
	}

	public void ShowBack()
	{
		GD.Print("In SetFaceDown");
		cardBack.Visible = true;
	}
}
