namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Lifecycle of a student submission.
/// <list type="bullet">
/// <item><see cref="Submitted"/> - handed in on or before the deadline.</item>
/// <item><see cref="Late"/> - handed in after the deadline (only possible when the
/// assignment allows late submissions).</item>
/// <item><see cref="Graded"/> - the teacher has recorded marks and feedback.</item>
/// <item><see cref="Returned"/> - the teacher sent the work back for revision; the
/// student may submit again even if the deadline has passed.</item>
/// </list>
/// </summary>
public enum SubmissionStatus
{
    Submitted = 1,
    Late = 2,
    Graded = 3,
    Returned = 4
}
