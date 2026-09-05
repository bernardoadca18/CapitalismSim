using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Economics;

/// <summary>
/// Represents the reason for an external money flow (source or sink).
/// </summary>
/// <remarks>
/// This is a minimal value type for Phase 2. Future phases may add more structured
/// reasons (e.g., loan disbursement, tax collection, etc.).
/// </remarks>
public readonly record struct MoneyFlowReason
{
    public string Value { get; }

    private MoneyFlowReason(string value)
    {
        Value = value;
    }

    public static MoneyFlowReason InitialEndowment => new("initial-endowment");
    public static MoneyFlowReason BasicNeeds => new("basic-needs");
    public static MoneyFlowReason Wages => new("wages");
    public static MoneyFlowReason TestFixture => new("test-fixture");

    public static MoneyFlowReason FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Money flow reason cannot be null or whitespace.", nameof(value));
        }
        return new MoneyFlowReason(value);
    }

    public override string ToString() => Value;
}

/// <summary>
/// Manages a set of monetary accounts and supports atomic transfers between them.
/// All operations use checked arithmetic to detect overflow.
/// </summary>
/// <remarks>
/// This is a deterministic, single-threaded ledger. No I/O, no persistence,
/// no external currency conversion. All accounts must use the same currency,
/// set at construction time.
///
/// Design invariants:
/// - Every account has a unique AccountId and an owner EntityId.
/// - Account balances must NEVER be negative. Debt is represented by explicit
///   Loan/DebtContract entities in future phases, not by negative balances.
/// - Transfers are atomic: either both sides update or neither does.
/// - ExternalCredit and ExternalDebit are the ONLY ways to create/destroy money.
/// - The ledger tracks total external credits and debits for supply accounting.
/// </remarks>
public sealed class MoneyLedger
{
    private readonly CurrencyCode _currency;
    private readonly Dictionary<AccountId, AccountData> _accounts = new();
    private long _totalExternalCredits;
    private long _totalExternalDebits;

    private readonly record struct AccountData(long Balance, EntityId Owner);

    public MoneyLedger(CurrencyCode currency)
    {
        _currency = currency;
    }

    /// <summary>The currency all accounts in this ledger use.</summary>
    public CurrencyCode Currency => _currency;

    /// <summary>Number of accounts in the ledger.</summary>
    public int AccountCount => _accounts.Count;

    /// <summary>Total money created via ExternalCredit.</summary>
    public Money TotalExternalCredits => new(_totalExternalCredits, _currency);

    /// <summary>Total money destroyed via ExternalDebit.</summary>
    public Money TotalExternalDebits => new(_totalExternalDebits, _currency);

    /// <summary>
    /// Creates a new account with zero balance and the specified owner.
    /// </summary>
    public void CreateAccount(AccountId accountId, EntityId owner)
    {
        if (_accounts.ContainsKey(accountId))
        {
            throw new InvalidOperationException(
                $"Account {accountId} already exists in the ledger.");
        }

        if (owner.IsNone)
        {
            throw new ArgumentException(
                "Account owner cannot be EntityId.None.", nameof(owner));
        }

        _accounts[accountId] = new AccountData(0, owner);
    }

    /// <summary>
    /// Gets the balance of an account. Returns zero for non-existent accounts.
    /// </summary>
    public Money GetBalance(AccountId accountId)
    {
        return _accounts.TryGetValue(accountId, out var data)
            ? new Money(data.Balance, _currency)
            : Money.Zero(_currency);
    }

    /// <summary>
    /// Gets the owner of an account. Returns EntityId.None for non-existent accounts.
    /// </summary>
    public EntityId GetOwner(AccountId accountId)
    {
        return _accounts.TryGetValue(accountId, out var data)
            ? data.Owner
            : EntityId.None;
    }

    /// <summary>
    /// Creates money from an external source and credits it to an account.
    /// This is one of only two ways to increase the monetary supply.
    /// </summary>
    /// <exception cref="InvalidOperationException">Account does not exist or amount would overflow.</exception>
    public void ExternalCredit(AccountId accountId, Money amount, MoneyFlowReason reason)
    {
        ValidateCurrency(amount);
        ValidateAccountExists(accountId);

        if (amount.IsZero)
        {
            return; // No-op for zero
        }

        if (amount.MinorUnits < 0)
        {
            throw new InvalidOperationException(
                "External credit amount cannot be negative.");
        }

        var data = _accounts[accountId];
        long newBalance = checked(data.Balance + amount.MinorUnits);

        // Balance is always non-negative (we're adding positive amount to non-negative balance)
        _totalExternalCredits = checked(_totalExternalCredits + amount.MinorUnits);
        _accounts[accountId] = data with { Balance = newBalance };
    }

