using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Employment;

public class EmploymentStateAndOperationsTests
{
    private readonly PopulationState _population = new();
    private readonly CompaniesState _companies = new();
    private readonly EmploymentState _employment = new();
    private readonly MoneyLedger _ledger = new(CurrencyCode.USD);
    private readonly EntityIdAllocator _entityIds = new();

    private (PersonId PersonId, AccountId PersonalAccountId) CreatePerson(long idVal, long accIdVal)
    {
        var personId = new PersonId(new EntityId((ulong)idVal));
        var householdId = new HouseholdId(new EntityId((ulong)(idVal + 1000)));
        var householdAccId = new AccountId(new EntityId((ulong)(idVal + 2000)));
        var personalAccountId = new AccountId(new EntityId((ulong)accIdVal));

        _population.CreateHousehold(householdId, householdAccId);
        _ledger.CreateAccount(householdAccId, householdId.EntityId);
        _ledger.CreateAccount(personalAccountId, personId.EntityId);

        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        _population.AddPerson(person);

        return (personId, personalAccountId);
    }

    private (CompanyId CompanyId, JobPositionId PositionId) CreateCompanyAndPosition(long compIdVal, long posIdVal, long payAmount = 50000)
    {
        var companyId = new CompanyId(new EntityId((ulong)compIdVal));
        var treasuryId = new AccountId(new EntityId((ulong)(compIdVal + 100)));
        _ledger.CreateAccount(treasuryId, companyId.EntityId);

        var company = new Company(companyId, $"Company-{compIdVal}", treasuryId);
        _companies.CreateCompany(company, _ledger);

        var posId = new JobPositionId(new EntityId((ulong)posIdVal));
        var compensation = new CompensationTerms(payAmount, 30, CurrencyCode.USD);
        var position = new JobPosition(posId, companyId, JobRoleCode.GeneralWorker, compensation);
        _companies.AddPosition(position);

        return (companyId, posId);
    }

    [Fact]
    public void AtomicHire_Succeeds_AndAllocatesIdInternally()
    {
        var (personId, _) = CreatePerson(10, 11);
        var (companyId, positionId) = CreateCompanyAndPosition(20, 21);

        var handler = new HirePersonCommandHandler(_population, _companies, _employment, _ledger, _entityIds);
        long nextIdBefore = _entityIds.NextValue;

        var contractId = handler.Execute(new HirePersonCommand
        {
            PersonId = personId,
            JobPositionId = positionId,
            StartDayIndex = 0,
        });

        Assert.Equal(nextIdBefore, contractId.EntityId.Value);
        Assert.Equal(nextIdBefore + 1, _entityIds.NextValue);
        Assert.Equal(1, _employment.ContractCount);
        Assert.Equal(1, _employment.ActiveContractCount);
        Assert.True(_employment.IsPositionOccupied(positionId));

        var contract = _employment.GetContract(contractId);
        Assert.Equal(personId, contract.PersonId);
        Assert.Equal(companyId, contract.CompanyId);
        Assert.Equal(positionId, contract.JobPositionId);

        var payroll = _employment.GetPayrollState(contractId);
        Assert.NotNull(payroll);
        Assert.Equal(30, payroll.NextPayDayIndex);
    }

