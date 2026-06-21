namespace LUmaKE.Applications;

public enum Platform { SDL }

public sealed class Application
{
    public static IApplication Create(Platform platform, string title, int width, int height)
    {
        return platform switch
        {
            Platform.SDL => new SdlApplication(title, width, height),
            _ => throw new Exception("Cannot create an application using an unsupported platform.")
        };
    }
}