using Godot;
using Jellies.src;
using Jellies.src.pills;
using System;

namespace Jellies.game.pill_node;

public partial class PillNode : Area2D
{
    public Board Board { get; set; }

    public int PillType { get; set; }

    public bool isDragging;
    private Vector2 _dragStartPosition;
    public event Action<PillNode> OnDrag;

    public override void _Ready()
    {
        base._Ready();
        this.InputEvent += PillNode_InputEvent;
        this.MouseEntered += PillNode_MouseEntered;
        this.MouseExited += PillNode_MouseExited;
    }

    private void PillNode_MouseEntered()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
    }

    private void PillNode_MouseExited()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    private void PillNode_InputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseEvent.Pressed)
            {
                if (!isDragging)
                {
                    Input.SetDefaultCursorShape(Input.CursorShape.Drag);
                    isDragging = true;
                    OnDrag(this);
                }
            }
        }
    }

    public Vector2I GetGridPosition()
    {
        return new Vector2I((int) Position.X, (int) Position.Y) / Constants.PillSize;
    }

    public Pill GetPill()
    {
        return Board.pills[GetGridPosition()];
    }

}
