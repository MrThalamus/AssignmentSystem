using AssignmentSystem.Api.Security;
using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Application.Features.Assignments;
using AssignmentSystem.Application.Features.Submissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Assignments. Reads are open to all three roles but the service narrows the result
/// to what the caller may see: admins get everything, teachers get their own course
/// subjects, and students get published work for the courses they are enrolled in.
/// </summary>
[ApiController]
[Route("api/assignments")]
[Produces("application/json")]
[Authorize]
public class AssignmentsController : ControllerBase
{
    private readonly IAssignmentService _assignmentService;
    private readonly ISubmissionService _submissionService;

    public AssignmentsController(
        IAssignmentService assignmentService, ISubmissionService submissionService)
    {
        _assignmentService = assignmentService;
        _submissionService = submissionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AssignmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AssignmentDto>>> List(
        [FromQuery] AssignmentListQuery query, CancellationToken ct) =>
        Ok(await _assignmentService.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AssignmentDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _assignmentService.GetAsync(id, ct));

    /// <summary>Creates an assignment as a draft, or published immediately.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignmentDto>> Create(
        CreateAssignmentRequest request, CancellationToken ct)
    {
        var assignment = await _assignmentService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = assignment.Id }, assignment);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignmentDto>> Update(
        Guid id, UpdateAssignmentRequest request, CancellationToken ct) =>
        Ok(await _assignmentService.UpdateAsync(id, request, ct));

    /// <summary>Makes a draft visible to the enrolled students.</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignmentDto>> Publish(Guid id, CancellationToken ct) =>
        Ok(await _assignmentService.PublishAsync(id, ct));

    /// <summary>Hides a published assignment again. Refused once submissions exist.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignmentDto>> Unpublish(Guid id, CancellationToken ct) =>
        Ok(await _assignmentService.RevertToDraftAsync(id, ct));

    /// <summary>Stops accepting submissions while leaving the assignment readable.</summary>
    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(AssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AssignmentDto>> Close(Guid id, CancellationToken ct) =>
        Ok(await _assignmentService.CloseAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _assignmentService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Lists the submissions handed in for one assignment.</summary>
    [HttpGet("{id:guid}/submissions")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(PagedResult<SubmissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SubmissionDto>>> ListSubmissions(
        Guid id, [FromQuery] SubmissionListQuery query, CancellationToken ct)
    {
        query.AssignmentId = id;
        return Ok(await _submissionService.ListAsync(query, ct));
    }
}
