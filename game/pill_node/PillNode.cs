using Godot;
using System;

namespace Jellies.game.pill_node;

public partial class PillNode : Node2D
{
    public int PillType { get; set; }

    private bool _isDragging;

    public override void _Ready()
    {
        base._Ready();
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        // TODO: Drag event to swap pills
        if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
        {
            GD.Print($"Pill of type {PillType} clicked at position {mouseEvent.Position}");
            // Handle pill click logic here
            if (mouseEvent.Pressed)
            {
                _isDragging = true;
            }
            else
            {
                _isDragging = false;
            }
        }
    }

}
