using Godot;
using System;

public partial class CardVisual : Control
{
	private Label rankLabel;
	private Label rankLabel2;
	private Label suitLabel;
	private ColorRect cardFront;
	private ColorRect cardBack;
	
	public override void _Ready()
	{
		rankLabel = GetNode<Label>("RankLabel");
		rankLabel2 = GetNode<Label>("RankLabel2");
		suitLabel = GetNode<Label>("SuitLabel");
		cardFront = GetNode<ColorRect>("CardFront");
		cardBack = GetNode<ColorRect>("CardBack");
	}
	
	public void ShowCard(Card card)
	{
		cardBack.Visible = false;
		
		rankLabel.Text = card.GetRankSymbol();
		rankLabel2.Text = card.GetRankSymbol();
		suitLabel.Text = card.GetSuitSymbol();
	}

	public void ShowBack()
	{
		cardBack.Visible = true;
	}
}
