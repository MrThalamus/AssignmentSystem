using System.Text;
using AssignmentSystem.Application.Features.Submissions;

namespace AssignmentSystem.Tests.TestSupport;

/// <summary>
/// Uploads for the submission tests. The file rules inspect the header and nothing
/// else, and the endpoint tests only assert that whatever went up comes back unchanged,
/// so a structurally complete PDF would prove nothing extra - what matters is that the
/// bytes are distinguishable from one another and start the way a PDF does.
/// </summary>
public static class TestPdf
{
    /// <summary>Bytes that pass as a PDF, made unique by <paramref name="marker"/>.</summary>
    public static byte[] Bytes(string marker = "test") =>
        Encoding.ASCII.GetBytes($"%PDF-1.4\n% {marker}\n%%EOF\n");

    /// <summary>An acceptable upload, named after the text it carries so tests can tell attempts apart.</summary>
    public static UploadedFile Upload(string marker = "answer") =>
        new($"{marker}.pdf", "application/pdf", Bytes(marker));

    /// <summary>A file that is not a PDF, whatever its name or declared type claims.</summary>
    public static UploadedFile NotAPdf(string fileName = "answer.pdf") =>
        new(fileName, "application/pdf", Encoding.ASCII.GetBytes("PK this is a zip"));
}
