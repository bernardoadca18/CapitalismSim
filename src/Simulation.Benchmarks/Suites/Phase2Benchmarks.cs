using BenchmarkDotNet.Attributes;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Engine;
using CapitalismSim.Simulation.Core.Population;
using CapitalismSim.Simulation.Persistence.Snapshots;

namespace CapitalismSim.Simulation.Benchmarks.Suites;

/// <summary>
/// Benchmarks for Phase 2 systems: population and money ledger operations.
/// </summary>
[MemoryDiagnoser]
public class Phase2Benchmarks
{
    private SimulationEngine _engine = null!;
    private HouseholdId _householdId;
    private AccountId _accountId;
    private PersonId _personId;
    private KernelSnapshotDto _snapshot = null!;

    [Params(100, 1000, 10000)]
    public int EntityCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _engine = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 42UL,
            Currency = CurrencyCode.USD,
        });

        var population = _engine.State.Population;
        var ledger = _engine.State.MoneyLedger;

        // Create households with accounts
        for (int i = 0; i < EntityCount; i++)
        {
            var householdId = new HouseholdId(_engine.EntityIds.Allocate());
            var accountId = new AccountId(_engine.EntityIds.Allocate());

            population.CreateHousehold(householdId, accountId);
            ledger.CreateAccount(accountId, householdId.EntityId);
            ledger.ExternalCredit(accountId, new Money(500000, CurrencyCode.USD), MoneyFlowReason.InitialEndowment);
        }

        // Store references for transfer benchmarks
        var householdIds = population.GetAllHouseholdIds().Take(2).ToList();
        _householdId = householdIds[0];
        var firstHousehold = population.GetHousehold(_householdId);
        _accountId = firstHousehold!.AccountId;

        _personId = new PersonId(_engine.EntityIds.Allocate());
        var personalAccountId = new AccountId(_engine.EntityIds.Allocate());
        ledger.CreateAccount(personalAccountId, _personId.EntityId);
        var person = Person.WithAge(_personId, _householdId, 25, referenceSimulationDay: 0, personalAccountId);
        population.AddPerson(person);

        _snapshot = KernelSnapshotWriter.Capture(_engine);
    }

    [Benchmark]
    public void CreateHousehold()
    {
        var householdId = new HouseholdId(_engine.EntityIds.Allocate());
        var accountId = new AccountId(_engine.EntityIds.Allocate());
        _engine.State.Population.CreateHousehold(householdId, accountId);
    }

    [Benchmark]
    public void AddPerson()
    {
        var personId = new PersonId(_engine.EntityIds.Allocate());
        var person = Person.WithAge(personId, _householdId, 30, referenceSimulationDay: 0, AccountId.None);
        _engine.State.Population.AddPerson(person);
    }

    [Benchmark]
    public void Transfer()
    {
        var householdIds = _engine.State.Population.GetAllHouseholdIds().Take(2).ToList();
        var from = _engine.State.Population.GetHousehold(householdIds[0])!.AccountId;
        var to = _engine.State.Population.GetHousehold(householdIds[1])!.AccountId;
        _engine.State.MoneyLedger.Transfer(from, to, new Money(100, CurrencyCode.USD));
    }

    [Benchmark]
    public void GetBalance()
    {
        _engine.State.MoneyLedger.GetBalance(_accountId);
    }

    [Benchmark]
    public void GetHousehold()
    {
        _engine.State.Population.GetHousehold(_householdId);
    }

    [Benchmark]
    public void GetPerson()
    {
        _engine.State.Population.GetPerson(_personId);
    }

    [Benchmark]
    public KernelSnapshotDto CaptureSnapshot() => KernelSnapshotWriter.Capture(_engine);

    [Benchmark]
    public void ApplySnapshot()
    {
        var target = new SimulationEngine(new SimulationEngineOptions
        {
            WorldSeed = 42UL,
            Currency = CurrencyCode.USD,
        });
        KernelSnapshotReader.Apply(target, _snapshot);
    }
}

/// <summary>
/// Benchmarks for large population operations (LOD scenarios).
/// </summary>
[MemoryDiagnoser]
public class LargePopulationBenchmarks
{
    private SimulationEngine _engine = null!;

    [Params(100000, 1000000)]
    public int CohortCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _engine = new SimulationEngine(new SimulationEngineOptions { WorldSeed = 42UL });

        var population = _engine.State.Population;

        // Create cohorts (aggregate population)
        for (int i = 0; i < CohortCount; i++)
        {
            var cohortId = new PopulationCohortId(_engine.EntityIds.Allocate());
            var cohort = new PopulationCohort(cohortId, count: 1000L, HouseholdId.None);
            population.SetCohort(cohort);
        }
    }

    [Benchmark]
    public long GetTotalPopulation()
    {
        return _engine.State.Population.TotalPopulation;
    }

    [Benchmark]
    public int GetCohortCount()
    {
        return _engine.State.Population.CohortCount;
    }
}
