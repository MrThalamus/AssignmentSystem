namespace AssignmentSystem.Domain.ValueObjects;

/// <summary>
/// A PDF a student has handed in, already read into memory and already checked for
/// being a PDF. Constructing one is deliberately trivial - deciding whether the
/// upload is acceptable belongs to the application layer, which can report the
/// failure per-field - so the domain only has to record what was accepted.
/// </summary>
/// <param name="FileName">The sanitised name to show the teacher and to serve on download.</param>
/// <param name="Content">The raw bytes of the file.</param>
public sealed record SubmittedDocument(string FileName, byte[] Content)
{
    public const string PdfContentType = "application/pdf";

    public int SizeBytes => Content.Length;
}
