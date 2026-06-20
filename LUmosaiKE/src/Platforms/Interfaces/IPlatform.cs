
using LUmosaiKE.Core;

namespace LUmosaiKE.Platforms;

public interface IPlatform
{
    void RegisterWindow     (Window window);
    bool IsWindowRegistered (Window window);
}