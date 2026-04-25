public class Program
{
    private static readonly Dictionary<string, Action> Demos = new()
    {
        ["GettingStarted"] = GettingStarted.Demo.Run
    };

    public static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            InvalidDemo();
            return;
        }

        Demos.TryGetValue(args[0], out var action);
        (action ?? InvalidDemo).Invoke();
    }

    public static void InvalidDemo()
        => Console.WriteLine($"\nInvalid demo specified - please choose one of the following:\n  |__ {string.Join("\n  |__ ", Demos.Keys)}\n");
}