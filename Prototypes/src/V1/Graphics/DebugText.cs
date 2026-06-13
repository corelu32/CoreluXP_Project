using static SDL.SDL3;
using Prototype.V1.Core;

namespace Prototype.V1.Graphics;

public sealed class DebugText : IDrawable
{
    public string Text { get; set; }
    public int    Left { get; set; }
    public int    Top  { get; set; }

    public DebugText(string text = "", int left = 10, int top = 10)
    {
        Text = text;
        Left = left;
        Top  = top;
    }
    
    public unsafe void Draw(Application target)
    {
        var renderer = target.SdlContext.GetRenderer();

        byte r, g, b, a;
        SDL_GetRenderDrawColor (renderer, &r, &g, &b, &a);
        SDL_SetRenderDrawColor (renderer, 255, 255, 255, 255);
        SDL_RenderDebugText    (renderer, Left, Top, Text);
        SDL_SetRenderDrawColor (renderer, r, g, b, a);
    }
}