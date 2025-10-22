using Godot;
using Godot.Sharp.Extras;
using Jellies.game.pill_node;
using Jellies.src;
using Souchy.Godot.structures;
using System;

namespace Jellies.game.board_node;

public partial class BoardNode : Node2D
{

    #region Nodes
    [NodePath] public Node2D Pills { get; set; }
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

    public void StartGame()
    {
        // Clear data, nodes and shapes
        Board = BoardGenerator.Generate(difficulty: 1);
        PillNodesTable = new(Board.pills.Width, Board.pills.Height, null);
        Pills.RemoveAndQueueFreeChildren();
        PhysicsServer2D.AreaClearShapes(Area2D.GetRid());

        // Offset board
        var halfBoardOffset = new Vector2(Board.pills.Size.X, Board.pills.Size.Y) * Constants.PillSize / 2f - Vector2.One * Constants.PillSize / 2f;
        this.Position = Vector2.Zero;
        Pills.Position = Vector2.Zero - halfBoardOffset;
        Area2D.Position = Vector2.Zero - halfBoardOffset;

        // New nodes
        foreach (var (i, j, pill) in Board.pills)
        {
            var sprite = pill.CreateNode();
            var pillnode = GD.Load<PackedScene>("res://game/pill_node/PillNode.tscn").Instantiate<PillNode>();
            pillnode.AddChild(sprite);
            pillnode.Position = new Vector2(i, j) * Constants.PillSize;
            pillnode.Board = this.Board;
            //pillnode.OnDrag += (node) =>
            //{
            //    DraggingNode = pillnode;
            //    DraggingNode.ZIndex = 10;
            //};
            pillnode.ZIndex = 0;
            PillNodesTable[i, j] = pillnode;
            // pillnode.OnExit += (node) => {
            //     this.area.remove(node.shape); // TODO
            // };
            // Create shape
            var shapeRid = PhysicsServer2D.RectangleShapeCreate();
            PhysicsServer2D.ShapeSetData(shapeRid, Vector2.One * Constants.PillSize / 2);
            PhysicsServer2D.AreaAddShape(Area2D.GetRid(), shapeRid, new Transform2D(0, pillnode.Position));
            Pills.AddChild(pillnode);
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
                var gridPos = new Vector2I((int) grid2dPos.X, (int) grid2dPos.Y) / Constants.PillSize;
                DraggingNode = PillNodesTable[gridPos];
                dragStartPosition = DraggingNode.Position;
                Input.SetDefaultCursorShape(Input.CursorShape.Drag);
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (!IsDragging)
            return;
        var lastPos = DraggingNode.Position;
        var lastBoardPos = lastPos / Constants.PillSize;
        bool wasSwapped = lastPos != dragStartPosition;
        if (@event is InputEventMouseButton mouseClickEvent && mouseClickEvent.ButtonIndex == MouseButton.Left)
        {
            // On drag release
            if (!mouseClickEvent.Pressed && IsDragging)
            {
                // TODO: Check if there's a match and dont reset if so.
                // reset both this node and the swapped node positions
                DraggingNode.Position = dragStartPosition;
                DraggingNode = null;
                if (wasSwapped)
                {
                    PillNodesTable[(int) lastBoardPos.X, (int) lastBoardPos.Y].Position = lastPos;
                }
                Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
            }
        }
        else
        // On move 
        if (@event is InputEventMouseMotion mouseMotionEvent && IsDragging)
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
            var newPos = dragStartPosition + delta;
            var newBoardPos = newPos / Constants.PillSize;
            int newX = (int) Mathf.Clamp(newBoardPos.X, 0, Board.pills.Width - 1);
            int newY = (int) Mathf.Clamp(newBoardPos.Y, 0, Board.pills.Height - 1);
            newBoardPos = new(newX, newY);
            newPos = newBoardPos * Constants.PillSize;

            // Preview swap
            DraggingNode.Position = newPos;
            PillNodesTable[(int) newBoardPos.X, (int) newBoardPos.Y].Position = dragStartPosition;
            // if we were swapped with another node and we changed position, reset the other node's position
            if (wasSwapped && lastPos != newPos)
            {
                PillNodesTable[(int) lastBoardPos.X, (int) lastBoardPos.Y].Position = lastPos;
            }
        }
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
