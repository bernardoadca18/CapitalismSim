using System.Globalization;

namespace CapitalismSim.Simulation.Headless.Cli;

/// <summary>
/// Parsed command-line options for the headless runner. Hand-rolled rather
/// than pulling in a command-line parsing library, since the surface area
/// is small and stable (see project brief section 33).
/// </summary>
public sealed class HeadlessOptions
{
    public ulong Seed { get; init; } = 123456UL;

    public int DurationDays { get; init; } = 365;

    public string? SavePath { get; init; }

    public string? LoadPath { get; init; }

    public bool VerifyDeterminism { get; init; }

    public static HeadlessOptions Parse(string[] args)
    {
        ulong seed = 123456UL;
        int durationDays = 365;
        string? savePath = null;
        string? loadPath = null;
        bool verifyDeterminism = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed":
                    seed = ulong.Parse(RequireValue(args, ref i, "--seed"), CultureInfo.InvariantCulture);
                    break;
                case "--duration-days":
                    durationDays = int.Parse(RequireValue(args, ref i, "--duration-days"), CultureInfo.InvariantCulture);
                    break;
                case "--save":
                    savePath = RequireValue(args, ref i, "--save");
                    break;
                case "--load":
                    loadPath = RequireValue(args, ref i, "--load");
                    break;
                case "--verify-determinism":
                    verifyDeterminism = true;
                    break;
                case "--help":
                case "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    throw new ArgumentException($"Unrecognized argument: '{args[i]}'. Use --help for usage.");
            }
        }

        if (durationDays < 0)
        {
            throw new ArgumentException("--duration-days must not be negative.");
        }

        return new HeadlessOptions
        {
            Seed = seed,
            DurationDays = durationDays,
            SavePath = savePath,
            LoadPath = loadPath,
            VerifyDeterminism = verifyDeterminism,
        };
    }

    private static string RequireValue(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Flag '{flag}' requires a value.");
        }

        index++;
        return args[index];
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
            CapitalismSim Simulation Headless

            Usage:
              dotnet run --project src/Simulation.Headless -- [options]

            Options:
              --seed <ulong>            World seed (default: 123456)
              --duration-days <int>     Days of simulated time to advance (default: 365)
              --save <path>             Save a kernel snapshot to <path> after advancing
              --load <path>             Load a kernel snapshot from <path> before advancing
              --verify-determinism      Run the same configuration twice and compare fingerprints
              --help, -h                Show this message
            """);
    }
}
