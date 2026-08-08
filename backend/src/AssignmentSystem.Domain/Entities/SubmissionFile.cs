namespace AssignmentSystem.Domain.Entities;

/// <summary>
/// The bytes of a submitted PDF, held in a table of their own so that listing and
/// grading queries - which only ever need the file's name and size - never drag a
/// multi-megabyte payload across the wire. Loaded deliberately, by the download
/// endpoint alone.
/// </summary>
public class SubmissionFile
{
    /// <summary>Also the primary key: a submission has exactly one file.</summary>
    public Guid SubmissionId { get; set; }

    public Submission Submission { get; set; } = null!;

    public byte[] Content { get; set; } = [];
}
