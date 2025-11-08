using Godot;
using Jellies.src.pills;
using Souchy.Godot.structures;
using Souchy.Net.comm;
using Souchy.Net.communication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

public record InputSwapRequest(Vector2I oldPos, Vector2I newPos) : IRequest<bool>;

public class Board
{
    public const int MaxWidth = 10;

    public TableArray<int> terrain;
    public TableArray<Pill> pills;

    public AsyncRequestBus RequestBus = new();
    public EventBus EventBus = new();

    public event Func<IPillEvent, Task> OnPillEvent;


    public Board()
    {
        RequestBus.SubscribeAsync<InputSwapRequest, bool>(OnInputSwap);
    }

    /// <summary>
    /// </summary>
    /// <param name="req">Request containing the Start and End pos of the swap</param>
    /// <returns></returns>
    private async Task<bool> OnInputSwap(InputSwapRequest req, CancellationToken token)
    {
        // Swap temporarily to check matches
        (pills[req.newPos], pills[req.oldPos]) = (pills[req.oldPos], pills[req.newPos]);
        List<Pattern> matchedPatterns = [];
        // Check matches
        bool matched1 = CheckMatchesOnSwap(ref matchedPatterns, req.newPos);
        bool matched2 = CheckMatchesOnSwap(ref matchedPatterns, req.oldPos);
        // Reverse the swap if no match
        if (!(matched1 || matched2))
        {
            (pills[req.newPos], pills[req.oldPos]) = (pills[req.oldPos], pills[req.newPos]);
        }
        else
        {
            var events = new List<IPillEvent>();
            pills[req.oldPos].OnSwap(this, req.oldPos, pills[req.newPos], ref events);
            pills[req.newPos].OnSwap(this, req.newPos, pills[req.oldPos], ref events);
            // TODO: Process swap events
            // TODO: wait animations happening after swap
            var tasks = events.Select(OnPillEvent);
            await Task.WhenAll(tasks);
            await ProcessMatches(matchedPatterns);
        }

        return matched1 || matched2;
    }

    private async Task ProcessMatches(List<Pattern> patterns)
    {
        if (patterns.Count == 0)
            return;
        // Apply destruction
        List<IPillEvent> destroyEvents = [];
        foreach (var pattern in patterns)
        {
            foreach (var cell in pattern.Cells)
            {
                pills[cell].OnDestroy(this, cell, ref destroyEvents);
            }
        }
        // Remove duplicate events
        destroyEvents = destroyEvents.Distinct().ToList();
        // TODO: Process events
        // TODO: wait animations for destruction etc
        var destroytasks = destroyEvents.Select(OnPillEvent);
        await Task.WhenAll(destroytasks);

        // Clear board of destroyed pills
        foreach (var ev in destroyEvents)
            if (ev is PillDestroyEvent pde)
                pills[pde.Position] = new EmptyPill();

        // Apply gravity
        List<PillGravityEvent> gravityEvents = ApplyGravity();
        // TODO: Process gravity events
        // TODO: wait gravity animation
        var gravityTasks = gravityEvents.Select(ge => (IPillEvent) ge).Select(OnPillEvent);
        await Task.WhenAll(gravityTasks);

        // Create new pills
        List<IPillEvent> createEvents = [];
        BoardGenerator.GenerateFillEmptyCells(this, ref createEvents);
        // TODO: Process create/gravity events
        // TODO: wait create/gravity animation
        var createTasks = createEvents.Select(OnPillEvent);
        await Task.WhenAll(createTasks);

        // Match again
        List<Pattern> newMatchedPatterns = [];
        foreach (var ge in gravityEvents)
        {
            CheckMatchesOnSwap(ref newMatchedPatterns, ge.ToPosition);
        }

        // Loop until no new matches
        await ProcessMatches(newMatchedPatterns);
    }

    private List<PillGravityEvent> ApplyGravity()
    {
        List<PillGravityEvent> gravityEvents = [];
        for (int x = 0; x < pills.Width; x++)
        {
            int emptySpaces = 0;
            for (int y = pills.Height - 1; y >= 0; y--)
            {
                var currentPill = pills[x, y];
                if (currentPill is EmptyPill)
                {
                    emptySpaces++;
                }
                else
                if (emptySpaces > 0)
                {
                    // Move pill down by emptySpaces
                    pills[x, y + emptySpaces] = currentPill;
                    pills[x, y] = new EmptyPill();
                    gravityEvents.Add(new PillGravityEvent(new Vector2I(x, y), new Vector2I(x, y + emptySpaces)));
                    y += emptySpaces; // Go back to check for more empty spaces
                }
            }
        }
        return gravityEvents;
    }


    public bool CheckMatchesOnSwap(ref List<Pattern> matchedPatterns, Vector2I newCell)
    {
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
