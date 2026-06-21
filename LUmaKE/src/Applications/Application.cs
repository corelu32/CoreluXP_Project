namespace LUmaKE.Applications;

public enum Platform { SDL }

public sealed class Application
{
    public static IApplication Create(Platform platform)
    {
        return platform switch
        {
            Platform.SDL => new SdlApplication(),
            _ => throw new Exception("Cannot create an application using an unsupported platform.")
        };
    }
}