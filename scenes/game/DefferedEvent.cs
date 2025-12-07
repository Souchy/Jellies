using Godot;
using Jellies.src.pills;
using System.Threading.Tasks;

namespace Jellies.game.board_node;

public partial class BoardNode
{
    public partial class DefferedEvent<T>(T ev) : Resource where T : IPillEvent
    {
        public T Event { get; } = ev;
        public TaskCompletionSource Tcs { get; } = new();
    }

}
