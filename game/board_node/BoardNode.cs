using Godot;
using Godot.Sharp.Extras;
using Jellies.game.pill_node;
using Jellies.src;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Jellies.game.board_node;

public partial class BoardNode : Node2D
{

    #region Nodes
    [NodePath] public Node2D Pills { get; set; }
    [NodePath] public Label LblDebug { get; set; }
    //[NodePath] 
    public Area2D Area2D { get; set; }
    #endregion

    private Board Board { get; set; }
    private TableArray<PillNode> PillNodesTable { get; set; }
    private PillNode? DraggingNode { get; set; }
    private bool IsDragging => DraggingNode != null;
    private Vector2 dragStartPosition;

    public override void _Ready()
    {
        this.OnReady();
        this.Area2D = new Area2D()
        {
            InputPickable = true,
            CollisionLayer = 1,
            CollisionMask = 1
        };
        this.AddChild(Area2D);
        this.Area2D.InputEvent += PillNode_InputEvent;
        this.Area2D.MouseEntered += PillNode_MouseEntered;
        this.Area2D.MouseExited += PillNode_MouseExited;
    }

    public async Task StartGame()
    {
        Area2D.InputPickable = false;

        // Clear data, nodes and shapes
        List<IPillEvent> creationEvents = [];
        DraggingNode = null;
        Board?.OnPillEvent -= OnPillEvent;
        Board = BoardGenerator.Generate(difficulty: 1, ref creationEvents);
        Board.OnPillEvent += OnPillEvent;
        PillNodesTable = new(Board.pills.Width, Board.pills.Height, null);
        Pills.RemoveAndQueueFreeChildren();
        PhysicsServer2D.AreaClearShapes(Area2D.GetRid());

        // Offset board
        var halfBoardOffset = new Vector2(Board.pills.Size.X, Board.pills.Size.Y) * Constants.PillSize / 2f - Vector2.One * Constants.PillSize / 2f;
        this.Position = Vector2.Zero;
        Pills.Position = Vector2.Zero - halfBoardOffset;
        Area2D.Position = Vector2.Zero - halfBoardOffset;
        LblDebug.Position = Vector2.Zero - halfBoardOffset - new Vector2(0, 100);

        // Process creation events
        IEnumerable<Task> tasks = creationEvents.Select(OnPillEvent);
        await Task.WhenAll(tasks);

        Area2D.InputPickable = true;
    }

    private async Task CreatePillNode(PillCreateEvent ev)
    {
        var pill = Board.pills[ev.RealPosition];
        var pillnode = GD.Load<PackedScene>("res://game/pill_node/PillNode.tscn").Instantiate<PillNode>();
        var sprite = pill.CreateNode();
        pillnode.AddChild(sprite);
        pillnode.Position = ev.SpawnPosition * Constants.PillSize;
        pillnode.Board = this.Board;
        PillNodesTable[ev.RealPosition] = pillnode;

        // Create shape
        var shapeRid = PhysicsServer2D.RectangleShapeCreate();
        PhysicsServer2D.ShapeSetData(shapeRid, Vector2.One * Constants.PillSize / 2);
        PhysicsServer2D.AreaAddShape(Area2D.GetRid(), shapeRid, new Transform2D(0, ev.RealPosition * Constants.PillSize));
        //int shapeIdx = PhysicsServer2D.AreaGetShapeCount(Area2D.GetRid()) - 1; // TODO: Idk if this always works when destroying/creating new pills
        //pillnode.TreeExited += () => PhysicsServer2D.AreaRemoveShape(Area2D.GetRid(), shapeIdx); // Remove shape on exit

        // Add to tree
        Pills.AddChild(pillnode);

        // Animate scale & opacity
        var targetScale = sprite.Scale;
        sprite.Scale = Vector2.Zero;
        sprite.Modulate = Colors.Transparent;
        var tween = GetTree().CreateTween().SetParallel(true);
        tween.TweenProperty(sprite, Node2D.PropertyName.Scale.ToString(), targetScale, 0.1f);
        tween.TweenProperty(sprite, Node2D.PropertyName.Modulate.ToString(), Colors.White, 0.1f);

        // Task to await tween finish
        var tcs = new TaskCompletionSource<bool>();
        tween.Finished += () => tcs.SetResult(true);
        await tcs.Task;
    }

