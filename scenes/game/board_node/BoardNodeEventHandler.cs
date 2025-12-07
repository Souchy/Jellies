using Godot;
using Jellies.game.pill_node;
using Jellies.src;
using Jellies.src.pills;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellies.game.board_node;

public partial class BoardNode
{
    #region Async Event Handlers
    private async Task OnCreation(PillCreateEvent ev)
    {
        var wrapper = new DefferedEvent<PillCreateEvent>(ev);
        this.CallDeferred(nameof(OnCreateDeffered), wrapper);
        await wrapper.Tcs.Task;
    }
    private async Task OnGravity(PillGravityEvent ev)
    {
        var wrapper = new DefferedEvent<PillGravityEvent>(ev);
        this.CallDeferred(nameof(OnGravityDeffered), wrapper);
        await wrapper.Tcs.Task;
    }
    private async Task OnDestruction(PillDestroyEvent ev)
    {
        var wrapper = new DefferedEvent<PillDestroyEvent>(ev);
        this.CallDeferred(nameof(OnDestroyDeffered), wrapper);
        await wrapper.Tcs.Task;
    }
    private void OnDelete(PillDeleteEvent ev)
    {
        // Remove nodes from tree & table
        foreach (var pos in ev.Positions)
        {
            var pillNode = PillNodesTable[pos];
            if (pillNode == null)
                continue;
            pillNode.QueueFree();
            PillNodesTable[pos] = null;
        }
    }
    #endregion

    #region Deffered Event Handlers
    private async void OnDestroyDeffered(DefferedEvent<PillDestroyEvent> ev)
    {
        List<Task> tweenTasks = [];
        foreach (var pos in ev.Event.Positions)
        {
            var pill = Board.pills[pos]; // data
            var pillNode = PillNodesTable[pos]; // node
            if (pillNode == null)
                continue;
            var tween = this.CreateTween();
            tween.TweenProperty(pillNode, Node2D.PropertyName.Scale.ToString(), Vector2.Zero, 1f);

            var signalAwaiter = ToSignal(tween, Tween.SignalName.Finished);
            async Task<Variant[]> wrapper() => await signalAwaiter;
            tweenTasks.Add(wrapper());
        }
        await Task.WhenAll(tweenTasks);
        ev.Tcs.SetResult();
    }
    private async void OnCreateDeffered(DefferedEvent<PillCreateEvent> ev)
    {
        var pill = Board.pills[ev.Event.RealPosition];
        var pillnode = GD.Load<PackedScene>("res://game/pill_node/PillNode.tscn").Instantiate<PillNode>();
        var sprite = pill.CreateNode();
        pillnode.AddChild(sprite);
        pillnode.Position = ev.Event.SpawnPosition * Constants.PillSize;
        pillnode.Board = this.Board;
        PillNodesTable[ev.Event.RealPosition] = pillnode;

        // Create shape
        var shapeRid = PhysicsServer2D.RectangleShapeCreate();
        PhysicsServer2D.ShapeSetData(shapeRid, Vector2.One * Constants.PillSize / 2);
        PhysicsServer2D.AreaAddShape(Area2D.GetRid(), shapeRid, new Transform2D(0, ev.Event.RealPosition * Constants.PillSize));
        //int shapeIdx = PhysicsServer2D.AreaGetShapeCount(Area2D.GetRid()) - 1; // TODO: Idk if this always works when destroying/creating new pills
        //pillnode.TreeExited += () => PhysicsServer2D.AreaRemoveShape(Area2D.GetRid(), shapeIdx); // Remove shape on exit

        // Add to tree
        Pills.AddChild(pillnode);

        // Animate scale & opacity
        var targetScale = sprite.Scale;
        sprite.Scale = Vector2.Zero;
        sprite.Modulate = Colors.Transparent;
        var tween = CreateTween().SetProcessMode(Tween.TweenProcessMode.Idle); //GetTree().CreateTween(); //.SetParallel(true);
        tween.TweenProperty(sprite, Node2D.PropertyName.Scale.ToString(), targetScale, 0.1f);
        tween.TweenProperty(sprite, Node2D.PropertyName.Modulate.ToString(), Colors.White, 0.1f);

        // Await tween finish and set task complete
        await ToSignal(tween, Tween.SignalName.Finished);
        ev.Tcs.SetResult();
    }
    private async void OnGravityDeffered(DefferedEvent<PillGravityEvent> ev)
    {
        // Move pill in the table if it's a valid 'from' position (ex: on inputswap, when a match happens below this pill)
        if (PillNodesTable.Has(ev.Event.FromPosition))
        {
            PillNodesTable[ev.Event.ToPosition] = PillNodesTable[ev.Event.FromPosition];
        }
        // Get the node, it's already in the right slot in the table. (ex: on game start, pillnodes start outside the board range)
        var pillNode = PillNodesTable[ev.Event.ToPosition];

        // Animation position
        var deltaPos = ev.Event.ToPosition - ev.Event.FromPosition;
        var animationTime = deltaPos.Y * 0.07f;
        var tween = GetTree().CreateTween();
        tween.TweenProperty(pillNode, Node2D.PropertyName.Position.ToString(), ev.Event.ToPosition.ToVector2() * Constants.PillSize, animationTime)
            .SetTrans(Tween.TransitionType.Bounce)
            .SetEase(Tween.EaseType.Out);

        // Await tween finish and set task complete
        await ToSignal(tween, Tween.SignalName.Finished);
        pillNode.Position = ev.Event.ToPosition.ToVector2() * Constants.PillSize; // Make sure the position is perfect?
        ev.Tcs.SetResult();
    }
    #endregion

}
