using Godot;
using Jellies.game.pill_node;
using Jellies.src;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellies.game.board_node;

public partial class BoardNode
{
    private PillNode? DraggingNode { get; set; }
    private bool IsDragging = false; // => DraggingNode != null;

    // Position where we started dragging
    private Vector2 DragStartPosition;
    private Vector2I DragStartBoardPos => DragStartPosition.ToVector2I() / Constants.PillSize;

    //// New position of the dragged node
    private Vector2 NewPos => GetCurrentMotionPosition();
    private Vector2I NewBoardPos => NewPos.ToVector2I() / Constants.PillSize;

    private bool IsSwapped => NewPos != DragStartPosition;

    #region Global Inputs
    // Fire and forget input task
    public override void _Input(InputEvent @event) => InputAsync(@event);


    public void EnableInput(bool enabled)
    {
        this.SetProcessInput(enabled);
        Area2D.InputPickable = enabled;
    }

    private async void InputAsync(InputEvent @event)
    {
        if (Board == null)
            return;

        var NewPos = GetCurrentMotionPosition();
        var NewBoardPos = NewPos.ToVector2I() / Constants.PillSize;
        bool IsSwapped = NewPos != DragStartPosition;

        if (@event is InputEventMouseButton mouseClickEvent && mouseClickEvent.ButtonIndex == MouseButton.Left)
        {
            // On drag release
            if (!mouseClickEvent.Pressed && IsDragging)
            {
                Vector2 LastPos = DraggingNode.Position;
                Vector2I LastBoardPos = LastPos.ToVector2I() / Constants.PillSize;
                bool WasSwapped = LastPos != DragStartPosition;

                // Reset cursor
                Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
                //GD.Print($"Stop drag {LastBoardPos} from {DragStartBoardPos}");
                LblDebug.Text = $"Stop drag {DragStartBoardPos} to {LastBoardPos}";

                // Stop dragging
                IsDragging = false;
                EnableInput(false);
                if (WasSwapped)
                {
                    //LblDebug.Text += $"\nWas Swapped";
                    // Swap the nodes in the node table too, temporarily
                    (PillNodesTable[DragStartBoardPos], PillNodesTable[LastBoardPos]) = (PillNodesTable[LastBoardPos], PillNodesTable[DragStartBoardPos]);

                    // Check if there's a match and swap the pills in the data board
                    bool matched = await Board.RequestBus.RequestAsync(new InputSwapRequest(DragStartBoardPos, LastBoardPos));

                    // if no match, reset both this node and the swapped node positions
                    if (!matched)
                    {
                        // swap
                        (PillNodesTable[DragStartBoardPos], PillNodesTable[LastBoardPos]) = (PillNodesTable[LastBoardPos], PillNodesTable[DragStartBoardPos]);
                        // reset positions
                        PillNodesTable[DragStartBoardPos].Position = DragStartPosition;
                        PillNodesTable[LastBoardPos].Position = LastPos;
                    }
                    //LblDebug.Text += $"\nMatched = {matched}";
                }
                DraggingNode = null;
                //DragStartPosition = null;
                EnableInput(true);
            }
        }
        else
        // On move + pressed = drag
        if (@event is InputEventMouseMotion mouseMotionEvent && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            // Start dragging if not already
            if (DraggingNode == null)
                return;
            if (!IsDragging)
            {
                //DraggingNode = PillNodesTable[DragStartBoardPos];
                IsDragging = true;
                Input.SetDefaultCursorShape(Input.CursorShape.Drag);
            }
            Vector2 LastPos = DraggingNode.Position;
            Vector2I LastBoardPos = LastPos.ToVector2I() / Constants.PillSize;
            bool WasSwapped = LastPos != DragStartPosition;

            // If mouse is on a new cell:
            //    1. move the node to the new pos
            //    2. move the newNode to the center (startPos)
            //    3. move the lastNode back to its position.
            if (LastPos != NewPos)
            {
                GD.Print($"Move drag {NewBoardPos} from {LastBoardPos} (start {DragStartBoardPos})");
                LblDebug.Text = $"Move drag {LastBoardPos} to {NewBoardPos} (start {DragStartBoardPos})";

                // if coming from center
                if (LastPos == DragStartPosition)
                {
                    DraggingNode.Position = NewPos; // Dragged node goes to new pos
                    PillNodesTable[NewBoardPos].Position = DragStartPosition; // New pos goes to center
                }
                else
                // if going to center
                if (NewPos == DragStartPosition)
                {
                    DraggingNode.Position = NewPos; // Dragged node goes to new pos
                    PillNodesTable[LastBoardPos].Position = LastPos; // Old pill goes back to old pos
                }
                else
                // if going from an adjacent cell to another adjacent cell
                {
                    DraggingNode.Position = NewPos; // Dragged node goes to new pos
                    PillNodesTable[NewBoardPos].Position = DragStartPosition; // New pos goes to center
                    PillNodesTable[LastBoardPos].Position = LastPos; // Old pill goes back to old pos
                }
            }
        }
    }
    #endregion

