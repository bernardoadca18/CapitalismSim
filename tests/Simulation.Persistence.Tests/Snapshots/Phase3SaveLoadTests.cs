using CapitalismSim.Simulation.Core.Diagnostics;
using CapitalismSim.Simulation.Headless.Scenarios;
using CapitalismSim.Simulation.Persistence.Snapshots;
using Xunit;

namespace CapitalismSim.Simulation.Persistence.Tests.Snapshots;

public class Phase3SaveLoadTests
{
    [Theory]
    [InlineData(123456UL, 29, 61)]
    [InlineData(123456UL, 30, 60)]
    [InlineData(123456UL, 31, 59)]
    [InlineData(123456UL, 173, 192)]
    [InlineData(987654UL, 30, 60)]
    [InlineData(424242UL, 173, 192)]
    public void Continuous_Versus_SplitSaveLoad_ProducesIdenticalFingerprint(
        ulong seed,
        int firstDuration,
        int secondDuration)
    {
        int totalDuration = firstDuration + secondDuration;

        // 1. Continuous Run
        var continuousScenario = Phase3Scenario.Create(seed);
        continuousScenario.AdvanceByDays(totalDuration);
        string continuousFingerprint = SimulationStateFingerprint.Compute(continuousScenario.Engine);

        // 2. Split Run: First Segment
        var splitScenario = Phase3Scenario.Create(seed);
        splitScenario.AdvanceByDays(firstDuration);

        // Capture full SimulationSnapshotDto
        var snapshot = SimulationSnapshotWriter.Capture(splitScenario.Engine);

        // 3. Split Run: Restore and Second Segment
        var restoredScenario = Phase3Scenario.FromSnapshot(snapshot);
        restoredScenario.AdvanceByDays(secondDuration);
        string splitFingerprint = SimulationStateFingerprint.Compute(restoredScenario.Engine);

        // Fingerprints must match down to the exact bit
        Assert.Equal(continuousFingerprint, splitFingerprint);

        // Detailed state assertions
        var continuousSummary = continuousScenario.GetSummary();
        var splitSummary = restoredScenario.GetSummary();

        Assert.Equal(continuousSummary.TotalContracts, splitSummary.TotalContracts);
        Assert.Equal(continuousSummary.ActiveContracts, splitSummary.ActiveContracts);
        Assert.Equal(continuousSummary.TerminatedContracts, splitSummary.TerminatedContracts);
        Assert.Equal(continuousSummary.ContractsInArrears, splitSummary.ContractsInArrears);
        Assert.Equal(continuousSummary.TotalUnpaidWages.MinorUnits, splitSummary.TotalUnpaidWages.MinorUnits);
        Assert.Equal(continuousSummary.TotalHouseholdSharedBalance.MinorUnits, splitSummary.TotalHouseholdSharedBalance.MinorUnits);
        Assert.Equal(continuousSummary.TotalPersonalBalance.MinorUnits, splitSummary.TotalPersonalBalance.MinorUnits);
        Assert.Equal(continuousSummary.TotalCompanyTreasury.MinorUnits, splitSummary.TotalCompanyTreasury.MinorUnits);
        Assert.Equal(continuousSummary.TotalLedgerBalance.MinorUnits, splitSummary.TotalLedgerBalance.MinorUnits);
        Assert.True(splitSummary.SupplyAccountingValid);
        Assert.True(splitSummary.IntegrityValid);
    }
}
