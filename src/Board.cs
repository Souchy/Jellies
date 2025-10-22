using Godot;
using Jellies.src.cells;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellies.src;

public enum PatternType
{
    Line,
    Square,
    // Line3,
    // Line4,
    // Line5,
    // Square,
    // TShape4,
    // TShape5,
    // Corner4,
    // Corner5
}

public record struct Pattern(PatternType Type, Vector2I[] Cells);

public class Board
{
    public const int MaxWidth = 10;

    public TableArray<int> terrain;
    public TableArray<Pill> pills;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="pos1">Start position of the dragged node</param>
    /// <param name="pos2">End position of the dragged node</param>
    /// <returns></returns>
    public bool InputSwap(Vector2I pos1, Vector2I pos2)
    {
        // Swap temporarily to check matches
        (pills[pos2], pills[pos1]) = (pills[pos1], pills[pos2]);
        bool matched1 = CheckMatchesOnSwap(out var matchedPatterns1, pos1, pos2);
        bool matched2 = CheckMatchesOnSwap(out var matchedPatterns2, pos2, pos1);
        // Reverse the swap if no match
        if (!(matched1 || matched2))
        {
            (pills[pos2], pills[pos1]) = (pills[pos1], pills[pos2]);
        }
        return matched1 || matched2;
    }

    /// <summary>
    /// Checks if there are no possible moves left.
    /// Would also work to give move suggestions to the player.
    /// TODO: Heavily threadable.
    /// </summary>
    public bool CheckIsDeadlock()
    {
        TableArray<Pill> pills = this.pills;
        // if any cell contains a non-regular pill, the player can click on it to play.
        if (pills.Any((cell) => cell.v is not EmptyPill && cell.v is not RegularPill))
            return false;
        // Check all cells for possible horse moves
        foreach (var (i, j, currentPill) in pills)
        {
            bool hasHorseMove = PatternChecker.CheckAllHorseMoves(this, new(i, j));
            if (hasHorseMove)
                return false;
            bool hasSnailMove = PatternChecker.CheckAllSnailMoves(this, new(i, j));
            if (hasSnailMove)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Given a set of cells to check, returns those cells and cells in a square around them.
    /// </summary>
    public HashSet<Vector2I> WrapCellsToCheck(params Vector2I[] cellsToCheck)
    {
        HashSet<Vector2I> possibleMoves = new();
        foreach (var cell in cellsToCheck)
        {
            possibleMoves.Add(cell);
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    var adjacent = cell + new Vector2I(i, j);
                    possibleMoves.Add(adjacent);
                }
            }
        }
        return possibleMoves;
    }


    public bool CheckMatchesOnSwap(out List<Pattern> matchedPatterns, Vector2I oldCell, Vector2I newCell) // params Vector2I[] cellsToCheck)
    {
        matchedPatterns = [];
        Pill pill = pills[newCell];

        static (bool, int) checkIntersection(HashSet<Vector2I> cells, List<Pattern> matchedPatterns)
        {
            for (int p = 0; p < matchedPatterns.Count; p++)
                foreach (var cell in cells)
                    if (matchedPatterns[p].Cells.Contains(cell))
                        return (true, p);
            return (false, 0);
        }

        // Check patterns in order of importance
        bool hasLine = PatternChecker.CheckAllLines(this, pill, newCell, out var lineMatches);
        bool hasSquare = PatternChecker.CheckAllSquares(this, pill, newCell, out var squareMatches);
        // Square > line3 but Square < line4 etc
        if (hasSquare && squareMatches.Count > lineMatches.Count)
        {
            (bool intersects, int index) = checkIntersection(squareMatches, matchedPatterns);
            // Add square only if no intersection with existing patterns
            if (!intersects)
            {
                matchedPatterns.Add(new Pattern(PatternType.Square, squareMatches.ToArray()));
            }
        }
        else
        if (hasLine)
        {
            (bool intersects, int index) = checkIntersection(lineMatches, matchedPatterns);
            // Combine patterns
            if (intersects)
            {
                var existingPattern = matchedPatterns[index];
                // Upgrade square to line
                if (existingPattern.Type == PatternType.Square && lineMatches.Count >= existingPattern.Cells.Length)
                {
                    matchedPatterns[index] = new Pattern(PatternType.Line, [.. lineMatches]);
                }
                // Combine line with line
                else
                {
                    var combinedCells = existingPattern.Cells.Union(lineMatches).ToArray();
                    matchedPatterns[index] = new Pattern(existingPattern.Type, combinedCells);
                }
            }
            else
            {
                matchedPatterns.Add(new Pattern(PatternType.Line, [.. lineMatches]));
            }
        }

        return matchedPatterns.Count > 0;
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
    public static readonly Vector2I[] SquareTopLeft = [Vector2I.Left, Vector2I.Up, Vector2I.Left + Vector2I.Up];
    public static readonly Vector2I[] SquareTopRight = [Vector2I.Right, Vector2I.Up, Vector2I.Right + Vector2I.Up];
    public static readonly Vector2I[] SquareBottomLeft = [Vector2I.Left, Vector2I.Down, Vector2I.Left + Vector2I.Down];
    public static readonly Vector2I[] SquareBottomRight = [Vector2I.Right, Vector2I.Down, Vector2I.Right + Vector2I.Down];
    public static readonly Vector2I[][] Squares = [SquareTopLeft, SquareTopRight, SquareBottomLeft, SquareBottomRight];

    public static bool CheckMatchUpLeft(Board board, Vector2I position, Pill pill)
    {
        var up = board.pills.Is(position + Vector2I.Up, pill);
        var upleft = board.pills.Is(position + Vector2I.Left + Vector2I.Up, pill);
        var left = board.pills.Is(position + Vector2I.Left, pill);
        // If both up and left are false, no match possible
        if ((up || left) == false)
            return false;
        // check 2 cells left
        if (left && board.pills.Is(position + Vector2I.Left * 2, pill))
            return true;
        // Check 2 cells up
        if (up && board.pills.Is(position + Vector2I.Up * 2, pill))
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

    public static bool CheckAllSquares(Board board, Pill pill, Vector2I newPos, out HashSet<Vector2I> matchedPositions)
    {
        matchedPositions = [];
        //Pill currentPill = board.pills[position];
        foreach (var square in Squares)
        {
            var squareInGrid = square.Select(offset => newPos + offset);
            if (squareInGrid.All(pos => board.pills.Is(pos, pill)))
            {
                foreach (var pos in squareInGrid)
                    matchedPositions.Add(pos);
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
                Direction.Right => board.pills.Width - newPos.X,
                Direction.Left => newPos.X,
                Direction.Down => board.pills.Height - newPos.Y,
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
        if(foundMatch)
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
