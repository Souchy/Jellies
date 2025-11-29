using Godot;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jellies.src;

public static class Extensions
{
    public static Vector2I ToVector2I(this Vector2 vec)
    {
        return new Vector2I((int) vec.X, (int) vec.Y);
    }
    public static Vector2 ToVector2(this Vector2I vec)
    {
        return new Vector2(vec.X, vec.Y);
    }
}
