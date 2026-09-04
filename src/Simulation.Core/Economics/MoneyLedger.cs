using CapitalismSim.Simulation.Core.Entities;

namespace CapitalismSim.Simulation.Core.Economics;

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
/// - Every account has a unique AccountId.
/// - Account balances can be negative (debt is allowed).
/// - Transfers are atomic: either both sides update or neither does.
/// - The ledger does not track transfer history; that would be a separate concern.
/// </remarks>
public sealed class MoneyLedger
{
    private readonly CurrencyCode _currency;
    private readonly Dictionary<AccountId, long> _balances = new();

    public MoneyLedger(CurrencyCode currency)
    {
        _currency = currency;
    }

    /// <summary>The currency all accounts in this ledger use.</summary>
    public CurrencyCode Currency => _currency;

    /// <summary>Number of accounts in the ledger.</summary>
    public int AccountCount => _balances.Count;

    /// <summary>
    /// Creates a new account with zero balance.
    /// </summary>
    public void CreateAccount(AccountId accountId)
    {
        if (_balances.ContainsKey(accountId))
        {
            throw new InvalidOperationException(
                $"Account {accountId} already exists in the ledger.");
        }

        _balances[accountId] = 0;
    }

    /// <summary>
    /// Gets the balance of an account. Returns zero for non-existent accounts.
    /// </summary>
    public Money GetBalance(AccountId accountId)
    {
        return _balances.TryGetValue(accountId, out long balance)
            ? new Money(balance, _currency)
            : Money.Zero(_currency);
    }

    /// <summary>
    /// Deposits money into an account. Account must exist.
    /// </summary>
    public void Deposit(AccountId accountId, Money amount)
    {
        ValidateCurrency(amount);
        ValidateAccountExists(accountId);

        _balances[accountId] = checked(_balances[accountId] + amount.MinorUnits);
    }

    /// <summary>
    /// Withdraws money from an account. Account must exist.
    /// Allows negative balances (debt).
    /// </summary>
    public void Withdraw(AccountId accountId, Money amount)
    {
        ValidateCurrency(amount);
        ValidateAccountExists(accountId);

        _balances[accountId] = checked(_balances[accountId] - amount.MinorUnits);
    }

    /// <summary>
    /// Transfers money atomically from one account to another.
    /// Both accounts must exist. Allows the source to go negative.
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

        // Use checked arithmetic for both sides
        _balances[from] = checked(_balances[from] - amount.MinorUnits);
        _balances[to] = checked(_balances[to] + amount.MinorUnits);
    }

    /// <summary>
    /// Checks if an account exists in the ledger.
    /// </summary>
    public bool AccountExists(AccountId accountId) => _balances.ContainsKey(accountId);

    /// <summary>
    /// Gets all account IDs in the ledger.
    /// </summary>
    public IEnumerable<AccountId> GetAllAccountIds() => _balances.Keys;

    /// <summary>
    /// Gets the sum of all balances. Can be negative if total debt exceeds assets.
    /// </summary>
    public Money GetTotalBalance()
    {
        long total = 0;
        foreach (long balance in _balances.Values)
        {
            total = checked(total + balance);
        }
        return new Money(total, _currency);
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
        if (!_balances.ContainsKey(accountId))
        {
            throw new InvalidOperationException(
                $"Account {accountId} does not exist in the ledger.");
        }
    }
}
