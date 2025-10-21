using Godot;
using System;
using System.Collections.Generic;

namespace Jellies.src.pills;

public abstract class Pill
{
    public virtual void OnSwap(Board board, Vector2I Position, Pill other, out List<IPillEvent> events)
    {
        events = [];
    }
    public virtual void OnClick(Board board, Vector2I Position, out List<IPillEvent> events)
    {
        events = [];
    }
    public virtual void OnDestroy(Board board, Vector2I Position, out List<IPillEvent> events)
    {
        events = [];
    }
    public virtual Node2D CreateNode()
    {
        // Default implementation does nothing
        throw new System.NotImplementedException();
    }
}

public enum RegularPillColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Purple,
    Black,
    White
}

public class EmptyPill : Pill
{
    public override Node2D CreateNode()
    {
        // Do nothing for empty pill
        return new Node2D();
    }
}

public class RegularPill(RegularPillColor Color) : Pill
{
    public static RegularPillColor GetRandomColor()
    {
        var values = Enum.GetValues<RegularPillColor>();
        return (RegularPillColor) values.GetValue(GD.Randi() % values.Length);
    }
    public override Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>($"res://assets/pills/{Color.ToString().ToLower()}.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
}

public class DynamitePill : Pill
{
    private const int BlastRadius = 3;
    public override Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/dynamite_pack.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public override void OnClick(Board board, Vector2I position, out List<IPillEvent> events) => OnDestroy(board, position, out events);
    public override void OnDestroy(Board board, Vector2I position, out List<IPillEvent> events)
    {
        /*
         * 5, 4, 3, 2, 3, 4, 5
         * 4, 3, 2, 1, 2, 3, 4
         * 3, 2, 1, 0, 1, 2, 3
         * 4, 3, 2, 1, 2, 3, 4
         * 5, 4, 3, 2, 3, 4, 5
         */
        events = [];
        for (int i = -BlastRadius; i <= BlastRadius; i++)
        {
            for (int j = -BlastRadius; j <= BlastRadius; j++)
            {
                if (i + j > BlastRadius + 1) // remove the corners
                    continue;
                Vector2I targetPos = new Vector2I(i, j) + position;
                if (board.pills.Has(targetPos))
                {
                    events.Add(new PillDestroyEvent(targetPos));
                }
            }
        }
    }
}

public class BombPill : Pill
{
    private const int BlastRadius = 1;
    public override Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/bomb.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public override void OnClick(Board board, Vector2I position, out List<IPillEvent> events) => OnDestroy(board, position, out events);
    public override void OnDestroy(Board board, Vector2I position, out List<IPillEvent> events)
    {
        events = [];
        for (int i = -BlastRadius; i <= BlastRadius; i++)
        {
            for (int j = -BlastRadius; j <= BlastRadius; j++)
            {
                Vector2I targetPos = new Vector2I(i, j) + position;
                if (board.pills.Has(targetPos))
                {
                    events.Add(new PillDestroyEvent(targetPos));
                }
            }
        }
    }
}

public class HorizontalPill : Pill
{
    public override Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/horizontal.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public override void OnClick(Board board, Vector2I position, out List<IPillEvent> events) => OnDestroy(board, position, out events);
    public override void OnDestroy(Board board, Vector2I position, out List<IPillEvent> events)
    {
        events = [];
        for (int i = 0; i < board.pills.Width; i++)
        {
            if (i == 0)
                continue;
            Vector2I targetPos = new(i, 0);
            if (board.pills.Has(targetPos))
            {
                events.Add(new PillDestroyEvent(targetPos));
            }
        }
    }
}

public class VerticalPill : Pill
{
    public override Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/vertical.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public override void OnClick(Board board, Vector2I position, out List<IPillEvent> events) => OnDestroy(board, position, out events);
    public override void OnDestroy(Board board, Vector2I position, out List<IPillEvent> events)
    {
        events = [];
        for (int j = 0; j < board.pills.Height; j++)
        {
            if (j == 0)
                continue;
            Vector2I targetPos = new(0, j);
            if (board.pills.Has(targetPos))
            {
                events.Add(new PillDestroyEvent(targetPos));
            }
        }
    }
}

public interface IPillEvent;
public record struct PillSwapEvent(Vector2I PositionA, Vector2I PositionB) : IPillEvent;
public record struct PillDestroyEvent(Vector2I Position) : IPillEvent;