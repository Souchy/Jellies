using Godot;
using System;

namespace Jellies.src.cells;

public enum Direction
{
    Up = 1,
    Down = 2,
    Left = 4,
    Right = 8
}

public static class DirectionExtensions
{
    public static Direction Opposite(this Direction dir)
    {
        return dir switch
        {
            Direction.Up => Direction.Down,
            Direction.Down => Direction.Up,
            Direction.Left => Direction.Right,
            Direction.Right => Direction.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
    }
    public static Vector2I ToVector2I(this Direction dir)
    {
        return dir switch
        {
            Direction.Up => new Vector2I(0, -1),
            Direction.Down => new Vector2I(0, 1),
            Direction.Left => new Vector2I(-1, 0),
            Direction.Right => new Vector2I(1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
    }
    public static bool IsVertical(this Direction dir)
    {
        return dir == Direction.Up || dir == Direction.Down;
    }
    public static bool IsHorizontal(this Direction dir)
    {
        return dir == Direction.Left || dir == Direction.Right;
    }
}