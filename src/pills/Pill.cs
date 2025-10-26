using Godot;
using System;
using System.Collections.Generic;

namespace Jellies.src.pills;

public interface Pill
{
    public bool CanSwap(Board board, Vector2I Position, Pill other)
    {
        return true;
    }
    public void OnSwap(Board board, Vector2I Position, Pill other, ref List<IPillEvent> events)
    {
        //OnDestroy(board, Position, ref events);
    }
    public void OnClick(Board board, Vector2I Position, ref List<IPillEvent> events)
    {
    }
    public void OnDestroy(Board board, Vector2I Position, ref List<IPillEvent> events)
    {
    }
    public Node2D CreateNode()
    {
        // Default implementation does nothing
        throw new NotImplementedException();
    }
}

public enum RegularPillColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Purple,
    //White,
    //Black,
}

public record struct EmptyPill : Pill
{
    public Node2D CreateNode()
    {
        // Do nothing for empty pill
        return new Sprite2D();
    }
}

public record struct RegularPill(RegularPillColor Color) : Pill
{
    public static RegularPillColor GetRandomColor()
    {
        var values = Enum.GetValues<RegularPillColor>();
        return (RegularPillColor) values.GetValue(GD.Randi() % values.Length);
    }
    public Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>($"res://assets/pills/{Color.ToString().ToLower()}.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public void OnDestroy(Board board, Vector2I Position, ref List<IPillEvent> events)
    {
        events.Add(new PillDestroyEvent(Position));
    }
}

public record struct DynamitePill : Pill
{
    private const int BlastRadius = 3;
    public Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/dynamite_pack.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public void OnClick(Board board, Vector2I position, ref List<IPillEvent> events) => OnDestroy(board, position, ref events);
    public void OnDestroy(Board board, Vector2I position, ref List<IPillEvent> events)
    {
        /*
         * 5, 4, 3, 2, 3, 4, 5
         * 4, 3, 2, 1, 2, 3, 4
         * 3, 2, 1, 0, 1, 2, 3
         * 4, 3, 2, 1, 2, 3, 4
         * 5, 4, 3, 2, 3, 4, 5
         */
        events.Add(new PillDestroyEvent(position));
        for (int i = -BlastRadius; i <= BlastRadius; i++)
        {
            for (int j = -BlastRadius; j <= BlastRadius; j++)
            {
                if (i + j > BlastRadius + 1) // remove the corners
                    continue;
                if(i == 0 && j == 0) // skip self
                    continue;
                Vector2I targetPos = new Vector2I(i, j) + position;
                if (board.pills.Has(targetPos))
                {
                    board.pills[targetPos].OnDestroy(board, targetPos, ref events);
                }
            }
        }
    }
}

public record struct BombPill : Pill
{
    private const int BlastRadius = 1;
    public Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/bomb.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public void OnClick(Board board, Vector2I position, ref List<IPillEvent> events) => OnDestroy(board, position, ref events);
    public void OnDestroy(Board board, Vector2I position, ref List<IPillEvent> events)
    {
        events.Add(new PillDestroyEvent(position));
        for (int i = -BlastRadius; i <= BlastRadius; i++)
        {
            for (int j = -BlastRadius; j <= BlastRadius; j++)
            {
                if (i == 0 && j == 0) // skip self
                    continue;
                Vector2I targetPos = new Vector2I(i, j) + position;
                if (board.pills.Has(targetPos))
                {
                    board.pills[targetPos].OnDestroy(board, targetPos, ref events);
                }
            }
        }
    }
}

public record struct HorizontalPill : Pill
{
    public Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/horizontal.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public void OnClick(Board board, Vector2I position, ref List<IPillEvent> events) => OnDestroy(board, position, ref events);
    public void OnDestroy(Board board, Vector2I position, ref List<IPillEvent> events)
    {
        events.Add(new PillDestroyEvent(position));
        for (int i = 0; i < board.pills.Width; i++)
        {
            if (i == position.X) // ignore self
                continue;
            Vector2I targetPos = new(i, position.Y);
            if (board.pills.Has(targetPos))
            {
                board.pills[targetPos].OnDestroy(board, targetPos, ref events);
            }
        }
    }
}

public record struct VerticalPill : Pill
{
    public Node2D CreateNode()
    {
        var sprite = new Sprite2D();
        sprite.Texture = GD.Load<Texture2D>("res://assets/pills/vertical.png");
        sprite.Scale = Constants.PillSize * Vector2.One / sprite.Texture.GetSize();
        return sprite;
    }
    public void OnClick(Board board, Vector2I position, ref List<IPillEvent> events) => OnDestroy(board, position, ref events);
    public void OnDestroy(Board board, Vector2I position, ref List<IPillEvent> events)
    {
        events.Add(new PillDestroyEvent(position));
        for (int j = 0; j < board.pills.Height; j++)
        {
            if (j == position.Y) // ignore self
                continue;
            Vector2I targetPos = new(position.X, j);
            if (board.pills.Has(targetPos))
            {
                board.pills[targetPos].OnDestroy(board, targetPos, ref events);
            }
        }
    }
}

public interface IPillEvent;
//public record struct PillSwapEvent(Vector2I PositionA, Vector2I PositionB) : IPillEvent;
public record struct PillDestroyEvent(Vector2I Position) : IPillEvent;
public record struct PillGravityEvent(Vector2I FromPosition, Vector2I ToPosition) : IPillEvent;
public record struct PillCreateEvent(Vector2I SpawnPosition, Vector2I RealPosition) : IPillEvent;