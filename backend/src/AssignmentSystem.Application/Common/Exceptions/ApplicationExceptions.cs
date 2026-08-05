namespace AssignmentSystem.Application.Common.Exceptions;

/// <summary>The requested entity does not exist (or the caller may not know that it does).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public static NotFoundException For(string entity, Guid id) =>
        new($"{entity} '{id}' was not found.");
}

/// <summary>
/// The caller is authenticated but is not allowed to touch this particular record,
/// e.g. a teacher opening another teacher's assignment. Surfaces as HTTP 403.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>Input failed validation. Carries per-field messages for the client.</summary>
public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] }) { }
}

/// <summary>A uniqueness or state conflict, e.g. an email that is already registered.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
