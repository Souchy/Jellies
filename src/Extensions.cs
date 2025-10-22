using Godot;

namespace Jellies.src;

public static class Extensions
{
    public static Vector2I ToVector2I(this Vector2 vec)
    {
        return new Vector2I((int)vec.X, (int)vec.Y);
    }
}
