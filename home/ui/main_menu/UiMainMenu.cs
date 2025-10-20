using Godot;
using Godot.Sharp.Extras;
using System;

namespace Jellies.home.ui.main_menu;

public partial class UiMainMenu : PanelContainer
{

    [NodePath]
    public Label LblAllo { get; set; }
    [NodePath]
    public Button BtnAllo { get; set; }


    public override void _Ready()
    {
        this.OnReady();
        int i = 0;
        BtnAllo.ButtonDown += OnClickBtnAllo;
    }

    public void OnClickBtnAllo()
    {
        LblAllo.Text = "Allo !";
        BtnAllo.ButtonDown -= OnClickBtnAllo;
    }

}
