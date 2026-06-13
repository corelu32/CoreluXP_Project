namespace Demo;

public class Program
{
    public static void Main(string[] args)
    {
        var demos = new Dictionary<string, Action>
        {
            ["ProtoV1"] = ProtoV1.Run,
            ["ProtoV2"] = ProtoV2.Run
        };
        
        // Validation handling.
        if (args.Length != 1 || !demos.ContainsKey(args[0]))
        {
            Console.WriteLine("Please provide one argument to run one of these valid demos:");

            foreach (var key in demos.Keys)
                Console.WriteLine($"|__ {key}");

            return;
        }

        demos[args[0]].Invoke();
    }
}