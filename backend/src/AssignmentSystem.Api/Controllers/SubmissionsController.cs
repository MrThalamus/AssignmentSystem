using AssignmentSystem.Api.Security;
using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Application.Features.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// The submission workflow. Students hand in and revise their own work; teachers
/// read, grade and re-status the work handed in for their own course subjects.
/// </summary>
[ApiController]
[Route("api/submissions")]
[Produces("application/json")]
[Authorize]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService) =>
        _submissionService = submissionService;

    /// <summary>
    /// Lists submissions the caller may see. A student's results are always narrowed
    /// to their own work regardless of the filters supplied.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SubmissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SubmissionDto>>> List(
        [FromQuery] SubmissionListQuery query, CancellationToken ct) =>
        Ok(await _submissionService.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmissionDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _submissionService.GetAsync(id, ct));

    /// <summary>
    /// Downloads the submitted PDF. Scoped exactly like the rest of the reads: a student
    /// gets only their own file, a teacher only the files handed in for their classes.
    /// </summary>
    [HttpGet("{id:guid}/file")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await _submissionService.DownloadAsync(id, ct);

        // Inline rather than an attachment, so the browser (and the grading screen's
        // embedded viewer) can show the PDF instead of only offering to save it.
        return File(file.Content, file.ContentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Hands in a PDF. Posting again for the same assignment revises the existing
    /// submission rather than creating a second one.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Student)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmissionDto>> Submit(
        [FromForm] Guid assignmentId, IFormFile? file, CancellationToken ct)
    {
        var request = new CreateSubmissionRequest(assignmentId, await ReadAsync(file, ct));
        var submission = await _submissionService.SubmitAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = submission.Id }, submission);
    }

    /// <summary>Replaces the PDF on an existing submission.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Student)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmissionDto>> Update(
        Guid id, IFormFile? file, CancellationToken ct) =>
        Ok(await _submissionService.UpdateAsync(id, new UpdateSubmissionRequest(await ReadAsync(file, ct)), ct));

    /// <summary>Records marks and feedback.</summary>
    [HttpPost("{id:guid}/grade")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmissionDto>> Grade(
        Guid id, GradeSubmissionRequest request, CancellationToken ct) =>
        Ok(await _submissionService.GradeAsync(id, request, ct));

    /// <summary>
    /// Moves a submission to another state by hand, for example returning graded work
    /// so the student can revise it.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmissionDto>> ChangeStatus(
        Guid id, ChangeSubmissionStatusRequest request, CancellationToken ct) =>
        Ok(await _submissionService.ChangeStatusAsync(id, request, ct));

    /// <summary>
    /// The ceiling Kestrel enforces on the whole request. A little larger than the file
    /// limit itself to leave room for multipart boundaries and headers, so a file of
    /// exactly the permitted size is rejected by the rule with a readable message rather
    /// than by the server with a bare protocol error.
    /// </summary>
    private const long MaxUploadBytes = SubmissionFileRules.MaxSizeBytes + 64 * 1024;

    /// <summary>
    /// Buffers the upload so the application layer never sees a request-scoped stream.
    /// A missing part is passed on as null rather than rejected here - the file rules
    /// own that message, and own it identically for both endpoints.
    /// </summary>
    private static async Task<UploadedFile?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return null;

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);

        return new UploadedFile(file.FileName, file.ContentType, buffer.ToArray());
    }
}
