using Godot;
using Godot.Sharp.Extras;
using Jellies.game.pill_node;
using Jellies.src;
using System;

namespace Jellies.game.board_node;

public partial class BoardNode : Node2D
{

    public Board Board { get; set; }

    public void StartGame()
    {
        Board = BoardGenerator.Generate(difficulty: 1);
        this.RemoveAndQueueFreeChildren();
        foreach(var (i, j, pill) in Board.pills)
        {
            var sprite = pill.CreateNode();

            var pillnode = GD.Load<PackedScene>("res://game/pill_node/PillNode.tscn").Instantiate<PillNode>();
            pillnode.AddChild(sprite);
            pillnode.Position = new Vector2(i, j) * 64;
            this.AddChild(pillnode);
        }
    }

}
