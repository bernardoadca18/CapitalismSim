using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Core.Scheduling;
using CapitalismSim.Simulation.Core.State;
using CapitalismSim.Simulation.Core.Time;

namespace CapitalismSim.Simulation.Core.Systems;

/// <summary>
/// A simulation system that processes household needs on a daily basis.
/// Each tick, it withdraws money from household accounts to "pay for" basic needs
/// and records the satisfaction level.
/// </summary>
/// <remarks>
/// <para>
/// This system implements the basic needs consumption model for Phase 2.
/// Households spend money on food, housing, utilities, and healthcare.
/// If a household cannot afford all needs, it spends what it can and records
/// a shortfall.
/// </para>
/// <para>
/// The system runs on a daily cadence. On each tick:
/// 1. For each household, calculate total daily needs based on member count
/// 2. Withdraw the lesser of (needs, account balance) from the household account
/// 3. Record the satisfied amount in HouseholdNeedsState
/// 4. Finalize the day (update consecutive shortfall tracking)
/// 5. Money is destroyed via ExternalDebit (represents consumption)
/// </para>
/// <para>
/// Future phases will add:
/// - Needs varying by household composition (children vs adults)
/// - Price adjustments based on market conditions
/// - Partial fulfillment prioritization (food before luxuries)
/// - Health consequences of chronic shortfalls
/// </para>
/// </remarks>
public sealed class HouseholdNeedsSystem : ISimulationSystem
{
    public SimulationSystemId Id { get; }

    private readonly BasicNeedsProfile _needsProfile;

    /// <summary>
    /// Creates a new HouseholdNeedsSystem with the specified needs profile.
    /// </summary>
    public HouseholdNeedsSystem(BasicNeedsProfile needsProfile)
    {
        _needsProfile = needsProfile;
        Id = new SimulationSystemId("household-needs");
    }

    /// <summary>
    /// Creates a HouseholdNeedsSystem with default needs profile for the specified currency.
    /// </summary>
    public HouseholdNeedsSystem(CurrencyCode currency)
        : this(CreateDefaultNeedsProfile(currency))
    {
    }

    /// <summary>
    /// Processes household needs for one day. Called by the scheduler.
    /// </summary>
    public void Tick(in SimulationTickContext context)
    {
        var state = context.State;
        var population = state.Population;
        var ledger = state.MoneyLedger;

        // Process each household
        foreach (var householdId in population.GetAllHouseholdIds())
        {
            var household = population.GetHousehold(householdId);
            if (household is null)
            {
                continue;
            }

            ProcessHouseholdNeeds(household, ledger, population);
        }
    }

    private void ProcessHouseholdNeeds(Household household, MoneyLedger ledger, PopulationState population)
    {
        int memberCount = household.MemberCount;
        if (memberCount == 0)
        {
            return; // No members, no needs
        }

        Money totalNeeds = _needsProfile.CalculateTotalDaily(memberCount);
        if (totalNeeds.IsZero)
        {
            return; // No needs to satisfy
        }

        AccountId accountId = household.AccountId;
        Money balance = ledger.GetBalance(accountId);

        // Determine how much can be spent (lesser of needs or balance)
        Money amountToSpend = balance < totalNeeds ? balance : totalNeeds;

        if (!amountToSpend.IsZero)
        {
            // Withdraw from household account (money is consumed/destroyed)
            ledger.ExternalDebit(accountId, amountToSpend, MoneyFlowReason.BasicNeeds);
        }

        // Record satisfaction for this household and finalize the day
        RecordAndFinalizeDay(household.Id, amountToSpend, memberCount, population);
    }

    private void RecordAndFinalizeDay(HouseholdId householdId, Money satisfied, int memberCount, PopulationState population)
    {
        var existingState = population.GetHouseholdNeedsState(householdId);
        if (existingState is not null)
        {
            // Record satisfaction and finalize the day
            existingState.RecordSatisfaction(satisfied);
            existingState.FinalizeDay();
        }
        else
        {
            // Create new state, record satisfaction, and finalize the day
            var newState = new HouseholdNeedsState(householdId, _needsProfile, memberCount);
            newState.RecordSatisfaction(satisfied);
            newState.FinalizeDay();
            population.SetHouseholdNeedsState(newState);
        }
    }

    /// <summary>
    /// Creates a default basic needs profile for a currency.
    /// These are placeholder values for Phase 2 testing.
    /// </summary>
    public static BasicNeedsProfile CreateDefaultNeedsProfile(CurrencyCode currency)
    {
        // Default daily costs (in minor units)
        // Derived from typical monthly costs divided by 30
        return new BasicNeedsProfile(
            dailyEssentialCostPerMember: new Money(2500, currency),    // ~$75/day per person (food + healthcare)
            dailyHousingCost: new Money(4000, currency),               // ~$120/day rent (~$3600/month)
            dailyUtilitiesCost: new Money(500, currency)               // ~$15/day utilities
        );
    }
}
