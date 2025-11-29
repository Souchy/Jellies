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
public record InputClickRequest(Vector2I pos) : IRequest<bool>;

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
        RequestBus.SubscribeAsync<InputClickRequest, bool>(OnInputClick);
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

            // TODO: Combo events? (ex: swap disco + dynamite = spawn dynamites everywhere)
            pills[req.oldPos].OnSwap(this, req.oldPos, pills[req.newPos], ref events);
            pills[req.newPos].OnSwap(this, req.newPos, pills[req.oldPos], ref events);
            // Process events here before continuing actually.

            // TODO: Upgrade events? (ex: line4 = dynamite horizontal/vertical)
            // ..

            // Destroy cells in matched patterns
            var destroyevents = matchedPatterns.Select(p => (IPillEvent) new PillDestroyEvent(p.Cells)).ToList();
            events.AddRange(destroyevents);

            // Process Destruction + Upgrades
            await ProcessDestruction(events);
        }

        return matched1 || matched2;
    }


    private async Task<bool> OnInputClick(InputClickRequest req, CancellationToken token)
    {
        Pill clickedPill = pills[req.pos];
        List<IPillEvent> events = [];
        clickedPill.OnClick(this, req.pos, ref events);

        await ProcessDestruction(events);

        return events.Count > 0;
    }


    private async Task ChainDestroyPills(List<IPillEvent> events, HashSet<Vector2I> destroyed)
    {
        // Chain reactions
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            if (ev is PillDestroyEvent pde)
            {
                for (int j = 0; j < pde.Positions.Length; j++)
                {
                    var pos = pde.Positions[j];
                    var pill = pills[pos];
                    // Add pill to destroyed list.
                    // If already destroyed in this chain reaction, skip
                    if (!destroyed.Add(pos))
                        continue;
                    // Regular pills dont cause other explosions
                    if (pill is RegularPill)
                    {
                        continue;
                    }

                    List<IPillEvent> newEvents = [];
                    // Check if destroying this pill creates more events
                    pill.OnDestroy(this, pos, ref newEvents);
                    if (newEvents.Count > 0)
                    {
                        // Animate those events
                        await SendEvents(newEvents);
                        // Chain react
                        await ChainDestroyPills(newEvents, destroyed);
                        // TODO Check if destroying this pill generates reactions on adjacents cells
                    }
                }
            }
        }
    }

    private async Task ProcessDestruction(List<IPillEvent> events)
    {
        if (events.Count == 0)
            return;

        // Send animation events to UI (destruction)
        await SendEvents(events);

        // Destroy pills
        HashSet<Vector2I> destroyedPositions = [];
        await ChainDestroyPills(events, destroyedPositions);

        // Set empty pills
        foreach (var pos in destroyedPositions)
        {
            pills[pos] = new EmptyPill();
        }
        var deleteEvent = new PillDeleteEvent(destroyedPositions.ToArray());
        EventBus.Publish(deleteEvent); // Delete event is not async



        // Apply gravity
        var gravityEvents = ApplyGravity();
        await SendEvents(gravityEvents);

        // Create new pills
        List<IPillEvent> createEvents = [];
        BoardGenerator.GenerateFillEmptyCells(this, ref createEvents);
        await SendEvents(createEvents);

        // Collect cells that moved
        var creationGravity = createEvents.Where(e => e is PillGravityEvent a).Cast<PillGravityEvent>();
        var movedCells = gravityEvents.Concat(creationGravity);

        // Check matches again on the moved cells
        List<Pattern> newMatchedPatterns = [];
        foreach (var ge in movedCells)
        {
            CheckMatchesOnSwap(ref newMatchedPatterns, ge.ToPosition);
        }

        // Loop until no new matches
        var destroyevents = newMatchedPatterns.Select(p => (IPillEvent) new PillDestroyEvent(p.Cells));
        if (destroyevents.Count() > 0)
            await ProcessDestruction([.. destroyevents]);
    }

    public async Task SendEvents<T>(IEnumerable<T> events) where T : IPillEvent
    {
        var tasks = events.Select(ev => EventBus.PublishAsync(ev));
        await Task.WhenAll(tasks);
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
                foreach (var pos in pde.Positions)
                    pills[pos] = new EmptyPill();

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
                    emptySpaces = 0;
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
