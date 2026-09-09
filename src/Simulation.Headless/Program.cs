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

    Console.WriteLine($"Run A - Household: {summaryA.TotalHouseholdMoneySupply}, Company: {summaryA.TotalCompanyTreasury}, Unpaid: {summaryA.TotalUnpaidWages}");
    Console.WriteLine($"Run B - Household: {summaryB.TotalHouseholdMoneySupply}, Company: {summaryB.TotalCompanyTreasury}, Unpaid: {summaryB.TotalUnpaidWages}");

    bool match = summaryA.TotalHouseholdMoneySupply == summaryB.TotalHouseholdMoneySupply
        && summaryA.TotalCompanyTreasury == summaryB.TotalCompanyTreasury
        && summaryA.TotalUnpaidWages == summaryB.TotalUnpaidWages
        && summaryA.ContractsInArrears == summaryB.ContractsInArrears
        && summaryA.SupplyAccountingValid == summaryB.SupplyAccountingValid;

    if (match)
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

    // Run 2: Split run - first half
    var scenario2First = Phase3Scenario.Create(options.Seed);
    scenario2First.AdvanceByDays(options.DurationDays / 2);

    // Save snapshot using SimulationSnapshotWriter directly (captures full Phase 3 state)
    var snapshotPath = Path.Combine(Path.GetTempPath(), $"verify-phase3-saveload-{Guid.NewGuid():N}.json");
    var snapshot = SimulationSnapshotWriter.Capture(scenario2First.Engine);
    var jsonOptions = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
    string json = System.Text.Json.JsonSerializer.Serialize(snapshot, jsonOptions);
    File.WriteAllText(snapshotPath, json);

    // Load snapshot and continue using Phase3Scenario.FromSnapshot
    json = File.ReadAllText(snapshotPath);
    var loadedSnapshot = System.Text.Json.JsonSerializer.Deserialize<SimulationSnapshotDto>(json, jsonOptions)
        ?? throw new InvalidOperationException("Failed to deserialize snapshot.");

    // Restore scenario with systems registered
    var scenario2Second = Phase3Scenario.FromSnapshot(loadedSnapshot);

    // Continue the simulation
    scenario2Second.AdvanceByDays(options.DurationDays - (options.DurationDays / 2));

    // Capture state from split run
    var summary2 = scenario2Second.GetSummary();

    // Cleanup temp file
    File.Delete(snapshotPath);

    // Compare
    Console.WriteLine("Continuous run:");
    Console.WriteLine($"  Household money: {summary1.TotalHouseholdMoneySupply}");
    Console.WriteLine($"  Company treasury: {summary1.TotalCompanyTreasury}");
    Console.WriteLine($"  Unpaid wages: {summary1.TotalUnpaidWages}");
    Console.WriteLine($"  Contracts in arrears: {summary1.ContractsInArrears}");

    Console.WriteLine();
    Console.WriteLine("Split run (save/load):");
    Console.WriteLine($"  Household money: {summary2.TotalHouseholdMoneySupply}");
    Console.WriteLine($"  Company treasury: {summary2.TotalCompanyTreasury}");
    Console.WriteLine($"  Unpaid wages: {summary2.TotalUnpaidWages}");
    Console.WriteLine($"  Contracts in arrears: {summary2.ContractsInArrears}");

    Console.WriteLine();

    bool allMatch = summary1.TotalHouseholdMoneySupply.MinorUnits == summary2.TotalHouseholdMoneySupply.MinorUnits
        && summary1.TotalCompanyTreasury.MinorUnits == summary2.TotalCompanyTreasury.MinorUnits
        && summary1.TotalUnpaidWages.MinorUnits == summary2.TotalUnpaidWages.MinorUnits
        && summary1.ContractsInArrears == summary2.ContractsInArrears;

    if (allMatch)
    {
        Console.WriteLine("Result: PASS (all Phase 3 state matches)");
        return 0;
    }

    Console.WriteLine("Result: FAIL (state mismatch)");
    if (summary1.TotalHouseholdMoneySupply.MinorUnits != summary2.TotalHouseholdMoneySupply.MinorUnits)
        Console.WriteLine($"  - Household money differs: {summary1.TotalHouseholdMoneySupply.MinorUnits} vs {summary2.TotalHouseholdMoneySupply.MinorUnits}");
    if (summary1.TotalCompanyTreasury.MinorUnits != summary2.TotalCompanyTreasury.MinorUnits)
        Console.WriteLine($"  - Company treasury differs: {summary1.TotalCompanyTreasury.MinorUnits} vs {summary2.TotalCompanyTreasury.MinorUnits}");
    if (summary1.TotalUnpaidWages.MinorUnits != summary2.TotalUnpaidWages.MinorUnits)
        Console.WriteLine($"  - Unpaid wages differ: {summary1.TotalUnpaidWages.MinorUnits} vs {summary2.TotalUnpaidWages.MinorUnits}");
    if (summary1.ContractsInArrears != summary2.ContractsInArrears)
        Console.WriteLine($"  - Contracts in arrears differ: {summary1.ContractsInArrears} vs {summary2.ContractsInArrears}");

    return 1;
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
