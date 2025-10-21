using Godot;
using Godot.Sharp.Extras;
using Jellies.game.pill_node;
using Jellies.src;
using System;

namespace Jellies.game.board_node;

public partial class BoardNode : Node2D
{

    #region Nodes
    [NodePath] public Node2D Pills { get; set; }
    #endregion

    private Board Board { get; set; }
    private PillNode? DraggingNode { get; set; }

    public override void _Ready()
    {
        this.OnReady();
    }

    public void StartGame()
    {
        Board = BoardGenerator.Generate(difficulty: 1);
        Pills.RemoveAndQueueFreeChildren();
        var halfBoardOffset = new Vector2(Board.pills.Size.X, Board.pills.Size.Y) * Constants.PillSize / 2f - Vector2.One * Constants.PillSize / 2f;
        this.Position = Vector2.Zero;
        Pills.Position = Vector2.Zero - halfBoardOffset;

        foreach (var (i, j, pill) in Board.pills)
        {
            var sprite = pill.CreateNode();
            var pillnode = GD.Load<PackedScene>("res://game/pill_node/PillNode.tscn").Instantiate<PillNode>();
            pillnode.AddChild(sprite);
            pillnode.Position = new Vector2(i, j) * Constants.PillSize;
            pillnode.Board = this.Board;
            pillnode.OnDrag += (node) =>
            {
                DraggingNode = pillnode;
                DraggingNode.ZIndex = 10;
            };
            pillnode.ZIndex = 0;
            // pillnode.OnExit += (node) => {
            //     this.area.remove(node.shape); // TODO
            // };
            Pills.AddChild(pillnode);
        }
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (DraggingNode?.isDragging != true)
            return;

        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (!mouseEvent.Pressed)
            {
                DraggingNode.isDragging = false;
                DraggingNode = null;
                Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            }
        }
        else
        if (@event is InputEventMouseMotion mouseMotionEvent)
        {
            var boardMousePos = Pills.GetLocalMousePosition();
            boardMousePos += Constants.PillVector / 2; // offset by center of pill ex: go from [-32,-32] to [0,0] and [32,32] becomes [64,64]
            var cellLocalMousePos = boardMousePos % Constants.PillSize;
            var coordPos = boardMousePos - cellLocalMousePos;
            DraggingNode.Position = coordPos;
            DraggingNode.ZIndex = 0;
        }
    }

}
