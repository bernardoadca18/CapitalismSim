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

    if (options.VerifySaveLoad)
    {
        return RunVerifySaveLoad(options);
    }

    if (options.Phase2)
    {
        return RunPhase2(options);
    }

    if (options.Phase3)
    {
        return RunPhase3(options);
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
    Console.WriteLine();
    Console.WriteLine("Phase 2 State:");
    Console.WriteLine($"  Households:             {run.Engine.State.Population.HouseholdCount}");
    Console.WriteLine($"  Individuals:            {run.Engine.State.Population.IndividualCount}");
    Console.WriteLine($"  Total money supply:     {run.Engine.State.MoneyLedger.GetTotalBalance()}");
    Console.WriteLine($"  Supply accounting:      {(run.Engine.State.MoneyLedger.VerifySupplyAccounting() ? "valid" : "INVALID")}");

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
    Console.WriteLine($"Duration: {options.DurationDays} days");

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
    scenario.Engine.AdvanceBy(SimulationDuration.FromDays(options.DurationDays));
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

static int RunPhase3(HeadlessOptions options)
{
    Console.WriteLine("=== Phase 3 Scenario ===");
    Console.WriteLine($"Seed: {options.Seed}");
    Console.WriteLine($"Duration: {options.DurationDays} days");

    var scenario = Phase3Scenario.Create(options.Seed);
    var summary = scenario.GetSummary();

    Console.WriteLine();
    Console.WriteLine("Initial state:");
    Console.WriteLine($"  Households: {summary.TotalHouseholds}");
    Console.WriteLine($"  Persons: {summary.TotalPersons}");
    Console.WriteLine($"  Companies: {summary.TotalCompanies}");
    Console.WriteLine($"  Job positions: {summary.TotalJobPositions}");
    Console.WriteLine($"  Employment contracts: {summary.TotalContracts}");
    Console.WriteLine($"  Active contracts: {summary.ActiveContracts}");
    Console.WriteLine($"  Household money supply: {summary.TotalHouseholdMoneySupply}");
    Console.WriteLine($"  Company treasury: {summary.TotalCompanyTreasury}");
    Console.WriteLine($"  Supply accounting valid: {summary.SupplyAccountingValid}");

    var sw = Stopwatch.StartNew();
    scenario.AdvanceByDays(options.DurationDays);
    sw.Stop();

    summary = scenario.GetSummary();

    Console.WriteLine();
    Console.WriteLine($"Final state (after {sw.ElapsedMilliseconds}ms):");
    Console.WriteLine($"  Total contracts: {summary.TotalContracts}");
    Console.WriteLine($"  Active contracts: {summary.ActiveContracts}");
    Console.WriteLine($"  Household money supply: {summary.TotalHouseholdMoneySupply}");
    Console.WriteLine($"  Company treasury: {summary.TotalCompanyTreasury}");
    Console.WriteLine($"  Unpaid wages: {summary.TotalUnpaidWages}");
    Console.WriteLine($"  Contracts in arrears: {summary.ContractsInArrears}");
    Console.WriteLine($"  Supply accounting valid: {summary.SupplyAccountingValid}");

    Console.WriteLine();
    Console.WriteLine("Result: PASS");
    return 0;
}

static int RunVerifySaveLoad(HeadlessOptions options)
{
    Console.WriteLine("=== Phase 2 Save/Load Verification ===");
    Console.WriteLine($"Seed: {options.Seed}");
    Console.WriteLine($"Duration: {options.DurationDays} days");
    Console.WriteLine();

    var store = new JsonFileKernelSnapshotStore();

    // Run 1: Continuous run
    var run1 = SimulationRun.Create(options.Seed);
    run1.AdvanceByDays(options.DurationDays);

    // Capture kernel state that IS persisted in snapshots
    var clock1 = run1.Engine.Clock.Current.TotalSeconds;
    var entityIds1 = run1.Engine.EntityIds.NextValue;
    var pop1Count = run1.Engine.State.Population.IndividualCount;
    var hh1Count = run1.Engine.State.Population.HouseholdCount;
    var money1 = run1.Engine.State.MoneyLedger.GetTotalBalance();
    var credits1 = run1.Engine.State.MoneyLedger.TotalExternalCredits;
    var debits1 = run1.Engine.State.MoneyLedger.TotalExternalDebits;

    // Run 2: Split run - first half
    var run2First = SimulationRun.Create(options.Seed);
    run2First.AdvanceByDays(options.DurationDays / 2);

    // Save snapshot
    var snapshotPath = Path.Combine(Path.GetTempPath(), $"verify-saveload-{Guid.NewGuid():N}.json");
    run2First.SaveTo(snapshotPath, store);

    // Load and continue
    var run2Second = SimulationRun.CreateFromSnapshot(snapshotPath, store);
    run2Second.AdvanceByDays(options.DurationDays - (options.DurationDays / 2));

    // Capture kernel state from split run
    var clock2 = run2Second.Engine.Clock.Current.TotalSeconds;
    var entityIds2 = run2Second.Engine.EntityIds.NextValue;
    var pop2Count = run2Second.Engine.State.Population.IndividualCount;
    var hh2Count = run2Second.Engine.State.Population.HouseholdCount;
    var money2 = run2Second.Engine.State.MoneyLedger.GetTotalBalance();
    var credits2 = run2Second.Engine.State.MoneyLedger.TotalExternalCredits;
    var debits2 = run2Second.Engine.State.MoneyLedger.TotalExternalDebits;

    // Cleanup temp file
    File.Delete(snapshotPath);

    // Compare
    Console.WriteLine("Continuous run:");
    Console.WriteLine($"  Clock:            {clock1}s");
    Console.WriteLine($"  Entity IDs:       {entityIds1}");
    Console.WriteLine($"  Households:       {hh1Count}");
    Console.WriteLine($"  Individuals:      {pop1Count}");
    Console.WriteLine($"  Money supply:     {money1}");
    Console.WriteLine($"  External credits: {credits1}");
    Console.WriteLine($"  External debits:  {debits1}");

    Console.WriteLine();
    Console.WriteLine("Split run (save/load):");
    Console.WriteLine($"  Clock:            {clock2}s");
    Console.WriteLine($"  Entity IDs:       {entityIds2}");
    Console.WriteLine($"  Households:       {hh2Count}");
    Console.WriteLine($"  Individuals:      {pop2Count}");
    Console.WriteLine($"  Money supply:     {money2}");
    Console.WriteLine($"  External credits: {credits2}");
    Console.WriteLine($"  External debits:  {debits2}");

    Console.WriteLine();

    bool allMatch = clock1 == clock2
        && entityIds1 == entityIds2
        && pop1Count == pop2Count
        && hh1Count == hh2Count
        && money1.MinorUnits == money2.MinorUnits
        && credits1.MinorUnits == credits2.MinorUnits
        && debits1.MinorUnits == debits2.MinorUnits;

    if (allMatch)
    {
        Console.WriteLine("Result: PASS (all Phase 2 state matches)");
        return 0;
    }

    Console.WriteLine("Result: FAIL (state mismatch)");
    if (clock1 != clock2)
        Console.WriteLine($"  - Clocks differ: {clock1} vs {clock2}");
    if (entityIds1 != entityIds2)
        Console.WriteLine($"  - Entity IDs differ: {entityIds1} vs {entityIds2}");
    if (pop1Count != pop2Count)
        Console.WriteLine($"  - Individual counts differ: {pop1Count} vs {pop2Count}");
    if (hh1Count != hh2Count)
        Console.WriteLine($"  - Household counts differ: {hh1Count} vs {hh2Count}");
    if (money1.MinorUnits != money2.MinorUnits)
        Console.WriteLine($"  - Money supplies differ: {money1} vs {money2}");
    if (credits1.MinorUnits != credits2.MinorUnits)
        Console.WriteLine($"  - External credits differ: {credits1} vs {credits2}");
    if (debits1.MinorUnits != debits2.MinorUnits)
        Console.WriteLine($"  - External debits differ: {debits1} vs {debits2}");

    return 1;
}
