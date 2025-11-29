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

public record struct Pattern(PatternType Type, Vector2I MovedCell, Vector2I[] Cells);

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
    /// When clicking on a pill. If it's a special pill, it will create events and we process them.
    /// </summary>
    /// <param name="req"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private async Task<bool> OnInputClick(InputClickRequest req, CancellationToken token)
    {
        Pill clickedPill = pills[req.pos];
        List<IPillEvent> events = [];
        clickedPill.OnClick(this, req.pos, ref events);

        await ProcessEvents(events);

        return events.Count > 0;
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
            pills[req.newPos].OnSwap(this, req.newPos, pills[req.oldPos], ref events); // Call the combo only on the new position?
            //pills[req.oldPos].OnSwap(this, req.oldPos, pills[req.newPos], ref events);
            // TODO: Process combo events here before continuing actually
            // 1. Spawn dynamite everywhere + process animation (SendEvents)
            // 2. Empty the event list to make place for the next step
            // 3. For each newly spawned dynamite, call OnDestroy(ref events). This will fill the list with PillDestroyEvents. How do we know to call OnDestroy on those cells though? 
            // 4. Continue to accumulate those events with the UpgradeEvents and other PillDestroyEvent from matches below

            // Process matches: create special pills from matches + destroy matched pills
            await ProcessMatches(matchedPatterns);
        }

        return matched1 || matched2;
    }

    /// <summary>
    /// Process matches: create special pills from matches + destroy matched pills, then process events
    /// </summary>
    private async Task ProcessMatches(List<Pattern> matchedPatterns)
    {
        var events = new List<IPillEvent>();

        foreach (var p in matchedPatterns)
        {
            // 1. Destroy cells in matched patterns
            events.Add(new PillDestroyEvent(p.Cells));

            // TODO: Upgrade events? (ex: line4 = dynamite horizontal/vertical)
            // 2. Create special pills for big patterns
            if (p.Cells.Length > 3)
            {
                if (p.Type == PatternType.Square)
                {
                    // TODO: Spawn a square pill! (helicopter/bomb)
                }
                if (p.Type == PatternType.Line)
                {
                    // TODO: Spawn something we dont know yet!
                    // TODO: Use bitwise enum for [0 = square, 1 = linear] + other bits to differentiate vertical/horizontal, T and L shapes...
                    // Make sure the types work with CheckMatchesOnSwap for combining patterns 
                }
            }
        }

        // Process Upgrades + Destruction
        await ProcessEvents(events);
    }

    private async Task ProcessEvents(List<IPillEvent> events)
    {
        if (events.Count == 0)
            return;

        // TODO: Do we process events 1 by 1 like this instead of batching them?
        // That way we can process a PillDestroyEvent fully, then process a PillUpgradeEvent/PillSetEvent, etc. sequentially
        foreach (var ev in events)
        {
            //// Send animation events to UI
            //await SendEvents(events);

            // Send 1 event at a time
            await EventBus.PublishAsync(ev);

            if (ev is PillDestroyEvent destroyEvent)
            {
                // Check for chain explosions
                HashSet<Vector2I> destroyedPositions = [];
                await ChainDestroyPills([destroyEvent], destroyedPositions);

                // Set empty pills
                foreach (var pos in destroyedPositions)
                {
                    pills[pos] = new EmptyPill();
                }
                var deleteEvent = new PillDeleteEvent([.. destroyedPositions]);
                EventBus.Publish(deleteEvent); // Delete event is not async
            }
            // TODO?: Process Upgrade/SetPill events.
            // Still, combo swaps sound hard because it's a whole animation that spawns bombs everywhere and explodes them etc.
            //if(ev is PillUpgradeEvent pu)
            //{
            //}
        }

        // FIXME: 
        // If we send destroy + upgrade in one go, then apply EmptyPill/Delete, then this deletes our new upgraded pills.
        // We need to set the special pill in the table after deleting the old pills. 

        await ApplyAftermath();
    }

    /// <summary>
    /// Checks for chain explosions and destroys more pills if needed recursively.
    /// TODO: 
    /// </summary>
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
                        // ..
                    }
                }
            }
        }
    }

    /// <summary>
    /// Apply gravity, Create new pills, Check matches, Process matches...
    /// </summary>
    private async Task ApplyAftermath()
    {
        // Apply gravity
        var gravityEvents = ApplyGravity();
        await SendEvents(gravityEvents);

        // Create new pills
        List<IPillEvent> createEvents = [];
        BoardGenerator.GenerateFillEmptyCells(this, ref createEvents);
        await SendEvents(createEvents);

        // Collect all cells that moved by gravity
        var creationGravity = createEvents.Where(e => e is PillGravityEvent a).Cast<PillGravityEvent>();
        var movedCells = gravityEvents.Concat(creationGravity);

        // Check matches again on the moved cells
        List<Pattern> newMatchedPatterns = [];
        foreach (var ge in movedCells)
        {
            CheckMatchesOnSwap(ref newMatchedPatterns, ge.ToPosition);
        }
        // Loop until no new matches
        await ProcessMatches(newMatchedPatterns);
    }

    public async Task SendEvents<T>(IEnumerable<T> events) where T : IPillEvent
    {
        var tasks = events.Select(ev => EventBus.PublishAsync(ev));
        await Task.WhenAll(tasks);
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
                matchedPatterns.Add(new Pattern(PatternType.Square, newCell, squareMatches.ToArray()));
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
                // Upgrade square to line (take line instead of square if it has 4 or more cells)
                if (existingPattern.Type == PatternType.Square && lineMatches.Count >= existingPattern.Cells.Length)
                {
                    matchedPatterns[index] = new Pattern(PatternType.Line, newCell, [.. lineMatches]);
                }
                // Combine line with line
                else
                {
                    var combinedCells = existingPattern.Cells.Union(lineMatches).ToArray();
                    matchedPatterns[index] = new Pattern(existingPattern.Type, newCell, combinedCells);
                }
            }
            else
            {
                matchedPatterns.Add(new Pattern(PatternType.Line, newCell, [.. lineMatches]));
            }
        }

        return matchedPatterns.Count > 0;
    }

}