    /// <summary>
    /// Destroys money by debiting from an account to an external sink.
    /// This is one of only two ways to decrease the monetary supply.
    /// The amount debited cannot exceed the account balance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Account does not exist, insufficient funds, or amount would overflow.</exception>
    public void ExternalDebit(AccountId accountId, Money amount, MoneyFlowReason reason)
    {
        ValidateCurrency(amount);
        ValidateAccountExists(accountId);

        if (amount.IsZero)
        {
            return; // No-op for zero
        }

        if (amount.MinorUnits < 0)
        {
            throw new InvalidOperationException(
                "External debit amount cannot be negative.");
        }

        var data = _accounts[accountId];

        if (data.Balance < amount.MinorUnits)
        {
            throw new InvalidOperationException(
                $"Insufficient funds for external debit. Account {accountId} has balance {data.Balance}, attempted to debit {amount.MinorUnits}.");
        }

        long newBalance = data.Balance - amount.MinorUnits;
        _totalExternalDebits = checked(_totalExternalDebits + amount.MinorUnits);
        _accounts[accountId] = data with { Balance = newBalance };
    }

    /// <summary>
    /// Transfers money atomically from one account to another.
    /// Both accounts must exist. The source must have sufficient funds.
    /// Neither balance can go negative.
    /// </summary>
    public void Transfer(AccountId from, AccountId to, Money amount)
    {
        ValidateCurrency(amount);
        ValidateAccountExists(from);
        ValidateAccountExists(to);

        if (from == to)
        {
            throw new InvalidOperationException(
                $"Cannot transfer to the same account: {from}.");
        }

        if (amount.IsZero)
        {
            return; // No-op for zero transfers
        }

        if (amount.MinorUnits < 0)
        {
            throw new InvalidOperationException(
                "Transfer amount cannot be negative.");
        }

        // Get copies of both account data for validation
        var fromData = _accounts[from];
        var toData = _accounts[to];

        // Check for sufficient funds BEFORE any mutation
        if (fromData.Balance < amount.MinorUnits)
        {
            throw new InvalidOperationException(
                $"Insufficient funds for transfer. Account {from} has balance {fromData.Balance}, attempted to transfer {amount.MinorUnits}.");
        }

        // Check for destination overflow BEFORE any mutation
        long newToBalance = checked(toData.Balance + amount.MinorUnits);
        if (newToBalance < 0)
        {
            throw new InvalidOperationException(
                $"Transfer would cause overflow in destination account {to}.");
        }

        // Both validations passed, perform the atomic transfer
        _accounts[from] = fromData with { Balance = fromData.Balance - amount.MinorUnits };
        _accounts[to] = toData with { Balance = newToBalance };
    }

    /// <summary>
    /// Checks if an account exists in the ledger.
    /// </summary>
    public bool AccountExists(AccountId accountId) => _accounts.ContainsKey(accountId);

    /// <summary>
    /// Gets all account IDs in the ledger.
    /// </summary>
    public IEnumerable<AccountId> GetAllAccountIds() => _accounts.Keys;

    /// <summary>
    /// Gets the sum of all balances. Should equal TotalExternalCredits - TotalExternalDebits.
    /// </summary>
    public Money GetTotalBalance()
    {
        long total = 0;
        foreach (var (_, data) in _accounts)
        {
            total = checked(total + data.Balance);
        }
        return new Money(total, _currency);
    }

    /// <summary>
    /// Verifies that the sum of all account balances equals the supply accounting invariant.
    /// </summary>
    public bool VerifySupplyAccounting()
    {
        return GetTotalBalance().MinorUnits == _totalExternalCredits - _totalExternalDebits;
    }

    /// <summary>
    /// Creates a deep copy of this ledger.
    /// </summary>
    public MoneyLedger Clone()
    {
        var clone = new MoneyLedger(_currency);
        foreach (var (accountId, data) in _accounts)
        {
            clone._accounts[accountId] = data;
        }
        clone._totalExternalCredits = _totalExternalCredits;
        clone._totalExternalDebits = _totalExternalDebits;
        return clone;
    }

    /// <summary>
    /// Restores account state directly from snapshot data.
    /// This bypasses normal ExternalCredit/ExternalDebit paths to restore
    /// exact balances and supply counters without affecting accounting invariants.
    /// </summary>
    /// <param name="accounts">The accounts to restore (accountId, owner, balance).</param>
    /// <param name="totalExternalCredits">The total external credits counter.</param>
    /// <param name="totalExternalDebits">The total external debits counter.</param>
    public void Restore(
        IEnumerable<(AccountId AccountId, EntityId Owner, long BalanceMinorUnits)> accounts,
        long totalExternalCredits,
        long totalExternalDebits)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        _accounts.Clear();
        foreach (var (accountId, owner, balance) in accounts)
        {
            if (owner.IsNone)
            {
                throw new ArgumentException(
                    $"Account {accountId} has invalid owner EntityId.None.", nameof(accounts));
            }

            if (balance < 0)
            {
                throw new ArgumentException(
                    $"Account {accountId} has invalid negative balance {balance}.", nameof(accounts));
            }

            _accounts[accountId] = new AccountData(balance, owner);
        }

        _totalExternalCredits = totalExternalCredits;
        _totalExternalDebits = totalExternalDebits;
    }

    private void ValidateCurrency(Money amount)
    {
        if (amount.Currency != _currency)
        {
            throw new InvalidOperationException(
                $"Ledger uses currency {_currency}, but amount was {amount.Currency}.");
        }
    }

    private void ValidateAccountExists(AccountId accountId)
    {
        if (!_accounts.ContainsKey(accountId))
        {
            throw new InvalidOperationException(
                $"Account {accountId} does not exist in the ledger.");
        }
    }
}
