using AssignmentSystem.Api.Security;
using AssignmentSystem.Application.Features.Academics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Subject catalogue. Teachers need to read it to label their work, but only
/// administrators may change it.
/// </summary>
[ApiController]
[Route("api/subjects")]
[Produces("application/json")]
[Authorize]
public class SubjectsController : ControllerBase
{
    private readonly ISubjectService _subjectService;

    public SubjectsController(ISubjectService subjectService) => _subjectService = subjectService;

    [HttpGet]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(IReadOnlyList<SubjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubjectDto>>> List(CancellationToken ct) =>
        Ok(await _subjectService.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubjectDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _subjectService.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubjectDto>> Create(SubjectRequest request, CancellationToken ct)
    {
        var subject = await _subjectService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = subject.Id }, subject);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(SubjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubjectDto>> Update(Guid id, SubjectRequest request, CancellationToken ct) =>
        Ok(await _subjectService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _subjectService.DeleteAsync(id, ct);
        return NoContent();
    }
}
