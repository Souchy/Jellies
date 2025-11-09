using Godot;
using Jellies.src.cells;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellies.src;

//public enum PillPattern
//{
//    Corner5,
//    TShape5,
//    Line5,

//    Corner4,
//    TShape4,
//    Line4,
//    Square,

//    Line3,
//}

public static class PatternChecker
{
    public static readonly Vector2I[] SquareTopLeft = [Vector2I.Left, Vector2I.Up, Vector2I.Left + Vector2I.Up];
    public static readonly Vector2I[] SquareTopRight = [Vector2I.Right, Vector2I.Up, Vector2I.Right + Vector2I.Up];
    public static readonly Vector2I[] SquareBottomLeft = [Vector2I.Left, Vector2I.Down, Vector2I.Left + Vector2I.Down];
    public static readonly Vector2I[] SquareBottomRight = [Vector2I.Right, Vector2I.Down, Vector2I.Right + Vector2I.Down];
    public static readonly Vector2I[][] Squares = [SquareTopLeft, SquareTopRight, SquareBottomLeft, SquareBottomRight];

    public static bool CheckMatchUpLeft(TableArray<Pill> pills, Vector2I position, Pill pill)
    {
        var up = pills.Is(position + Vector2I.Up, pill);//board.pills.Is(position + Vector2I.Up, pill);
        var upleft = pills.Is(position + Vector2I.Left + Vector2I.Up, pill);
        var left = pills.Is(position + Vector2I.Left, pill);
        // If both up and left are false, no match possible
        if ((up || left) == false)
            return false;
        // check 2 cells left
        if (left && pills.Is(position + Vector2I.Left * 2, pill))
            return true;
        // Check 2 cells up
        if (up && pills.Is(position + Vector2I.Up * 2, pill))
            return true;
        // Check 3 cells up-left
        if (left && up && upleft)
            return true;
        return false;
    }
    public static bool CheckSquare(Board board, Vector2I position, out HashSet<Vector2I> matchedPositions)
    {
        matchedPositions = [];
        Pill currentPill = board.pills[position];
        var pos0 = position;
        var pos1 = position + Vector2I.Right;
        var pos2 = position + Vector2I.Down;
        var pos3 = position + Vector2I.Down + Vector2I.Right;
        if (board.pills.Is(pos1, currentPill) &&
            board.pills.Is(pos2, currentPill) &&
            board.pills.Is(pos3, currentPill))
        {
            matchedPositions.Add(pos0);
            matchedPositions.Add(pos1);
            matchedPositions.Add(pos2);
            matchedPositions.Add(pos3);
            return true;
        }
        return false;
    }

    public static bool CheckAllSquares(Board board, Pill pill, Vector2I headPos, out HashSet<Vector2I> matchedPositions)
    {
        matchedPositions = [];
        //Pill currentPill = board.pills[position];
        foreach (var square in Squares)
        {
            var squareInGrid = square.Select(offset => headPos + offset);
            if (squareInGrid.All(pos => board.pills.Is(pos, pill)))
            {
                foreach (var pos in squareInGrid)
                    matchedPositions.Add(pos);
                matchedPositions.Add(headPos);
                return true;
            }
        }
        return false;
    }

    public static bool CheckLine(Board board, Vector2I position, Direction dir, out Vector2I[] matchedPositions)
    {
        int max = dir.IsHorizontal() ? board.pills.Width - position.X : board.pills.Height - position.Y;
        var dirVec = dir.ToVector2I();

        matchedPositions = [];
        Pill currentPill = board.pills[position];
        Vector2I[] line = new Vector2I[max];
        int count = 0;
        for (int i = 0; i < max; i++)
        {
            var pos = position + dirVec * i;
            if (!board.pills.Is(pos, currentPill))
                break;
            line[i] = pos;
            count++;
        }
        if (count >= 3)
        {
            matchedPositions = new Vector2I[count];
            Array.Copy(line, matchedPositions, count);
        }
        return count >= 3;
    }