    #region Area2D collision shape inputs
    private void PillNode_MouseEntered()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.PointingHand);
    }

    private void PillNode_MouseExited()
    {
        if (!IsDragging)
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    private async void PillNode_InputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is InputEventMouseButton mouseClickEvent && mouseClickEvent.ButtonIndex == MouseButton.Left)
        {
            var clickPos = GetMouseGrid2DPos();
            var clickBoardPos = clickPos.ToVector2I() / Constants.PillSize;
            IsDragging = false;
            // On click
            if (mouseClickEvent.Pressed)
            {
                DraggingNode = PillNodesTable[clickBoardPos];
                DragStartPosition = clickPos;
                //Input.SetDefaultCursorShape(Input.CursorShape.Drag);
                LblDebug.Text = $"Press {clickBoardPos} {Board.pills[clickBoardPos]}";
            }
            else
            // On release click
            if (!mouseClickEvent.Pressed && DraggingNode != null)
            {
                LblDebug.Text = $"Click!: {clickBoardPos} {Board.pills[clickBoardPos]}";
                EnableInput(false);
                DraggingNode = null;
                bool ok = await Board.RequestBus.RequestAsync(new InputClickRequest(clickBoardPos));
                EnableInput(true);
            }
        }
    }
    #endregion

    /// <summary>
    /// Returns the Vector2 position of the current motion target. Aka the nearest cell in the drag direction.
    /// </summary>
    private Vector2 GetCurrentMotionPosition()
    {
        var currPos = GetMouseGrid2DPos();
        var delta = currPos - DragStartPosition;
        delta = delta.Normalized();
        bool horizontal = Mathf.Abs(delta.X) > Mathf.Abs(delta.Y);
        if (horizontal)
        {
            delta = Mathf.Sign(delta.X) * Vector2.Right;
        }
        else
        {
            delta = Mathf.Sign(delta.Y) * Vector2.Down;
        }
        delta *= Constants.PillSize;
        Vector2 newPos = DragStartPosition + delta;
        Vector2I newBoardPos = newPos.ToVector2I() / Constants.PillSize;
        int newX = Mathf.Clamp(newBoardPos.X, 0, Board.pills.Width - 1);
        int newY = Mathf.Clamp(newBoardPos.Y, 0, Board.pills.Height - 1);
        newBoardPos = new(newX, newY);
        newPos = newBoardPos * Constants.PillSize;
        return newPos;
    }

    private Vector2 GetMouseGrid2DPos()
    {
        var boardMousePos = Pills.GetLocalMousePosition();
        boardMousePos += Constants.PillVector / 2; // offset by center of pill ex: go from [-32,-32] to [0,0] and [32,32] becomes [64,64]
        var cellLocalMousePos = boardMousePos % Constants.PillSize;
        var coordPos = boardMousePos - cellLocalMousePos;
        return coordPos;
    }

}
