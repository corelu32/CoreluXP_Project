
using LUmaKE.Core;

namespace LUmaKE.Applications;

public interface IApplication
{
    void AddWindow      (Window window);
    void RemoveWindow   (Window window);
    bool ContainsWindow (Window window);
}