    public static bool CheckRightLine(Board board, Vector2I position, out Vector2I[] matched, out int count)
    {
        matched = new Vector2I[board.pills.Width - position.X];
        count = 0;
        for (int i = 0; i < matched.Length; i++)
        {
            var pos = position + Vector2I.Right * i;
            if (!board.pills.Is(pos, board.pills[position]))
                break;
            matched[i] = pos;
            count++;
        }
        return count >= 3;
    }
    public static bool CheckDownLine(Board board, Vector2I position, out Vector2I[] matched, out int count)
    {
        matched = new Vector2I[board.pills.Height - position.Y];
        count = 0;
        for (int i = 0; i < matched.Length; i++)
        {
            var pos = position + Vector2I.Down * i;
            if (!board.pills.Is(pos, board.pills[position]))
                break;
            matched[i] = pos;
            count++;
        }
        return count >= 3;
    }

    public static bool CheckAllLines(Board board, Pill pill, Vector2I newPos, out HashSet<Vector2I> matchedPositions)
    {
        matchedPositions = [];
        //Pill currentPill = board.pills[position];

        Vector2I[] horizontal = new Vector2I[board.pills.Width];
        Vector2I[] vertical = new Vector2I[board.pills.Height];
        int horizontalCount = 0; // count center
        int verticalCount = 0; // count center

        // for each direction, put pills in vertical or horizontal lists
        foreach (var dir in Enum.GetValues<Direction>())
        {
            var dirVec = DirectionExtensions.ToVector2I(dir);
            int max = dir switch
            {
                Direction.Right => board.pills.Width - newPos.X - 1,
                Direction.Left => newPos.X,
                Direction.Down => board.pills.Height - newPos.Y - 1,
                Direction.Up => newPos.Y,
                _ => 0
            };
            // start from 1 to skip center
            for (int i = 1; i <= max; i++)
            {
                var pos = newPos + dirVec * i;
                if (!board.pills.Is(pos, pill))
                    break;
                if (dir.IsHorizontal())
                {
                    horizontal[horizontalCount] = pos;
                    horizontalCount++;
                }
                else
                {
                    vertical[verticalCount] = pos;
                    verticalCount++;
                }
            }
        }
        bool foundMatch = false;
        if (horizontalCount >= 2) // 2 cells other than center
        {
            for (int i = 0; i < horizontalCount; i++)
            {
                matchedPositions.Add(horizontal[i]);
            }
            foundMatch = true;
        }
        if (verticalCount >= 2) // 2 cells other than center
        {
            for (int i = 0; i < verticalCount; i++)
            {
                matchedPositions.Add(vertical[i]);
            }
            foundMatch = true;
        }
        if (foundMatch)
            matchedPositions.Add(newPos);
        return foundMatch;
    }

    /// <summary>
    /// Checks for chess "horse" pattern: two in a line and one offset in an L shape.
    /// This means there's a possible swap move to create a line of 3.
    /// TODO: Shouldnt check up-left directions? 
    /// </summary>
    public static bool CheckAllHorseMoves(Board board, Vector2I position)
    {
        Pill currentPill = board.pills[position];
        foreach (var dir in Enum.GetValues<Direction>())
        {
            var dirVec = dir.ToVector2I();
            var posAdjacent = position + dirVec;
            if (!board.pills.Is(posAdjacent, currentPill))
            {
                continue;
            }
            var pos2 = position + dirVec * 2;
            var perpendicularOffset = new Vector2I(dirVec.Y, dirVec.X); // swap x and y to get perpendicular
            var horse1 = pos2 + perpendicularOffset; // x, x, v
            var horse2 = pos2 - perpendicularOffset; // x, x, ^
            var horse3 = pos2 + dirVec; // x, x, _, x

            if (board.pills.Is(horse1, currentPill) || board.pills.Is(horse2, currentPill) || board.pills.Is(horse3, currentPill))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// TODO: Shouldnt check up-left directions? 
    /// </summary>
    public static bool CheckAllSnailMoves(Board board, Vector2I position)
    {
        Pill currentPill = board.pills[position];
        foreach (var dir in Enum.GetValues<Direction>())
        {
            var dirVec = dir.ToVector2I();
            var pos2 = position + dirVec * 2;
            if (!board.pills.Is(pos2, currentPill))
            {
                continue;
            }
            var posAdjacent = position + dirVec;
            var perpendicularOffset = new Vector2I(dirVec.Y, dirVec.X); // swap x and y to get perpendicular
            var snail1 = posAdjacent + perpendicularOffset;
            var snail2 = posAdjacent - perpendicularOffset;

            if (board.pills.Is(snail1, currentPill) || board.pills.Is(snail2, currentPill))
            {
                return true;
            }
        }
        return false;
    }
}
