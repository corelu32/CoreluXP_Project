using Tot3D.Primitives;
using Veldrid;

namespace Tot3D.Applications;

public interface IApplication
{
    PlatformType PlatformType { get; }

    event Action<float> Rendering;
    event Action Resized;
    event Action<KeyEvent> KeyPressed;

    uint Width { get; }
    uint Height { get; }

    void Run();
}