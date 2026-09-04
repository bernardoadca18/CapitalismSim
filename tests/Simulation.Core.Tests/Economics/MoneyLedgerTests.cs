using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Economics;

public sealed class MoneyLedgerTests
{
    private static readonly EntityId TestOwner = new(1);

    [Fact]
    public void Constructor_SetsCurrency()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);

        Assert.Equal(CurrencyCode.USD, ledger.Currency);
        Assert.Equal(0, ledger.AccountCount);
    }

    [Fact]
    public void CreateAccount_AddsAccountWithZeroBalanceAndOwner()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));

        ledger.CreateAccount(accountId, TestOwner);

        Assert.Equal(1, ledger.AccountCount);
        Assert.True(ledger.AccountExists(accountId));
        Assert.Equal(Money.Zero(CurrencyCode.USD), ledger.GetBalance(accountId));
        Assert.Equal(TestOwner, ledger.GetOwner(accountId));
    }

    [Fact]
    public void CreateAccount_DuplicateAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);

        Assert.Throws<InvalidOperationException>(() => ledger.CreateAccount(accountId, TestOwner));
    }

    [Fact]
    public void CreateAccount_NoneOwner_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));

        Assert.Throws<ArgumentException>(() => ledger.CreateAccount(accountId, EntityId.None));
    }

    [Fact]
    public void GetBalance_NonExistentAccount_ReturnsZero()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));

        var balance = ledger.GetBalance(accountId);

        Assert.Equal(Money.Zero(CurrencyCode.USD), balance);
    }

    [Fact]
    public void GetOwner_NonExistentAccount_ReturnsNone()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));

        var owner = ledger.GetOwner(accountId);

        Assert.Equal(EntityId.None, owner);
    }

    [Fact]
    public void ExternalCredit_IncreasesBalance()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);

        ledger.ExternalCredit(accountId, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Equal(new Money(100L, CurrencyCode.USD), ledger.GetBalance(accountId));
        Assert.Equal(new Money(100L, CurrencyCode.USD), ledger.TotalExternalCredits);
    }

    [Fact]
    public void ExternalCredit_WrongCurrency_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.ExternalCredit(accountId, new Money(100L, CurrencyCode.EUR), MoneyFlowReason.TestFixture));
    }

    [Fact]
    public void ExternalCredit_NonExistentAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));

        Assert.Throws<InvalidOperationException>(() =>
            ledger.ExternalCredit(accountId, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture));
    }

    [Fact]
    public void ExternalCredit_NegativeAmount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.ExternalCredit(accountId, new Money(-100L, CurrencyCode.USD), MoneyFlowReason.TestFixture));
    }

    [Fact]
    public void ExternalDebit_DecreasesBalance()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);
        ledger.ExternalCredit(accountId, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        ledger.ExternalDebit(accountId, new Money(30L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Equal(new Money(70L, CurrencyCode.USD), ledger.GetBalance(accountId));
        Assert.Equal(new Money(30L, CurrencyCode.USD), ledger.TotalExternalDebits);
    }

    [Fact]
    public void ExternalDebit_InsufficientFunds_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);
        ledger.ExternalCredit(accountId, new Money(50L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.ExternalDebit(accountId, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture));
    }

    [Fact]
    public void ExternalDebit_NegativeAmount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);
        ledger.ExternalCredit(accountId, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.ExternalDebit(accountId, new Money(-30L, CurrencyCode.USD), MoneyFlowReason.TestFixture));
    }

    [Fact]
    public void Transfer_MovesMoneyAtomically()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from, TestOwner);
        ledger.CreateAccount(to, TestOwner);
        ledger.ExternalCredit(from, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        ledger.Transfer(from, to, new Money(40L, CurrencyCode.USD));

        Assert.Equal(new Money(60L, CurrencyCode.USD), ledger.GetBalance(from));
        Assert.Equal(new Money(40L, CurrencyCode.USD), ledger.GetBalance(to));
    }

    [Fact]
    public void Transfer_SameAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId, TestOwner);
        ledger.ExternalCredit(accountId, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(accountId, accountId, new Money(10L, CurrencyCode.USD)));
    }

    [Fact]
    public void Transfer_ZeroAmount_IsNoOp()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from, TestOwner);
        ledger.CreateAccount(to, TestOwner);
        ledger.ExternalCredit(from, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        ledger.Transfer(from, to, Money.Zero(CurrencyCode.USD));

        Assert.Equal(new Money(100L, CurrencyCode.USD), ledger.GetBalance(from));
        Assert.Equal(Money.Zero(CurrencyCode.USD), ledger.GetBalance(to));
    }

    [Fact]
    public void Transfer_InsufficientFunds_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from, TestOwner);
        ledger.CreateAccount(to, TestOwner);
        ledger.ExternalCredit(from, new Money(50L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(from, to, new Money(100L, CurrencyCode.USD)));
    }

    [Fact]
    public void Transfer_InsufficientFunds_DoesNotMutate()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from, TestOwner);
        ledger.CreateAccount(to, TestOwner);
        ledger.ExternalCredit(from, new Money(50L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(from, to, new Money(100L, CurrencyCode.USD)));

        // Balances should be unchanged
        Assert.Equal(new Money(50L, CurrencyCode.USD), ledger.GetBalance(from));
        Assert.Equal(Money.Zero(CurrencyCode.USD), ledger.GetBalance(to));
    }

    [Fact]
    public void Transfer_NegativeAmount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from, TestOwner);
        ledger.CreateAccount(to, TestOwner);
        ledger.ExternalCredit(from, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(from, to, new Money(-10L, CurrencyCode.USD)));
    }

    [Fact]
    public void Transfer_NonExistentFromAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(to, TestOwner);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(from, to, new Money(10L, CurrencyCode.USD)));
    }

    [Fact]
    public void Transfer_NonExistentToAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from, TestOwner);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(from, to, new Money(10L, CurrencyCode.USD)));
    }

    [Fact]
    public void GetTotalBalance_SumsAllAccounts()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var a = new AccountId(new EntityId(1));
        var b = new AccountId(new EntityId(2));
        var c = new AccountId(new EntityId(3));
        ledger.CreateAccount(a, TestOwner);
        ledger.CreateAccount(b, TestOwner);
        ledger.CreateAccount(c, TestOwner);
        ledger.ExternalCredit(a, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);
        ledger.ExternalCredit(b, new Money(200L, CurrencyCode.USD), MoneyFlowReason.TestFixture);
        ledger.ExternalCredit(c, new Money(50L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        var total = ledger.GetTotalBalance();

        Assert.Equal(new Money(350L, CurrencyCode.USD), total);
    }

    [Fact]
    public void VerifySupplyAccounting_MatchesTotalBalance()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var a = new AccountId(new EntityId(1));
        var b = new AccountId(new EntityId(2));
        ledger.CreateAccount(a, TestOwner);
        ledger.CreateAccount(b, TestOwner);
        ledger.ExternalCredit(a, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);
        ledger.ExternalCredit(b, new Money(200L, CurrencyCode.USD), MoneyFlowReason.TestFixture);
        ledger.ExternalDebit(a, new Money(30L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        Assert.True(ledger.VerifySupplyAccounting());
    }

    [Fact]
    public void Transfer_DoesNotAffectSupply()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var a = new AccountId(new EntityId(1));
        var b = new AccountId(new EntityId(2));
        ledger.CreateAccount(a, TestOwner);
        ledger.CreateAccount(b, TestOwner);
        ledger.ExternalCredit(a, new Money(100L, CurrencyCode.USD), MoneyFlowReason.TestFixture);

        var creditsBefore = ledger.TotalExternalCredits;
        var debitsBefore = ledger.TotalExternalDebits;

        ledger.Transfer(a, b, new Money(40L, CurrencyCode.USD));

        Assert.Equal(creditsBefore, ledger.TotalExternalCredits);
        Assert.Equal(debitsBefore, ledger.TotalExternalDebits);
    }

    [Fact]
    public void GetAllAccountIds_ReturnsAllCreatedAccounts()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var a = new AccountId(new EntityId(1));
        var b = new AccountId(new EntityId(2));
        ledger.CreateAccount(a, TestOwner);
        ledger.CreateAccount(b, TestOwner);

        var ids = ledger.GetAllAccountIds().ToHashSet();

        Assert.Equal(2, ids.Count);
        Assert.Contains(a, ids);
        Assert.Contains(b, ids);
    }
}
