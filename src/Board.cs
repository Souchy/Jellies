using Godot;
using Jellies.src.cells;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellies.src;

public class Board
{
    public const int MaxWidth = 64;

    public TableArray<int> terrain;
    public TableArray<Pill> pills;

    public void CheckMatches()
    {
        TableArray<Pill> pills = this.pills;
        //List<List<Vector2I>> horizontalMatches = new();
        //List<List<Vector2I>> verticalMatches = new();
        TableArray<bool> matchedTable = new(pills.Width, pills.Height);

        // Check all cells for matches
        foreach (var (i, j, currentPill) in pills)
        {
            // Check patterns in order of importance
            bool hasLine = PatternChecker.CheckLines(this, new(), out var lineMatches);
            bool hasSquare = PatternChecker.CheckSquare(this, new(), out var squareMatches);
            // Square > line3 but Square < line4 etc
            if (hasSquare && squareMatches.Count > lineMatches.Count)
            {
                foreach (var pos in squareMatches)
                {
                    matchedTable[pos] = true;
                }
            }
            else
            if (hasLine)
            {
                foreach (var pos in lineMatches)
                {
                    matchedTable[pos] = true;
                }
            }
        }

        foreach (var (i, j, currentPill) in pills)
        {
            
        }

    }

}

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
    public static bool CheckSquare(Board board, Vector2I position, out List<Vector2I> matchedPositions)
    {
        matchedPositions = [];
        var pos0 = position;
        var pos1 = position + Vector2I.Right;
        var pos2 = position + Vector2I.Down;
        var pos3 = position + Vector2I.Down + Vector2I.Right;
        Pill currentPill = board.pills[position];
        if (board.pills.Is(pos1, currentPill) &&
            board.pills.Is(pos2, currentPill) &&
            board.pills.Is(pos3, currentPill))
        {
            matchedPositions.AddRange([pos0, pos1, pos2, pos3]);
            return true;
        }
        return false;
    }
    public static bool CheckLines(Board board, Vector2I position, out List<Vector2I> matchedPositions)
    {
        matchedPositions = [];
        Pill currentPill = board.pills[position];

        Span<Vector2I> horizontal = stackalloc Vector2I[Board.MaxWidth];
        Span<Vector2I> vertical = stackalloc Vector2I[Board.MaxWidth];
        int horizontalCount = 0;
        int verticalCount = 0;

        // for each direction, put pills in vertical or horizontal lists
        foreach (var dir in Enum.GetValues<Direction>())
        {
            var dirVec = DirectionExtensions.ToVector2I(dir);
            for (int i = 0; i < Board.MaxWidth; i++)
            {
                var pos = position + dirVec * i;
                if (!board.pills.Is(pos, currentPill))
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
        if (horizontalCount >= 3)
        {
            for(int i = 0; i < horizontalCount; i++)
            {
                matchedPositions.Add(horizontal[i]);
            }
            foundMatch = true;
        }
        if (verticalCount >= 3)
        {
            for (int i = 0; i < verticalCount; i++)
            {
                matchedPositions.Add(vertical[i]);
            }
            foundMatch = true;
        }

        return foundMatch;
    }
}