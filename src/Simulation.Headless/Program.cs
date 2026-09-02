using System.Diagnostics;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Headless.Cli;
using CapitalismSim.Simulation.Headless.Diagnostics;
using CapitalismSim.Simulation.Persistence.Snapshots;

try
{
    HeadlessOptions options = HeadlessOptions.Parse(args);

    if (options.VerifyDeterminism)
    {
        return RunVerifyDeterminism(options);
    }

    return RunSingle(options);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
catch (SnapshotFormatException ex)
{
    Console.Error.WriteLine($"Snapshot error: {ex.Message}");
    return 1;
}

static int RunSingle(HeadlessOptions options)
{
    var diagnosticSink = new ConsoleDiagnosticSink();
    var store = new JsonFileKernelSnapshotStore();
    var stopwatch = Stopwatch.StartNew();
    long allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();

    SimulationRun run = options.LoadPath is not null
        ? SimulationRun.CreateFromSnapshot(options.LoadPath, store, diagnosticSink)
        : SimulationRun.Create(options.Seed, diagnosticSink);

    SimulationInstant start = run.Engine.Clock.Current;

    run.AdvanceByDays(options.DurationDays);

    if (options.SavePath is not null)
    {
        run.SaveTo(options.SavePath, store);
    }

    stopwatch.Stop();
    long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;

    Console.WriteLine("CapitalismSim Simulation Headless");
    Console.WriteLine();
    Console.WriteLine($"Seed:                     {run.Engine.WorldSeed}");
    Console.WriteLine($"Start:                    {start}");
    Console.WriteLine($"End:                      {run.Engine.Clock.Current}");
    Console.WriteLine($"Simulated duration:       {run.Engine.Clock.Current.Since(start)}");
    Console.WriteLine($"Systems executed:         {run.Engine.Metrics.SystemExecutionCount}");
    Console.WriteLine($"Events emitted:           {run.Engine.Events.TotalPublishedCount}");
    Console.WriteLine($"Commands dispatched:      {run.Engine.Metrics.CommandCount}");
    Console.WriteLine($"Entities allocated:       {run.Engine.EntityIds.NextValue - 1}");
    Console.WriteLine($"Deterministic fingerprint: {run.ComputeFingerprint()}");
    Console.WriteLine($"Wall time:                {stopwatch.Elapsed}");
    Console.WriteLine($"Allocated bytes:          {allocatedBytes}");

    return 0;
}

static int RunVerifyDeterminism(HeadlessOptions options)
{
    SimulationRun runA = SimulationRun.Create(options.Seed);
    runA.AdvanceByDays(options.DurationDays);
    string fingerprintA = runA.ComputeFingerprint();

    SimulationRun runB = SimulationRun.Create(options.Seed);
    runB.AdvanceByDays(options.DurationDays);
    string fingerprintB = runB.ComputeFingerprint();

    Console.WriteLine("CapitalismSim Determinism Check");
    Console.WriteLine();
    Console.WriteLine($"Seed:          {options.Seed}");
    Console.WriteLine($"Duration days: {options.DurationDays}");
    Console.WriteLine($"Run A fingerprint: {fingerprintA}");
    Console.WriteLine($"Run B fingerprint: {fingerprintB}");

    if (fingerprintA == fingerprintB)
    {
        Console.WriteLine("Result: PASS (fingerprints match)");
        return 0;
    }

    Console.WriteLine("Result: FAIL (fingerprints differ)");
    return 1;
}