    [Fact]
    public void AtomicHire_Failure_DoesNotMutateState_OrConsumeEntityId()
    {
        var (personId, personalAccountId) = CreatePerson(10, 11);
        var (companyId, positionId) = CreateCompanyAndPosition(20, 21);

        var handler = new HirePersonCommandHandler(_population, _companies, _employment, _ledger, _entityIds);

        // 1. Invalid person
        long idCursor = _entityIds.NextValue;
        Assert.Throws<InvalidOperationException>(() => handler.Execute(new HirePersonCommand
        {
            PersonId = new PersonId(new EntityId(9999)),
            JobPositionId = positionId,
            StartDayIndex = 0,
        }));
        Assert.Equal(idCursor, _entityIds.NextValue);
        Assert.Equal(0, _employment.ContractCount);

        // 2. Missing position
        Assert.Throws<InvalidOperationException>(() => handler.Execute(new HirePersonCommand
        {
            PersonId = personId,
            JobPositionId = new JobPositionId(new EntityId(9999)),
            StartDayIndex = 0,
        }));
        Assert.Equal(idCursor, _entityIds.NextValue);
        Assert.Equal(0, _employment.ContractCount);

        // 3. Position already occupied
        handler.Execute(new HirePersonCommand
        {
            PersonId = personId,
            JobPositionId = positionId,
            StartDayIndex = 0,
        });
        Assert.Equal(1, _employment.ContractCount);

        var (person2Id, _) = CreatePerson(50, 51);
        long afterFirstHireCursor = _entityIds.NextValue;

        Assert.Throws<InvalidOperationException>(() => handler.Execute(new HirePersonCommand
        {
            PersonId = person2Id,
            JobPositionId = positionId,
            StartDayIndex = 0,
        }));
        Assert.Equal(afterFirstHireCursor, _entityIds.NextValue);
        Assert.Equal(1, _employment.ContractCount);
    }

    [Fact]
    public void OnePerson_CanHoldMultiplePositions_EvenInSameCompany()
    {
        var (personId, _) = CreatePerson(10, 11);
        var (companyId, pos1) = CreateCompanyAndPosition(20, 21);

        var pos2 = new JobPositionId(new EntityId(22));
        var position2 = new JobPosition(pos2, companyId, JobRoleCode.Specialist, new CompensationTerms(80000, 14, CurrencyCode.USD));
        _companies.AddPosition(position2);

        var handler = new HirePersonCommandHandler(_population, _companies, _employment, _ledger, _entityIds);

        var contract1 = handler.Execute(new HirePersonCommand { PersonId = personId, JobPositionId = pos1, StartDayIndex = 0 });
        var contract2 = handler.Execute(new HirePersonCommand { PersonId = personId, JobPositionId = pos2, StartDayIndex = 0 });

        Assert.Equal(2, _employment.ContractCount);
        Assert.Equal(2, _employment.ActiveContractCount);
        Assert.True(_employment.IsPositionOccupied(pos1));
        Assert.True(_employment.IsPositionOccupied(pos2));
    }

    [Fact]
    public void ContractTermination_ReleasesOccupancy_AndPreservesHistoryAndArrears()
    {
        var (personId, _) = CreatePerson(10, 11);
        var (companyId, posId) = CreateCompanyAndPosition(20, 21);

        var handler = new HirePersonCommandHandler(_population, _companies, _employment, _ledger, _entityIds);
        var contractId = handler.Execute(new HirePersonCommand { PersonId = personId, JobPositionId = posId, StartDayIndex = 0 });

        var payroll = _employment.GetPayrollState(contractId);
        payroll.AccrueUnpaidWages(15000);

        // Invalid termination: end day < start day
        Assert.Throws<ArgumentOutOfRangeException>(() => _employment.TerminateContract(contractId, -1));

        // Valid termination
        _employment.TerminateContract(contractId, 15);

        Assert.False(_employment.IsPositionOccupied(posId));
        Assert.Equal(1, _employment.ContractCount);
        Assert.Equal(0, _employment.ActiveContractCount);

        var contract = _employment.GetContract(contractId);
        Assert.False(contract.IsActive);
        Assert.Equal(15, contract.EndDayIndex);

        // Arrears preserved
        Assert.Equal(15000, payroll.AccruedUnpaidWagesMinorUnits);

        // Double termination fails
        Assert.Throws<InvalidOperationException>(() => _employment.TerminateContract(contractId, 20));

        // Position can be reused by another person
        var (person2Id, _) = CreatePerson(30, 31);
        var contract2Id = handler.Execute(new HirePersonCommand { PersonId = person2Id, JobPositionId = posId, StartDayIndex = 16 });

        Assert.True(_employment.IsPositionOccupied(posId));
        Assert.Equal(2, _employment.ContractCount);
        Assert.Equal(1, _employment.ActiveContractCount);
    }
}
