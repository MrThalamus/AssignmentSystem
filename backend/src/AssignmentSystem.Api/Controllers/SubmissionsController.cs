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
    /// Hands in an answer. Posting again for the same assignment revises the existing
    /// submission rather than creating a second one.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmissionDto>> Submit(
        CreateSubmissionRequest request, CancellationToken ct)
    {
        var submission = await _submissionService.SubmitAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = submission.Id }, submission);
    }

    /// <summary>Replaces the answer on an existing submission.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Student)]
    [ProducesResponseType(typeof(SubmissionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubmissionDto>> Update(
        Guid id, UpdateSubmissionRequest request, CancellationToken ct) =>
        Ok(await _submissionService.UpdateAsync(id, request, ct));

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
}
