using CapitalismSim.Simulation.Core.Companies;
using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using CapitalismSim.Simulation.Core.Employment;
using CapitalismSim.Simulation.Core.Population;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Companies;

public class CompaniesStateTests
{
    private readonly MoneyLedger _ledger = new(CurrencyCode.USD);
    private readonly CompaniesState _companies = new();
    private readonly EmploymentState _employment = new();
    private readonly PopulationState _population = new();

    [Fact]
    public void CreateCompany_WithValidParameters_Succeeds()
    {
        var companyId = new CompanyId(new EntityId(10));
        var treasuryId = new AccountId(new EntityId(11));
        _ledger.CreateAccount(treasuryId, companyId.EntityId);

        var company = new Company(companyId, "Acme Corp", treasuryId);
        _companies.CreateCompany(company, _ledger);

        Assert.True(_companies.CompanyExists(companyId));
        Assert.Equal(1, _companies.CompanyCount);
        Assert.Same(company, _companies.GetCompany(companyId));
    }

    [Fact]
    public void CreateCompany_WithInvalidParameters_Throws()
    {
        var companyId = new CompanyId(new EntityId(10));
        var treasuryId = new AccountId(new EntityId(11));

        // Company constructor validation
        Assert.Throws<ArgumentException>(() => new Company(CompanyId.None, "Valid Name", treasuryId));
        Assert.Throws<ArgumentException>(() => new Company(companyId, "", treasuryId));
        Assert.Throws<ArgumentException>(() => new Company(companyId, "   ", treasuryId));
        Assert.Throws<ArgumentException>(() => new Company(companyId, new string('a', 200), treasuryId));
        Assert.Throws<ArgumentException>(() => new Company(companyId, "Valid Name", AccountId.None));

        // State registration validation
        var validCompany = new Company(companyId, "Acme Corp", treasuryId);
        // Treasury account not yet in ledger
        Assert.Throws<InvalidOperationException>(() => _companies.CreateCompany(validCompany, _ledger));

        _ledger.CreateAccount(treasuryId, companyId.EntityId);
        _companies.CreateCompany(validCompany, _ledger);

        // Duplicate company
        Assert.Throws<InvalidOperationException>(() => _companies.CreateCompany(validCompany, _ledger));
    }

    [Fact]
    public void AddPosition_And_RemovePosition_EnforcesOccupancyInvariant()
    {
        var companyId = new CompanyId(new EntityId(10));
        var treasuryId = new AccountId(new EntityId(11));
        _ledger.CreateAccount(treasuryId, companyId.EntityId);
        var company = new Company(companyId, "Acme Corp", treasuryId);
        _companies.CreateCompany(company, _ledger);

        var posId = new JobPositionId(new EntityId(20));
        var comp = new CompensationTerms(50000, 30, CurrencyCode.USD);
        var position = new JobPosition(posId, companyId, JobRoleCode.GeneralWorker, comp);

        _companies.AddPosition(position);
        Assert.True(_companies.PositionExists(posId));
        Assert.Equal(1, _companies.PositionCount);

        // Duplicate position
        Assert.Throws<InvalidOperationException>(() => _companies.AddPosition(position));

        // Create person and contract to occupy the position
        var personId = new PersonId(new EntityId(30));
        var householdId = new HouseholdId(new EntityId(31));
        var householdAccountId = new AccountId(new EntityId(32));
        var personalAccountId = new AccountId(new EntityId(33));

        _population.CreateHousehold(householdId, householdAccountId);
        _ledger.CreateAccount(householdAccountId, householdId.EntityId);
        _ledger.CreateAccount(personalAccountId, personId.EntityId);
        var person = Person.WithAge(personId, householdId, 30, 0, personalAccountId);
        _population.AddPerson(person);

        var contractId = new EmploymentContractId(new EntityId(40));
        var contract = new EmploymentContract(contractId, personId, companyId, posId, 0, comp);
        _employment.CreateContract(contract, _population, _companies);

        // Cannot remove occupied position
        Assert.Throws<InvalidOperationException>(() => _companies.RemovePosition(posId, _employment));

        // Terminate contract
        _employment.TerminateContract(contractId, 30);

        // Now removal succeeds
        _companies.RemovePosition(posId, _employment);
        Assert.False(_companies.PositionExists(posId));
        Assert.Equal(0, _companies.PositionCount);
    }

    [Fact]
    public void ValidateIntegrity_DetectsStructuralCorruptions()
    {
        var companyId = new CompanyId(new EntityId(10));
        var treasuryId = new AccountId(new EntityId(11));
        _ledger.CreateAccount(treasuryId, companyId.EntityId);
        var company = new Company(companyId, "Acme Corp", treasuryId);
        _companies.CreateCompany(company, _ledger);

        var (isValid, violations) = _companies.ValidateIntegrity(_ledger);
        Assert.True(isValid);
        Assert.Empty(violations);

        // Corrupted treasury account owner
        var corruptedLedger = new MoneyLedger(CurrencyCode.USD);
        corruptedLedger.CreateAccount(treasuryId, new EntityId(999)); // Wrong owner
        var (corruptedValid, corruptedViolations) = _companies.ValidateIntegrity(corruptedLedger);
        Assert.False(corruptedValid);
        Assert.NotEmpty(corruptedViolations);
    }
}
