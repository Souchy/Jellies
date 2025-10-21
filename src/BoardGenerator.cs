using Godot;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System;

namespace Jellies.src;

internal class BoardGenerator
{
    public static Board Generate(int difficulty)
    {
        Board board = new()
        {
            pills = new(8, 4)
        };
        // Assign random pills to positions
        foreach (var (i, j, _) in board.pills)
        {
            uint rand = GD.Randi() % 100;
            Pill pill = rand switch
            {
                < 70 => new RegularPill(RegularPill.GetRandomColor()),
                < 85 => GD.Randi() % 2 == 0 ? new HorizontalPill() : new VerticalPill(),
                < 95 => new BombPill(),
                _ => new DynamitePill(),
            };
            board.pills[i, j] = pill;
        }
        return board;
    }

}
