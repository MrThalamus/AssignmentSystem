namespace AssignmentSystem.Domain.Exceptions;

/// <summary>Base type for every rule the domain enforces.</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>
/// A request that is well-formed but not allowed in the current state of the data,
/// e.g. submitting to a draft assignment. Surfaces as HTTP 409.
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}
