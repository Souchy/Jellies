using Godot;
using Godot.Sharp.Extras;
using Jellies.game.board_node;
using System;
using System.Threading.Tasks;

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

    private async void OnPlayBtnPressed()
    {
        GD.Print("Play button pressed");
        // Start game task and disable input during the process (animations)
        this.SetProcessInput(false);
        await BoardNode.StartGame();
        this.SetProcessInput(true);
    }

}