    private async Task OnPillEvent(IPillEvent ev)
    {
        if (ev is PillDestroyEvent destroyEvent)
        {
            // TODO: PillDestroyEvent
            var pillNode = PillNodesTable[destroyEvent.Position];
            if (pillNode == null)
            {
                // FIXME: Shouldn't destroy the same node twice?
                return;
            }
            // Animation + remove node
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Destroy node
            //var anim = pillNode.GetNode<AnimationPlayer>("AnimationPlayer");
            //anim.Play("destroy");
            //anim.AnimationFinished += name =>
            //{
            //    pillNode.QueueFree();
            //    PillNodesTable[destroyEvent.Position] = null;
            //    tcs.SetResult(true);
            //};
            pillNode.QueueFree();
            PillNodesTable[destroyEvent.Position] = null;
            tcs.SetResult(true);
            await tcs.Task;
        }
        else
        if (ev is PillCreateEvent createEvent)
        {
            await CreatePillNode(createEvent);
        }
        else
        if (ev is PillGravityEvent gravityEvent)
        {
            // Move pill in the table if it's a valid 'from' position (ex: on inputswap, when a match happens below this pill)
            if (PillNodesTable.Has(gravityEvent.FromPosition))
            {
                PillNodesTable[gravityEvent.ToPosition] = PillNodesTable[gravityEvent.FromPosition];
            }
            // Get the node, it's already in the right slot in the table. (ex: on game start, pillnodes start outside the board range)
            var pillNode = PillNodesTable[gravityEvent.ToPosition];
            // Animation position
            var tween = GetTree().CreateTween();
            var deltaPos = gravityEvent.ToPosition - gravityEvent.FromPosition;
            var animationTime = deltaPos.Y * 0.07f;
            tween.TweenProperty(pillNode, Node2D.PropertyName.Position.ToString(),
                gravityEvent.ToPosition.ToVector2() * Constants.PillSize, animationTime)
                .SetTrans(Tween.TransitionType.Bounce)
                .SetEase(Tween.EaseType.Out);
            // Task to await tween finish
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            tween.Finished += () =>
            {
                pillNode.Position = gravityEvent.ToPosition.ToVector2() * Constants.PillSize;
                tcs.SetResult(true);
            };
            await tcs.Task;
        }
    }

