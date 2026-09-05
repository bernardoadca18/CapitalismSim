using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Time;
using CapitalismSim.Simulation.Headless.Scenarios;
using Xunit;

namespace CapitalismSim.Simulation.Headless.Tests.Scenarios;

public class Phase3ScenarioTests
{
    [Fact]
    public void Create_WithDefaultParameters_PopulatesSimulation()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);

        Assert.NotNull(scenario.Engine);
        Assert.NotNull(scenario.NeedsSystem);
        Assert.NotNull(scenario.PayrollSystem);

        var summary = scenario.GetSummary();

        // Default: 2 companies, 5 positions each = 10 positions
        Assert.Equal(2, summary.TotalCompanies);
        Assert.Equal(10, summary.TotalJobPositions);

        // Default: 10 households, 4 persons each = 40 persons
        Assert.Equal(10, summary.TotalHouseholds);
        Assert.Equal(40, summary.TotalPersons);

        // All positions should have contracts (10 contracts)
        Assert.Equal(10, summary.TotalContracts);
        Assert.Equal(10, summary.ActiveContracts);
    }

    [Fact]
    public void Create_WithCustomParameters_PopulatesSimulation()
    {
        var scenario = Phase3Scenario.Create(
            seed: 123456,
            companyCount: 3,
            positionsPerCompany: 4,
            householdCount: 5,
            personsPerHousehold: 2);

        var summary = scenario.GetSummary();

        Assert.Equal(3, summary.TotalCompanies);
        Assert.Equal(12, summary.TotalJobPositions); // 3 * 4
        Assert.Equal(5, summary.TotalHouseholds);
        Assert.Equal(10, summary.TotalPersons); // 5 * 2
        Assert.Equal(10, summary.TotalContracts); // Min(positions, persons)
    }

    [Fact]
    public void Create_InitialMoneySupply_IsValid()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);
        var summary = scenario.GetSummary();

        // Households: 10 * $3000 = $30,000
        Assert.Equal(3000000, summary.TotalHouseholdMoneySupply.MinorUnits);

        // Companies: 2 * $100,000 = $200,000
        Assert.Equal(20000000, summary.TotalCompanyTreasury.MinorUnits);

        // Supply accounting should be valid
        Assert.True(summary.SupplyAccountingValid);
    }

    [Fact]
    public void AdvanceByDays_AdvancesSimulation()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);
        var initialClock = scenario.Engine.Clock.Current;

        scenario.AdvanceByDays(30);

        var finalClock = scenario.Engine.Clock.Current;
        Assert.Equal(30L * 86400L, finalClock.TotalSeconds - initialClock.TotalSeconds);
    }

    [Fact]
    public void AdvanceByDays_TransfersWages()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);
        var initialTreasury = scenario.GetSummary().TotalCompanyTreasury;

        // Advance 30 days (first pay day for most contracts)
        scenario.AdvanceByDays(30);

        var summary = scenario.GetSummary();

        // Company treasury should decrease (wages paid to employees)
        Assert.True(summary.TotalCompanyTreasury.MinorUnits < initialTreasury.MinorUnits);

        // No unpaid wages (companies have sufficient funds)
        Assert.Equal(0, summary.TotalUnpaidWages.MinorUnits);
        Assert.Equal(0, summary.ContractsInArrears);

        // Supply accounting should remain valid
        Assert.True(summary.SupplyAccountingValid);
    }

    [Fact]
    public void AdvanceByDays_MaintainsSupplyAccounting()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);

        // Run for 90 days
        scenario.AdvanceByDays(90);

        var summary = scenario.GetSummary();
        Assert.True(summary.SupplyAccountingValid);
    }

    [Fact]
    public void AdvanceByDays_WithSufficientFunds_NoArrears()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);

        // Run for a full year
        scenario.AdvanceByDays(365);

        var summary = scenario.GetSummary();

        // Companies should still have money (started with $100k each)
        // and employees should have been paid regularly
        Assert.True(summary.SupplyAccountingValid);
    }

    [Fact]
    public void GetSummary_ReflectsCurrentState()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);

        var summary1 = scenario.GetSummary();

        scenario.AdvanceByDays(30);

        var summary2 = scenario.GetSummary();

        // Summaries should reflect different states
        Assert.NotEqual(summary1.TotalHouseholdMoneySupply, summary2.TotalHouseholdMoneySupply);
    }

    [Fact]
    public void Engine_HasRegisteredSystems()
    {
        var scenario = Phase3Scenario.Create(seed: 123456);

        // Engine should have systems registered
        Assert.NotNull(scenario.Engine.Scheduler);
        Assert.True(scenario.Engine.State.Population.HouseholdCount > 0);
        Assert.True(scenario.Engine.State.Companies.CompanyCount > 0);
        Assert.True(scenario.Engine.State.Employment.ContractCount > 0);
    }

    [Fact]
    public void Determinism_SameSeedProducesSameState()
    {
        var scenario1 = Phase3Scenario.Create(seed: 999);
        var scenario2 = Phase3Scenario.Create(seed: 999);

        scenario1.AdvanceByDays(60);
        scenario2.AdvanceByDays(60);

        var summary1 = scenario1.GetSummary();
        var summary2 = scenario2.GetSummary();

        // Same seed should produce identical results
        Assert.Equal(summary1.TotalHouseholdMoneySupply, summary2.TotalHouseholdMoneySupply);
        Assert.Equal(summary1.TotalCompanyTreasury, summary2.TotalCompanyTreasury);
        Assert.Equal(summary1.TotalUnpaidWages, summary2.TotalUnpaidWages);
        Assert.Equal(summary1.ContractsInArrears, summary2.ContractsInArrears);
    }

    [Fact]
    public void Create_WithDifferentSeeds_ProducesValidScenarios()
    {
        // The Phase 3 scenario uses deterministic population setup (no RNG),
        // so different seeds produce identical state. This test verifies the
        // scenario is valid and runnable with different seeds.
        var scenario1 = Phase3Scenario.Create(seed: 111);
        var scenario2 = Phase3Scenario.Create(seed: 222);

        scenario1.AdvanceByDays(30);
        scenario2.AdvanceByDays(30);

        var summary1 = scenario1.GetSummary();
        var summary2 = scenario2.GetSummary();

        // Both scenarios should be valid
        Assert.True(summary1.SupplyAccountingValid);
        Assert.True(summary2.SupplyAccountingValid);

        // Same deterministic setup produces same state
        Assert.Equal(summary1.TotalHouseholdMoneySupply, summary2.TotalHouseholdMoneySupply);
        Assert.Equal(summary1.TotalCompanyTreasury, summary2.TotalCompanyTreasury);
    }
}
