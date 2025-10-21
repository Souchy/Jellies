using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jellies.src;

public static class Constants
{
    public const int PillSize = 64;
    public static readonly Vector2I PillVector = Vector2I.One * Constants.PillSize;
}
