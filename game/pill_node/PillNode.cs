using Godot;
using Jellies.src;
using Jellies.src.pills;
using System;

namespace Jellies.game.pill_node;

public partial class PillNode : Area2D
{
    public Board Board { get; set; }

    public int PillType { get; set; }

    public override void _Ready()
    {
        base._Ready();
        this.InputPickable = false;
    }

    public Vector2I GetBoardPosition()
    {
        return new Vector2I((int) Position.X, (int) Position.Y) / Constants.PillSize;
    }

    public Pill GetPill()
    {
        return Board.pills[GetBoardPosition()];
    }

}
