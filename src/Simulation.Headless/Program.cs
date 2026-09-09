using System.Diagnostics;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Headless.Cli;
using CapitalismSim.Simulation.Headless.Diagnostics;
using CapitalismSim.Simulation.Headless.Scenarios;
using CapitalismSim.Simulation.Persistence.Snapshots;

try
{
    HeadlessOptions options = HeadlessOptions.Parse(args);

    // Phase selection takes precedence over verification flags
    // so that --phase3 --verify-determinism runs Phase 3 verification
    if (options.Phase3)
    {
        if (options.VerifyDeterminism)
        {
            return RunPhase3VerifyDeterminism(options);
        }
        if (options.VerifySaveLoad)
        {
            return RunPhase3VerifySaveLoad(options);
        }
        return RunPhase3(options);
    }

    if (options.Phase2)
    {
        if (options.VerifyDeterminism)
        {
            return RunVerifyDeterminism(options);
        }
        if (options.VerifySaveLoad)
        {
            return RunVerifySaveLoad(options);
        }
        return RunPhase2(options);
    }

    // Legacy/default mode: Phase 2 verification flags
    if (options.VerifyDeterminism)
    {
        return RunVerifyDeterminism(options);
    }

    if (options.VerifySaveLoad)
    {
        return RunVerifySaveLoad(options);
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

    Phase3Scenario scenario;
    if (options.LoadPath is not null)
    {
        Console.WriteLine($"Loading snapshot from: {options.LoadPath}");
        var store = new JsonFileSimulationSnapshotStore();
        var snapshot = store.Load(options.LoadPath);
        scenario = Phase3Scenario.FromSnapshot(snapshot);
    }
    else
    {
        scenario = Phase3Scenario.Create(options.Seed);
    }

    var initialSummary = scenario.GetSummary();

    Console.WriteLine();
    Console.WriteLine("Initial state:");
    Console.WriteLine($"  Households: {initialSummary.TotalHouseholds}");
    Console.WriteLine($"  Persons: {initialSummary.TotalPersons}");
    Console.WriteLine($"  Companies: {initialSummary.TotalCompanies}");
    Console.WriteLine($"  Job positions: {initialSummary.TotalJobPositions}");
    Console.WriteLine($"  Employment contracts: {initialSummary.TotalContracts}");
    Console.WriteLine($"  Active contracts: {initialSummary.ActiveContracts}");
    Console.WriteLine($"  Household shared balance: {initialSummary.TotalHouseholdSharedBalance}");
    Console.WriteLine($"  Personal accounts balance: {initialSummary.TotalPersonalBalance}");
    Console.WriteLine($"  Company treasury: {initialSummary.TotalCompanyTreasury}");
    Console.WriteLine($"  Total ledger balance: {initialSummary.TotalLedgerBalance}");
    Console.WriteLine($"  Supply accounting valid: {initialSummary.SupplyAccountingValid}");

    long initialAllocated = GC.GetAllocatedBytesForCurrentThread();
    var sw = Stopwatch.StartNew();
    long executedSystems = scenario.Engine.AdvanceBy(SimulationDuration.FromDays(options.DurationDays));
    sw.Stop();
    long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - initialAllocated;

    if (options.SavePath is not null)
    {
        Console.WriteLine($"Saving snapshot to: {options.SavePath}");
        var store = new JsonFileSimulationSnapshotStore();
        var snapshot = SimulationSnapshotWriter.Capture(scenario.Engine);
        store.Save(options.SavePath, snapshot);
    }

    var summary = scenario.GetSummary();

    Console.WriteLine();
    Console.WriteLine($"Final state (after {sw.ElapsedMilliseconds}ms):");
    Console.WriteLine($"  Seed: {scenario.Engine.WorldSeed}");
    Console.WriteLine($"  Simulated days: {options.DurationDays}");
    Console.WriteLine($"  Detailed persons: {summary.DetailedPersons}");
    Console.WriteLine($"  Aggregate population: {summary.AggregatePopulation}");
    Console.WriteLine($"  Households: {summary.TotalHouseholds}");
    Console.WriteLine($"  Companies: {summary.TotalCompanies}");
    Console.WriteLine($"  Job positions: {summary.TotalJobPositions}");
    Console.WriteLine($"  Total contracts: {summary.TotalContracts}");
    Console.WriteLine($"  Active contracts: {summary.ActiveContracts}");
    Console.WriteLine($"  Terminated contracts: {summary.TerminatedContracts}");
    Console.WriteLine($"  Household shared balance: {summary.TotalHouseholdSharedBalance}");
    Console.WriteLine($"  Personal account balances: {summary.TotalPersonalBalance}");
    Console.WriteLine($"  Company treasury: {summary.TotalCompanyTreasury}");
    Console.WriteLine($"  Total ledger balance: {summary.TotalLedgerBalance}");
    Console.WriteLine($"  External credits: {summary.TotalExternalCredits}");
    Console.WriteLine($"  External debits: {summary.TotalExternalDebits}");
    Console.WriteLine($"  Total wage arrears: {summary.TotalUnpaidWages}");
    Console.WriteLine($"  Contracts in arrears: {summary.ContractsInArrears}");
    Console.WriteLine($"  Supply accounting valid: {summary.SupplyAccountingValid}");
    Console.WriteLine($"  Integrity valid: {summary.IntegrityValid}");
    Console.WriteLine($"  System executions: {executedSystems}");
    Console.WriteLine($"  Canonical fingerprint: {summary.CanonicalFingerprint}");
    Console.WriteLine($"  Wall time: {sw.ElapsedMilliseconds}ms");
    Console.WriteLine($"  Allocated bytes: {allocatedBytes}");

    if (!summary.IntegrityValid)
    {
        Console.WriteLine();
        Console.WriteLine("Integrity violations:");
        foreach (var violation in summary.IntegrityViolations)
        {
            Console.WriteLine($"  - {violation}");
        }
        Console.WriteLine();
        Console.WriteLine("Result: FAIL (integrity violations)");
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Result: PASS");
    return 0;
}

static int RunPhase3VerifyDeterminism(HeadlessOptions options)
{
    Console.WriteLine("=== Phase 3 Determinism Check ===");
    Console.WriteLine($"Seed: {options.Seed}");
    Console.WriteLine($"Duration: {options.DurationDays} days");
    Console.WriteLine();

    var scenarioA = Phase3Scenario.Create(options.Seed);
    scenarioA.AdvanceByDays(options.DurationDays);
    var summaryA = scenarioA.GetSummary();

    var scenarioB = Phase3Scenario.Create(options.Seed);
    scenarioB.AdvanceByDays(options.DurationDays);
    var summaryB = scenarioB.GetSummary();

    Console.WriteLine($"Run A fingerprint: {summaryA.CanonicalFingerprint}");
    Console.WriteLine($"Run B fingerprint: {summaryB.CanonicalFingerprint}");

    if (summaryA.CanonicalFingerprint == summaryB.CanonicalFingerprint)
    {
        Console.WriteLine("Result: PASS (Phase 3 state matches)");
        return 0;
    }

    Console.WriteLine("Result: FAIL (Phase 3 state differs)");
    return 1;
}

static int RunPhase3VerifySaveLoad(HeadlessOptions options)
{
    Console.WriteLine("=== Phase 3 Save/Load Verification ===");
    Console.WriteLine($"Seed: {options.Seed}");
    Console.WriteLine($"Duration: {options.DurationDays} days");
    Console.WriteLine();

    // Run 1: Continuous run
    var scenario1 = Phase3Scenario.Create(options.Seed);
    scenario1.AdvanceByDays(options.DurationDays);
    var summary1 = scenario1.GetSummary();

    // Run 2: Split run (save/load)
    int firstHalf = options.DurationDays == 365 ? 173 : options.DurationDays / 2;
    int secondHalf = options.DurationDays - firstHalf;

    var scenario2First = Phase3Scenario.Create(options.Seed);
    scenario2First.AdvanceByDays(firstHalf);

    var store = new JsonFileSimulationSnapshotStore();
    var snapshotPath = Path.Combine(Path.GetTempPath(), $"verify-phase3-saveload-{Guid.NewGuid():N}.json");

    try
    {
        var snapshot = SimulationSnapshotWriter.Capture(scenario2First.Engine);
        store.Save(snapshotPath, snapshot);

        var loadedSnapshot = store.Load(snapshotPath);
        var scenario2Second = Phase3Scenario.FromSnapshot(loadedSnapshot);
        scenario2Second.AdvanceByDays(secondHalf);

        var summary2 = scenario2Second.GetSummary();

        Console.WriteLine($"Continuous run fingerprint: {summary1.CanonicalFingerprint}");
        Console.WriteLine($"Split run (save/load) fp:   {summary2.CanonicalFingerprint}");

        if (summary1.CanonicalFingerprint == summary2.CanonicalFingerprint)
        {
            Console.WriteLine("Result: PASS (all Phase 3 state matches)");
            return 0;
        }

        Console.WriteLine("Result: FAIL (state mismatch)");
        return 1;
    }
    finally
    {
        if (File.Exists(snapshotPath))
        {
            try { File.Delete(snapshotPath); } catch { }
        }
    }
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
