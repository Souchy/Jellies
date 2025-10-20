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
            bool hasLine = PatternChecker.CheckLine(this, new(), out var lineMatches);
            bool hasSquare = PatternChecker.CheckSquare(this, new(), out var squareMatches);
            if(hasSquare && squareMatches.Count > lineMatches.Count)
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
    public static bool CheckLine(Board board, Vector2I position, out List<Vector2I> matchedPositions)
    {
        matchedPositions = [];

        Pill currentPill = board.pills[position];
        Dictionary<bool, HashSet<Vector2I>> directions = Enum.GetValues<Direction>().ToDictionary(d => d.IsHorizontal(), d => new HashSet<Vector2I>());

        // for each direction, put pills in vertical or horizontal lists
        foreach (var dir in Enum.GetValues<Direction>())
        {
            for (int i = 0; i < Board.MaxWidth; i++)
            {
                var pos = position + DirectionExtensions.ToVector2I(dir) * i;
                if (!board.pills.Is(pos, currentPill))
                    break;
                directions[dir.IsHorizontal()].Add(pos);
            }
        }
        bool foundMatch = false;
        foreach (var dir in directions)
        {
            // For each direction, trigger pills if we have 3 or more in a line (including the original position)
            if (dir.Value.Count >= 3)
            {
                matchedPositions.AddRange(dir.Value);
                foundMatch = true;
            }
        }
        return foundMatch;
    }
}