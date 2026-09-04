using System.Diagnostics;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Headless.Cli;
using CapitalismSim.Simulation.Headless.Diagnostics;
using CapitalismSim.Simulation.Headless.Scenarios;
using CapitalismSim.Simulation.Persistence.Snapshots;

try
{
    HeadlessOptions options = HeadlessOptions.Parse(args);

    if (options.VerifyDeterminism)
    {
        return RunVerifyDeterminism(options);
    }

    if (options.Phase2)
    {
        return RunPhase2(options);
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
    Console.WriteLine($"Commands dispatched:      {run.Engine.Commands.TotalDispatchCount}");
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

static int RunPhase2(HeadlessOptions options)
{
    Console.WriteLine($"=== Phase 2 Scenario ===");
    Console.WriteLine($"Seed: {options.Seed}");
    Console.WriteLine($"Duration: {options.DurationDays} days ({options.DurationDays / 30} months)");

    var scenario = Phase2Scenario.Create(options.Seed);
    var summary = scenario.GetSummary();

    Console.WriteLine();
    Console.WriteLine("Initial state:");
    Console.WriteLine($"  Households: {summary.TotalHouseholds}");
    Console.WriteLine($"  Persons: {summary.TotalPersons}");
    Console.WriteLine($"  Cohorts: {summary.TotalCohorts}");
    Console.WriteLine($"  Total money supply: {summary.TotalMoneySupply}");
    Console.WriteLine($"  Avg household balance: {summary.AverageHouseholdBalance}");
    Console.WriteLine($"  Supply accounting valid: {summary.SupplyAccountingValid}");

    var sw = Stopwatch.StartNew();
    int months = Math.Max(1, options.DurationDays / 30);
    scenario.AdvanceByMonths(months);
    sw.Stop();

    summary = scenario.GetSummary();

    Console.WriteLine();
    Console.WriteLine($"Final state (after {sw.ElapsedMilliseconds}ms):");
    Console.WriteLine($"  Households tracked: {summary.HouseholdsTracked}");
    Console.WriteLine($"  Fully satisfied: {summary.FullySatisfiedHouseholds}");
    Console.WriteLine($"  Total money supply: {summary.TotalMoneySupply}");
    Console.WriteLine($"  Avg household balance: {summary.AverageHouseholdBalance}");
    Console.WriteLine($"  Supply accounting valid: {summary.SupplyAccountingValid}");

    Console.WriteLine();
    Console.WriteLine("Result: PASS");
    return 0;
}
