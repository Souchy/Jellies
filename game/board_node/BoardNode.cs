using Godot;
using Godot.Sharp.Extras;
using Jellies.game.pill_node;
using Jellies.src;
using Jellies.src.pills;
using Souchy.Godot.structures;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Jellies.game.board_node;

public partial class BoardNode : Node2D
{
    #region Nodes
    [NodePath] public Node2D Pills { get; set; }
    [NodePath] public Label LblDebug { get; set; }
    //[NodePath] 
    public Area2D Area2D { get; set; }
    #endregion

    private Board Board { get; set; }
    private TableArray<PillNode> PillNodesTable { get; set; }

    public override void _Ready()
    {
        this.OnReady();
        this.Area2D = new Area2D()
        {
            InputPickable = true,
            CollisionLayer = 1,
            CollisionMask = 1
        };
        this.AddChild(Area2D);
        this.Area2D.InputEvent += PillNode_InputEvent;
        this.Area2D.MouseEntered += PillNode_MouseEntered;
        this.Area2D.MouseExited += PillNode_MouseExited;
    }

    public async Task StartGame()
    {
        Area2D.InputPickable = false;

        // Clear data, nodes and shapes
        List<IPillEvent> creationEvents = [];
        DraggingNode = null;
        //Board?.OnPillEvent -= OnPillEvent;
        Board = BoardGenerator.Generate(difficulty: 1, ref creationEvents);
        //Board.OnPillEvent += OnPillEvent;
        Board.EventBus.Subscribe(OnDestruction, OnCreation, OnGravity, OnDelete);

        PillNodesTable = new(Board.pills.Width, Board.pills.Height, null);
        Pills.RemoveAndQueueFreeChildren();
        PhysicsServer2D.AreaClearShapes(Area2D.GetRid());

        // Offset board
        var halfBoardOffset = new Vector2(Board.pills.Size.X, Board.pills.Size.Y) * Constants.PillSize / 2f - Vector2.One * Constants.PillSize / 2f;
        this.Position = Vector2.Zero;
        Pills.Position = Vector2.Zero - halfBoardOffset;
        Area2D.Position = Vector2.Zero - halfBoardOffset;
        LblDebug.Position = Vector2.Zero - halfBoardOffset - new Vector2(0, 100);

        // Process creation & gravity events
        var tasks = creationEvents.Select(ev => Board.EventBus.PublishAsync(ev));
        await Task.WhenAll(tasks);

        Area2D.InputPickable = true;
    }

}
