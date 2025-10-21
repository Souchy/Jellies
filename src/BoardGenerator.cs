using Godot;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System;
using System.Collections.Generic;

namespace Jellies.src;

internal class BoardGenerator
{
    public static Board Generate(int difficulty)
    {
        Board board = new()
        {
            pills = new(5, 5)
        };
        // Assign random pills to positions
        foreach (var (i, j, _) in board.pills)
        {
            uint rand = GD.Randi() % 100;
            Pill pill = null;
            bool isValid = true;
            int count = 0;
            pill = rand switch
            {
                >= 100 => new DynamitePill(), // 1 % chance
                >= 99 => new BombPill(), // 1 %
                >= 98 => GD.Randi() % 2 == 0 ? new HorizontalPill() : new VerticalPill(), // 1 %
                _ => new RegularPill(RegularPill.GetRandomColor()),
            };
            // Reroll regular color to avoid matches
            if (pill is RegularPill regularPill)
            {
                do
                {
                    var hasMatch = PatternChecker.CheckMatchUpLeft(board, new Vector2I(i, j), regularPill);
                    if (isValid == false && !hasMatch)
                    {
                        GD.Print("saved");
                    }
                    isValid = !hasMatch;
                    // If match, increment color
                    if (hasMatch)
                    {
                        int color = (int) regularPill.Color;
                        color += 1;
                        color %= Enum.GetValues<RegularPillColor>().Length;
                        var finalColor = (RegularPillColor) color;
                        //var color2 = rp.Color + 1;
                        regularPill = new RegularPill(finalColor);
                        count++;
                    }
                } while (!isValid && count < Enum.GetValues<RegularPillColor>().Length);
                pill = regularPill;
            }
            if (!isValid)
            {
                GD.Print($"Couldnt get a good pill at pos ({i}, {j})");
            }
            board.pills[i, j] = pill;
        }
        if(board.CheckIsDeadlock())
        {
            return Generate(difficulty);
        }
        return board;
    }

}
