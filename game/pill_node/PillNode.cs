using Godot;
using Jellies.src;
using Jellies.src.pills;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellies.game.pill_node;

public partial class PillNode : Area2D
{
    public Board Board { get; set; }

    //public int PillType { get; set; }

    // movement settings
    public float FallSpeed = 600f; // pixels per second
    public float ArrivalThreshold = 2f; // pixels

    public Vector2 FallToPosition { get; set; }
    public bool IsFalling { get; set; } = false;

    private CancellationTokenRegistration? _ctReg;
    private TaskCompletionSource<bool>? _moveTcs;

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

    //public void Fall(Vector2 targetPos)
    //{
    //    IsFalling = true;
    //    FallToPosition = targetPos;
    //}

    public override void _Process(double delta)
    {
        base._Process(delta);
        if(IsFalling)
        {
            this.Position = this.Position.MoveToward(FallToPosition, (float)(64 * delta));
            if (this.Position.DistanceSquaredTo(FallToPosition) < ArrivalThreshold * ArrivalThreshold)
            {
                this.Position = FallToPosition;
                IsFalling = false;
                EndMovement(success: true);
            }
        }
    }
    // Start a fall to a world position and return a Task that completes when the pill reaches it.
    // Optional CancellationToken lets callers cancel the motion.
    public Task PlayFallAsync(Vector2 worldTarget, CancellationToken cancellation = default)
    {
        // If already moving, cancel previous move (change behaviour if you prefer to queue or wait)
        AbortMovement(cancellation);

        FallToPosition = worldTarget;
        IsFalling = true;

        // create tcs with RunContinuationsAsynchronously to avoid running continuations inline
        _moveTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (cancellation.CanBeCanceled)
        {
            _ctReg = cancellation.Register(() =>
            {
                // safe-cancel from the main thread or elsewhere
                _moveTcs?.TrySetCanceled();
            });
        }

        return _moveTcs.Task;
    }

    // If you ever need to forcibly stop and cleanup
    public void AbortMovement(CancellationToken cancellation = default)
    {
        if (IsFalling)
        {
            IsFalling = false;
            _moveTcs?.TrySetCanceled(cancellation);
            _moveTcs = null;
            _ctReg?.Dispose();
        }
    }

    private void EndMovement(bool success)
    {
        IsFalling = false;
        if (_moveTcs is not null)
        {
            if (success)
                _moveTcs.TrySetResult(true);
            else
                _moveTcs.TrySetCanceled();
            _moveTcs = null;
        }
        _ctReg?.Dispose();
    }

}
