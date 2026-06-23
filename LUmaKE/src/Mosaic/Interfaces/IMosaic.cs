using LUmaKE.Mathematics;
using LUmaKE.Primitives;

namespace LUmaKE.Mosaic;

public interface IMosaic
{
    bool         Running         { get; }
    string       Title           { get; set; }
    Vector2<int> Position        { get; set; }
    Vector2<int> Size            { get; set; }
    double?      TargetFramerate { get; set; }
    bool         VSyncEnabled    { get; set; }

    void Close();
    void Run();

    event Action?          OnStart;
    event Action?          OnClose;
    event Action<Keycode>? OnKeyDown;
    event Action<double>?  OnUpdate;
    event Action<double>?  OnRender;
}