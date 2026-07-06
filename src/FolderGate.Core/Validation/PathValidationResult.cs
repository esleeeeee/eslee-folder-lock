namespace FolderGate.Core.Validation;

public sealed class PathValidationResult
{
    public bool IsValid { get; init; }

    public string? NormalizedPath { get; init; }

    public string? ErrorMessage { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    public static PathValidationResult Valid(string normalizedPath, IReadOnlyList<string> warnings)
    {
        return new PathValidationResult
        {
            IsValid = true,
            NormalizedPath = normalizedPath,
            Warnings = warnings
        };
    }

    public static PathValidationResult Invalid(string errorMessage)
    {
        return new PathValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
}
