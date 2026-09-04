using CapitalismSim.Simulation.Core.Economics;
using CapitalismSim.Simulation.Core.Entities;
using Xunit;

namespace CapitalismSim.Simulation.Core.Tests.Economics;

public sealed class MoneyLedgerTests
{
    [Fact]
    public void Constructor_SetsCurrency()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);

        Assert.Equal(CurrencyCode.USD, ledger.Currency);
        Assert.Equal(0, ledger.AccountCount);
    }

    [Fact]
    public void CreateAccount_AddsAccountWithZeroBalance()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));

        ledger.CreateAccount(accountId);

        Assert.Equal(1, ledger.AccountCount);
        Assert.True(ledger.AccountExists(accountId));
        Assert.Equal(Money.Zero(CurrencyCode.USD), ledger.GetBalance(accountId));
    }

    [Fact]
    public void CreateAccount_DuplicateAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId);

        Assert.Throws<InvalidOperationException>(() => ledger.CreateAccount(accountId));
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
    public void Deposit_IncreasesBalance()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId);

        ledger.Deposit(accountId, new Money(100L, CurrencyCode.USD));

        Assert.Equal(new Money(100L, CurrencyCode.USD), ledger.GetBalance(accountId));
    }

    [Fact]
    public void Deposit_WrongCurrency_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Deposit(accountId, new Money(100L, CurrencyCode.EUR)));
    }

    [Fact]
    public void Deposit_NonExistentAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Deposit(accountId, new Money(100L, CurrencyCode.USD)));
    }

    [Fact]
    public void Withdraw_DecreasesBalance()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId);
        ledger.Deposit(accountId, new Money(100L, CurrencyCode.USD));

        ledger.Withdraw(accountId, new Money(30L, CurrencyCode.USD));

        Assert.Equal(new Money(70L, CurrencyCode.USD), ledger.GetBalance(accountId));
    }

    [Fact]
    public void Withdraw_AllowsNegativeBalance()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId);

        ledger.Withdraw(accountId, new Money(50L, CurrencyCode.USD));

        Assert.Equal(new Money(-50L, CurrencyCode.USD), ledger.GetBalance(accountId));
    }

    [Fact]
    public void Transfer_MovesMoneyAtomically()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from);
        ledger.CreateAccount(to);
        ledger.Deposit(from, new Money(100L, CurrencyCode.USD));

        ledger.Transfer(from, to, new Money(40L, CurrencyCode.USD));

        Assert.Equal(new Money(60L, CurrencyCode.USD), ledger.GetBalance(from));
        Assert.Equal(new Money(40L, CurrencyCode.USD), ledger.GetBalance(to));
    }

    [Fact]
    public void Transfer_SameAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var accountId = new AccountId(new EntityId(1));
        ledger.CreateAccount(accountId);
        ledger.Deposit(accountId, new Money(100L, CurrencyCode.USD));

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(accountId, accountId, new Money(10L, CurrencyCode.USD)));
    }

    [Fact]
    public void Transfer_ZeroAmount_IsNoOp()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from);
        ledger.CreateAccount(to);
        ledger.Deposit(from, new Money(100L, CurrencyCode.USD));

        ledger.Transfer(from, to, Money.Zero(CurrencyCode.USD));

        Assert.Equal(new Money(100L, CurrencyCode.USD), ledger.GetBalance(from));
        Assert.Equal(Money.Zero(CurrencyCode.USD), ledger.GetBalance(to));
    }

    [Fact]
    public void Transfer_NonExistentFromAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(to);

        Assert.Throws<InvalidOperationException>(() =>
            ledger.Transfer(from, to, new Money(10L, CurrencyCode.USD)));
    }

    [Fact]
    public void Transfer_NonExistentToAccount_Throws()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var from = new AccountId(new EntityId(1));
        var to = new AccountId(new EntityId(2));
        ledger.CreateAccount(from);

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
        ledger.CreateAccount(a);
        ledger.CreateAccount(b);
        ledger.CreateAccount(c);
        ledger.Deposit(a, new Money(100L, CurrencyCode.USD));
        ledger.Deposit(b, new Money(200L, CurrencyCode.USD));
        ledger.Deposit(c, new Money(50L, CurrencyCode.USD));

        var total = ledger.GetTotalBalance();

        Assert.Equal(new Money(350L, CurrencyCode.USD), total);
    }

    [Fact]
    public void GetTotalBalance_IncludesNegativeBalances()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var a = new AccountId(new EntityId(1));
        var b = new AccountId(new EntityId(2));
        ledger.CreateAccount(a);
        ledger.CreateAccount(b);
        ledger.Deposit(a, new Money(100L, CurrencyCode.USD));
        ledger.Withdraw(b, new Money(50L, CurrencyCode.USD));

        var total = ledger.GetTotalBalance();

        Assert.Equal(new Money(50L, CurrencyCode.USD), total);
    }

    [Fact]
    public void GetAllAccountIds_ReturnsAllCreatedAccounts()
    {
        var ledger = new MoneyLedger(CurrencyCode.USD);
        var a = new AccountId(new EntityId(1));
        var b = new AccountId(new EntityId(2));
        ledger.CreateAccount(a);
        ledger.CreateAccount(b);

        var ids = ledger.GetAllAccountIds().ToHashSet();

        Assert.Equal(2, ids.Count);
        Assert.Contains(a, ids);
        Assert.Contains(b, ids);
    }
}
