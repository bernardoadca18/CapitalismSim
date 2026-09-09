using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Headless.Scenarios;
using CapitalismSim.Simulation.Persistence.Snapshots;
using Xunit;

namespace CapitalismSim.Simulation.Persistence.Tests.Snapshots;

public class CorruptedSnapshotTests
{
    private SimulationSnapshotDto CreateValidSnapshot()
    {
        var scenario = Phase3Scenario.Create(123456);
        scenario.AdvanceByDays(30);
        return SimulationSnapshotWriter.Capture(scenario.Engine);
    }

    private SimulationEngine CreateMatchingEngine(SimulationSnapshotDto snapshot)
    {
        return new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = snapshot.Kernel.WorldSeed,
            Currency = new CurrencyCode(snapshot.MoneyLedger!.Currency),
        });
    }

    [Fact]
    public void FormatVersionMismatch_Throws()
    {
        var snapshot = CreateValidSnapshot();
        var corrupted = snapshot with { FormatVersion = 999 };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, corrupted));
    }

    [Fact]
    public void ZeroOrNegativeEntityId_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Set PersonId to 0
        snapshot.Population!.Individuals[0] = snapshot.Population.Individuals[0] with { PersonId = 0 };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void EntityIdCollision_BetweenDomains_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Give a Company the same EntityId as an individual Person
        long personId = snapshot.Population!.Individuals[0].PersonId;
        snapshot.Companies!.Companies[0] = snapshot.Companies.Companies[0] with { CompanyId = personId };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void AllocatorCursor_NotGreaterThanMaxDeclared_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Set NextEntityId lower than max declared ID
        snapshot.Kernel = snapshot.Kernel with { NextEntityId = 5 };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void TreasuryAccount_WrongOwner_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Point TreasuryAccountId to account owned by a person
        long personAccId = snapshot.Population!.Individuals[0].PersonalAccountId;
        snapshot.Companies!.Companies[0] = snapshot.Companies.Companies[0] with { TreasuryAccountId = personAccId };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void PersonalAccount_WrongOwner_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Point person PersonalAccountId to household account
        long householdAccId = snapshot.Population!.Households[0].AccountId;
        snapshot.Population.Individuals[0] = snapshot.Population.Individuals[0] with { PersonalAccountId = householdAccId };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void ZeroPayPosition_Throws()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Companies!.JobPositions[0] = snapshot.Companies.JobPositions[0] with { PayAmountMinorUnits = 0 };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void CurrencyMismatch_Throws()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Companies!.JobPositions[0] = snapshot.Companies.JobPositions[0] with { Currency = "EUR" };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void Contract_EndDayBeforeStartDay_Throws()
    {
        var snapshot = CreateValidSnapshot();
        snapshot.Employment!.Contracts[0] = snapshot.Employment.Contracts[0] with { StartDayIndex = 50, EndDayIndex = 20 };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void MultipleActiveContracts_OnSamePosition_Throws()
    {
        var snapshot = CreateValidSnapshot();
        long activePosId = snapshot.Employment!.Contracts[0].JobPositionId;
        // Make second active contract occupy the exact same position
        snapshot.Employment.Contracts[1] = snapshot.Employment.Contracts[1] with { JobPositionId = activePosId, EndDayIndex = -1 };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void Contract_MissingPayroll_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Remove payroll for contract 0
        long contractId = snapshot.Employment!.Contracts[0].EmploymentContractId;
        snapshot.Employment.PayrollStates.RemoveAll(p => p.EmploymentContractId == contractId);
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void OrphanPayroll_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Add payroll for non-existent contract
        snapshot.Employment!.PayrollStates.Add(new PayrollStateSnapshotDto
        {
            EmploymentContractId = 99999,
            AccruedUnpaidWagesMinorUnits = 0,
            ConsecutiveMissedPayPeriods = 0,
            NextPayDayIndex = 30,
        });
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void DuplicatePayroll_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Duplicate first payroll entry
        snapshot.Employment!.PayrollStates.Add(snapshot.Employment.PayrollStates[0]);
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }

    [Fact]
    public void MoneySupplyAccountingMismatch_Throws()
    {
        var snapshot = CreateValidSnapshot();
        // Alter account balance without altering credits/debits
        snapshot.MoneyLedger!.Accounts[0] = snapshot.MoneyLedger.Accounts[0] with
        {
            BalanceMinorUnits = snapshot.MoneyLedger.Accounts[0].BalanceMinorUnits + 10000
        };
        var engine = CreateMatchingEngine(snapshot);

        Assert.Throws<SnapshotFormatException>(() => SimulationSnapshotReader.Apply(engine, snapshot));
    }
}
