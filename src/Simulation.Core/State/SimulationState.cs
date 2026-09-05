using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;

namespace CapitalismSim.Simulation.Core.State;

/// <summary>
/// Root aggregate for all simulation domain state.
/// </summary>
/// <remarks>
/// <para>
/// This class holds the mutable state of the simulation world: the monetary
/// ledger, population state, companies, employment, and other domain aggregates.
/// It is intentionally separated from <see cref="Engine.SimulationEngine"/> to
/// maintain a clear boundary between runtime orchestration and domain state.
/// </para>
/// <para>
/// <strong>Determinism:</strong> All mutations must be deterministic. The state
/// should be reusable across runs with the same seed, producing identical results.
/// </para>
/// <para>
/// <strong>Thread safety:</strong> This class is NOT thread-safe. It is designed
/// for single-threaded access consistent with the project's deterministic kernel
/// architecture (see docs/adr/0003-deterministic-single-threaded-kernel.md).
/// </para>
/// </remarks>
public sealed class SimulationState
{
    /// <summary>
    /// The monetary ledger tracking all accounts and money flows.
    /// </summary>
    public MoneyLedger MoneyLedger { get; }

    /// <summary>
    /// The population state tracking individuals, households, and cohorts.
    /// </summary>
    public PopulationState Population { get; }

    /// <summary>
    /// The companies state tracking all companies and job positions (Phase 3+).
    /// </summary>
    public CompaniesState Companies { get; }

    /// <summary>
    /// The employment state tracking all employment contracts and payroll (Phase 3+).
    /// </summary>
    public EmploymentState Employment { get; }

    /// <summary>
    /// Creates a new empty simulation state with the specified currency.
    /// </summary>
    public SimulationState(CurrencyCode currency)
    {
        MoneyLedger = new MoneyLedger(currency);
        Population = new PopulationState();
        Companies = new CompaniesState();
        Employment = new EmploymentState();
    }

    /// <summary>
    /// Creates a new empty simulation state with the default currency (USD).
    /// </summary>
    public SimulationState() : this(CurrencyCode.USD)
    {
    }

    /// <summary>
    /// Creates a simulation state with the provided components.
    /// </summary>
    public SimulationState(
        MoneyLedger moneyLedger,
        PopulationState population,
        CompaniesState companies,
        EmploymentState employment)
    {
        MoneyLedger = moneyLedger ?? throw new ArgumentNullException(nameof(moneyLedger));
        Population = population ?? throw new ArgumentNullException(nameof(population));
        Companies = companies ?? throw new ArgumentNullException(nameof(companies));
        Employment = employment ?? throw new ArgumentNullException(nameof(employment));
    }

    /// <summary>
    /// Creates a deep copy of this state for snapshot/rollback purposes.
    /// </summary>
    public SimulationState Clone()
    {
        return new SimulationState(
            MoneyLedger.Clone(),
            Population.Clone(),
            Companies.Clone(),
            Employment.Clone());
    }
}
