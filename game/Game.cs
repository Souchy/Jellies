using Godot;
using Godot.Sharp.Extras;
using Jellies.game.board_node;
using System;

namespace Jellies.game;

[Tool]
public partial class Game : Control //Node2D
{
    #region Nodes
    [NodePath] public BoardNode BoardNode { get; set; }
    [NodePath] public Button BtnPlay { get; set; }
    #endregion

    public override void _Ready()
    {
        this.OnReady();
        BtnPlay.Pressed += OnPlayBtnPressed;
    }

    private void OnPlayBtnPressed()
    {
        GD.Print("Play button pressed");
        BoardNode.StartGame();
    }

}
