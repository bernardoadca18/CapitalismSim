namespace CapitalismSim.Simulation.Core.Population;

/// <summary>
/// Result of validating population state integrity.
/// </summary>
public sealed class PopulationValidationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Violations { get; }

    private PopulationValidationResult(bool isValid, IReadOnlyList<string> violations)
    {
        IsValid = isValid;
        Violations = violations;
    }

    public static PopulationValidationResult Success() =>
        new(true, Array.Empty<string>());

    public static PopulationValidationResult Failure(IReadOnlyList<string> violations) =>
        new(false, violations);
}
