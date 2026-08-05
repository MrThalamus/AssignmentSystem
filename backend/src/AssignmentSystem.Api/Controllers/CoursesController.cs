using AssignmentSystem.Api.Security;
using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Application.Features.Academics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Controllers;

/// <summary>
/// Courses, the subjects taught within them, the teacher responsible for each, and
/// student enrollment. Structural changes are administrator-only; teachers get read
/// access so they can pick a course subject when creating an assignment.
/// </summary>
[ApiController]
[Route("api/courses")]
[Produces("application/json")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly ICourseService _courseService;

    public CoursesController(ICourseService courseService) => _courseService = courseService;

    // -------------------------------------------------------------- courses

    [HttpGet]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(PagedResult<CourseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CourseDto>>> List(
        [FromQuery] CourseListQuery query, CancellationToken ct) =>
        Ok(await _courseService.ListAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDto>> Get(Guid id, CancellationToken ct) =>
        Ok(await _courseService.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourseDto>> Create(CourseRequest request, CancellationToken ct)
    {
        var course = await _courseService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = course.Id }, course);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(CourseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseDto>> Update(Guid id, CourseRequest request, CancellationToken ct) =>
        Ok(await _courseService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _courseService.DeleteAsync(id, ct);
        return NoContent();
    }

    // ------------------------------------------------------- course subjects

    /// <summary>Lists the subjects taught in a course together with their teachers.</summary>
    [HttpGet("{courseId:guid}/subjects")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(IReadOnlyList<CourseSubjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseSubjectDto>>> ListSubjects(
        Guid courseId, CancellationToken ct) =>
        Ok(await _courseService.ListCourseSubjectsAsync(courseId, ct));

    /// <summary>Adds a subject to a course, optionally naming its teacher straight away.</summary>
    [HttpPost("{courseId:guid}/subjects")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(CourseSubjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CourseSubjectDto>> AddSubject(
        Guid courseId, AddCourseSubjectRequest request, CancellationToken ct)
    {
        var courseSubject = await _courseService.AddSubjectAsync(courseId, request, ct);
        return CreatedAtAction(nameof(ListSubjects), new { courseId }, courseSubject);
    }

    /// <summary>Assigns (or clears) the teacher responsible for a course subject.</summary>
    [HttpPut("subjects/{courseSubjectId:guid}/teacher")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(CourseSubjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CourseSubjectDto>> AssignTeacher(
        Guid courseSubjectId, AssignTeacherRequest request, CancellationToken ct) =>
        Ok(await _courseService.AssignTeacherAsync(courseSubjectId, request, ct));

    [HttpDelete("subjects/{courseSubjectId:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveSubject(Guid courseSubjectId, CancellationToken ct)
    {
        await _courseService.RemoveSubjectAsync(courseSubjectId, ct);
        return NoContent();
    }

    /// <summary>
    /// The course subjects the caller may create assignments for: their own for a
    /// teacher, all of them for an administrator.
    /// </summary>
    [HttpGet("teachable-subjects")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(IReadOnlyList<CourseSubjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CourseSubjectDto>>> ListTeachable(CancellationToken ct) =>
        Ok(await _courseService.ListTeachableAsync(ct));

    // ----------------------------------------------------------- enrollments

    [HttpGet("{courseId:guid}/students")]
    [Authorize(Roles = Roles.AdminOrTeacher)]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> ListStudents(
        Guid courseId, CancellationToken ct) =>
        Ok(await _courseService.ListEnrollmentsAsync(courseId, ct));

    /// <summary>Enrolls one or more students. Students already enrolled are ignored.</summary>
    [HttpPost("{courseId:guid}/students")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> EnrollStudents(
        Guid courseId, EnrollStudentsRequest request, CancellationToken ct) =>
        Ok(await _courseService.EnrollStudentsAsync(courseId, request, ct));

    [HttpDelete("{courseId:guid}/students/{studentId:guid}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveStudent(Guid courseId, Guid studentId, CancellationToken ct)
    {
        await _courseService.RemoveEnrollmentAsync(courseId, studentId, ct);
        return NoContent();
    }
}
