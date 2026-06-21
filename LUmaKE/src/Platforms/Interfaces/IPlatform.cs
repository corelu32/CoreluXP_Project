
using LUmaKE.Core;

namespace LUmaKE.Platforms;

public interface IPlatform
{
    void RegisterWindow     (Window window);
    void UnregisterWindow   (Window window);
    bool IsWindowRegistered (Window window);
}