    private void PillNode_MouseEntered()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
    }

    private void PillNode_MouseExited()
    {
        if (!IsDragging)
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    private void PillNode_InputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton mouseClickEvent && mouseClickEvent.ButtonIndex == MouseButton.Left)
        {
            // On drag start
            if (mouseClickEvent.Pressed && !IsDragging)
            {
                var grid2dPos = GetMouseGrid2DPos();
                var gridPos = grid2dPos.ToVector2I() / Constants.PillSize;
                DraggingNode = PillNodesTable[gridPos];
                dragStartPosition = grid2dPos; //DraggingNode.Position;
                Input.SetDefaultCursorShape(Input.CursorShape.Drag);
                GD.Print($"Start drag {gridPos}");
                //LblDebug.Text = $"Start drag {gridPos}";
                LblDebug.Text = Board.pills[gridPos].ToString();
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (!IsDragging)
            return;
        var lastPos = DraggingNode.Position;
        var lastBoardPos = lastPos.ToVector2I() / Constants.PillSize;
        var newPos = GetCurrentMotionPosition();
        var newBoardPos = newPos.ToVector2I() / Constants.PillSize;
        var dragStartBoardPos = dragStartPosition.ToVector2I() / Constants.PillSize;
        bool wasSwapped = lastPos != dragStartPosition;
        bool isSwapped = newPos != dragStartPosition;
        if (@event is InputEventMouseButton mouseClickEvent && mouseClickEvent.ButtonIndex == MouseButton.Left)
        {
            // On drag release
            if (!mouseClickEvent.Pressed && IsDragging)
            {
                // Reset cursor
                Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
                GD.Print($"Stop drag {lastBoardPos} from {dragStartBoardPos}");
                //LblDebug.Text = $"Stop drag {lastBoardPos} from {dragStartBoardPos}";

                if (wasSwapped)
                {
                    LblDebug.Text += $"\nWas Swapped";
                    // Swap the nodes in the node table too, temporarily
                    (PillNodesTable[dragStartBoardPos], PillNodesTable[lastBoardPos]) 
                        = (PillNodesTable[lastBoardPos], PillNodesTable[dragStartBoardPos]);
                    // Check if there's a match and swap the pills in the data board
                    bool matched = Board.InputSwap(dragStartBoardPos, lastBoardPos);
                    // if no match, reset both this node and the swapped node positions
                    if (!matched)
                    {
                        (PillNodesTable[dragStartBoardPos], PillNodesTable[lastBoardPos]) 
                            = (PillNodesTable[lastBoardPos], PillNodesTable[dragStartBoardPos]);
                        PillNodesTable[dragStartBoardPos].Position = dragStartPosition;
                        PillNodesTable[lastBoardPos].Position = lastPos;
                    }
                    LblDebug.Text += $"\nMatched = {matched}";
                }
                // Stop dragging
                DraggingNode = null;
            }
        }
        else
        // On move 
        if (@event is InputEventMouseMotion mouseMotionEvent && IsDragging)
        {
            // If mouse is on a new cell:
            //    1. move the node to the new pos
            //    2. move the newNode to the center (startPos)
            //    3. move the lastNode back to its position.
            if (lastPos != newPos)
            {
                GD.Print($"Move drag {newBoardPos} from {lastBoardPos}");
                //LblDebug.Text = $"Move drag {newBoardPos} from {lastBoardPos}";

                // if coming from center
                if (lastPos == dragStartPosition)
                {
                    DraggingNode.Position = newPos; // Dragged node goes to new pos
                    PillNodesTable[newBoardPos.X, newBoardPos.Y].Position = dragStartPosition; // New pos goes to center
                }
                else
                // if going to center
                if (newPos == dragStartPosition)
                {
                    DraggingNode.Position = newPos; // Dragged node goes to new pos
                    PillNodesTable[lastBoardPos.X, lastBoardPos.Y].Position = lastPos; // Old pill goes back to old pos
                }
                else
                // if going from an adjacent cell to another adjacent cell
                {
                    DraggingNode.Position = newPos; // Dragged node goes to new pos
                    PillNodesTable[newBoardPos.X, newBoardPos.Y].Position = dragStartPosition; // New pos goes to center
                    PillNodesTable[lastBoardPos.X, lastBoardPos.Y].Position = lastPos; // Old pill goes back to old pos
                }
            }
        }
    }

    private Vector2 GetCurrentMotionPosition()
    {
        var currPos = GetMouseGrid2DPos();
        var delta = currPos - dragStartPosition;
        delta = delta.Normalized();
        bool horizontal = Mathf.Abs(delta.X) > Mathf.Abs(delta.Y);
        if (horizontal)
        {
            delta = Mathf.Sign(delta.X) * Vector2.Right;
        }
        else
        {
            delta = Mathf.Sign(delta.Y) * Vector2.Down;
        }
        delta *= Constants.PillSize;
        Vector2 newPos = dragStartPosition + delta;
        Vector2I newBoardPos = newPos.ToVector2I() / Constants.PillSize;
        int newX = Mathf.Clamp(newBoardPos.X, 0, Board.pills.Width - 1);
        int newY = Mathf.Clamp(newBoardPos.Y, 0, Board.pills.Height - 1);
        newBoardPos = new(newX, newY);
        newPos = newBoardPos * Constants.PillSize;
        return newPos;
    }

    private Vector2 GetMouseGrid2DPos()
    {
        var boardMousePos = Pills.GetLocalMousePosition();
        boardMousePos += Constants.PillVector / 2; // offset by center of pill ex: go from [-32,-32] to [0,0] and [32,32] becomes [64,64]
        var cellLocalMousePos = boardMousePos % Constants.PillSize;
        var coordPos = boardMousePos - cellLocalMousePos;
        return coordPos;
    }

}
