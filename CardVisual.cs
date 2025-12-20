using Godot;
using System;

public partial class CardVisual : Control
{
	private Label rankLabel;
	private Label suitLabel;
	private ColorRect background;
	
	private void EnsureNodesReady()
	{
		if (rankLabel == null)
		{
			rankLabel = GetNode<Label>("RankLabel");
			suitLabel = GetNode<Label>("SuitLabel");
			background = GetNode<ColorRect>("Background");
		}
	}
	
	public void SetCard(Card card)
	{
		EnsureNodesReady(); // Make sure nodes are found
		
		if (card == null) return;
		
		rankLabel.Text = GetRankText(card.Rank);
		suitLabel.Text = GetSuitSymbol(card.Suit);
		
		if (card.Suit == Suit.Hearts || card.Suit == Suit.Diamonds)
		{
			suitLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0, 0));
		}
		else
		{
			suitLabel.AddThemeColorOverride("font_color", new Color(0, 0, 0));
		}
	}
	
	public void SetFaceDown()
	{
		EnsureNodesReady(); // Make sure nodes are found
		
		rankLabel.Text = "";
		suitLabel.Text = "";
		background.Color = new Color(0.2f, 0.4f, 0.6f);
	}
	
	private string GetRankText(Rank rank)
	{
		return rank switch
		{
			Rank.Ace => "A",
			Rank.King => "K",
			Rank.Queen => "Q",
			Rank.Jack => "J",
			Rank.Ten => "10",
			_ => ((int)rank + 2).ToString()
		};
	}
	
	private string GetSuitSymbol(Suit suit)
	{
		return suit switch
		{
			Suit.Spades => "♠",
			Suit.Hearts => "♥",
			Suit.Diamonds => "♦",
			Suit.Clubs => "♣",
			_ => ""
		};
	}
}
