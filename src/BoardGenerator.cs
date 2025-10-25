using Godot;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System;
using System.Collections.Generic;

namespace Jellies.src;

internal class BoardGenerator
{
    public static Board Generate(int difficulty, ref List<IPillEvent> events)
    {
        Board board = new()
        {
            pills = new(5, 5, new EmptyPill())
        };
        GenerateFillEmptyCells(board, ref events);
        return board;
    }


    public static void GenerateFillEmptyCells(Board board, ref List<IPillEvent> events)
    {
        TableArray<Pill> tempTable = board.pills.Copy();
        // Assign random pills to positions
        foreach (var (i, j, val) in tempTable)
        {
            if (val is not EmptyPill)
                continue;
            uint rand = GD.Randi() % 100;
            Pill pill = null;
            pill = rand switch
            {
                >= 99 => new DynamitePill(), // 1 % chance
                >= 98 => new BombPill(), // 1 %
                >= 97 => GD.Randi() % 2 == 0 ? new HorizontalPill() : new VerticalPill(), // 1 %
                _ => new RegularPill(RegularPill.GetRandomColor()),
            };
            bool isValid = true;
            int count = 0;
            // Reroll regular color to avoid matches
            if (pill is RegularPill regularPill)
            {
                do
                {
                    var hasMatch = PatternChecker.CheckMatchUpLeft(tempTable, new Vector2I(i, j), regularPill);
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
            tempTable[i, j] = pill;
            events.Add(new PillCreateEvent(new(i, j - board.pills.Height - i), new(i, j))); // create above board
            events.Add(new PillGravityEvent(new(i, j - board.pills.Height - i), new(i, j))); // gravity to position
        }
        // If deadlock, try again
        if (board.CheckIsDeadlock())
        {
            events.Clear();
            GenerateFillEmptyCells(board, ref events);
        }
        else
        // Otherwise, set board table
        {
            board.pills = tempTable;
        }
    }

}
