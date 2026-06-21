using LUmaKE.Mathematics;

namespace LUmaKE.Graphics;

public sealed class DebugText : IDrawable
{
    public string       Content  { get; set; } = "";
    public Vector2<int> Position { get; set; }
}