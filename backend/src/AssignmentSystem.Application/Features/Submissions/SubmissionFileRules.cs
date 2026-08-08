using System.Text;
using AssignmentSystem.Domain.ValueObjects;
using ValidationException = AssignmentSystem.Application.Common.Exceptions.ValidationException;

namespace AssignmentSystem.Application.Features.Submissions;

/// <summary>
/// Decides whether an upload is a submission we are willing to store. Enforced in the
/// application layer rather than by a FluentValidation rule, because the file arrives
/// as multipart form data rather than as a JSON body the auto-validator would see.
/// </summary>
public static class SubmissionFileRules
{
    /// <summary>
    /// The ceiling on an uploaded PDF. The bytes live in PostgreSQL, so this is as much
    /// about keeping the database small as it is about rejecting nonsense.
    /// </summary>
    public const int MaxSizeBytes = 10 * 1024 * 1024;

    /// <summary>The field name errors are reported against, so the form can highlight the input.</summary>
    public const string FieldName = "file";

    private const string Extension = ".pdf";

    /// <summary>
    /// Every PDF starts with this marker. The specification tolerates a little junk in
    /// front of it, so the first kilobyte is searched rather than only byte zero.
    /// </summary>
    private static readonly byte[] PdfHeader = Encoding.ASCII.GetBytes("%PDF-");
    private const int HeaderSearchWindow = 1024;

    public static string MaxSizeDescription => $"{MaxSizeBytes / (1024 * 1024)} MB";

    /// <summary>
    /// Checks an upload and turns it into the value the domain stores.
    /// </summary>
    /// <exception cref="ValidationException">The upload is missing, too large, or not a PDF.</exception>
    public static SubmittedDocument Accept(UploadedFile? upload)
    {
        if (upload is null || upload.Content.Length == 0)
            throw Reject("Attach the PDF you want to hand in.");

        if (upload.Content.Length > MaxSizeBytes)
            throw Reject($"The file is larger than the {MaxSizeDescription} limit.");

        if (!upload.FileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            throw Reject("Only PDF files can be submitted.");

        // The browser's Content-Type is a claim about the file, not evidence: it is
        // whatever the operating system associated with the extension. The bytes are
        // what decide, so a renamed .docx is caught here rather than reaching a teacher.
        if (!LooksLikePdf(upload.Content))
            throw Reject("That file is not a valid PDF. Export your work as a PDF and try again.");

        return new SubmittedDocument(SanitiseFileName(upload.FileName), upload.Content);
    }

    private static bool LooksLikePdf(byte[] content)
    {
        var limit = Math.Min(content.Length, HeaderSearchWindow) - PdfHeader.Length;

        for (var start = 0; start <= limit; start++)
        {
            var matched = true;

            for (var offset = 0; offset < PdfHeader.Length; offset++)
            {
                if (content[start + offset] == PdfHeader[offset]) continue;

                matched = false;
                break;
            }

            if (matched) return true;
        }

        return false;
    }

    /// <summary>
    /// Reduces the browser-supplied name to something safe to store and to echo back in
    /// a Content-Disposition header: no directories, no control characters, no quotes.
    /// </summary>
    private static string SanitiseFileName(string fileName)
    {
        // A browser may send a full path (older Internet Explorer did), and a crafted
        // client can send anything at all, so only the last segment is kept.
        var name = fileName.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];

        var cleaned = new StringBuilder(name.Length);

        foreach (var character in name)
        {
            cleaned.Append(char.IsControl(character) || character is '"' or '\r' or '\n'
                ? '_'
                : character);
        }

        var safe = cleaned.ToString().Trim();

        if (safe.Length > 200)
        {
            // Keep the extension rather than the tail of a very long name.
            safe = string.Concat(safe.AsSpan(0, 200 - Extension.Length), Extension);
        }

        return safe.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && safe.Length > Extension.Length
            ? safe
            : "submission.pdf";
    }

    private static ValidationException Reject(string message) => new(FieldName, message);
}
