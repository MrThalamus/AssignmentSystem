using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Application.Features.Academics;

public interface ISubjectService
{
    Task<IReadOnlyList<SubjectDto>> ListAsync(CancellationToken ct = default);
    Task<SubjectDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<SubjectDto> CreateAsync(SubjectRequest request, CancellationToken ct = default);
    Task<SubjectDto> UpdateAsync(Guid id, SubjectRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public class SubjectService : ISubjectService
{
    private readonly IApplicationDbContext _db;
    private readonly IDateTimeProvider _clock;

    public SubjectService(IApplicationDbContext db, IDateTimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<SubjectDto>> ListAsync(CancellationToken ct = default) =>
        await _db.Subjects
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SubjectDto(s.Id, s.Name, s.Code, s.Description))
            .ToListAsync(ct);

    public async Task<SubjectDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var subject = await _db.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw NotFoundException.For("Subject", id);

        return Map(subject);
    }

    public async Task<SubjectDto> CreateAsync(SubjectRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();

        if (await _db.Subjects.AnyAsync(s => s.Code == code, ct))
            throw new ConflictException($"A subject with the code '{code}' already exists.");

        var subject = new Subject
        {
            Name = request.Name.Trim(),
            Code = code,
            Description = request.Description?.Trim(),
            CreatedAt = _clock.UtcNow
        };

        _db.Subjects.Add(subject);
        await _db.SaveChangesAsync(ct);

        return Map(subject);
    }

    public async Task<SubjectDto> UpdateAsync(Guid id, SubjectRequest request, CancellationToken ct = default)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw NotFoundException.For("Subject", id);

        var code = request.Code.Trim().ToUpperInvariant();

        if (await _db.Subjects.AnyAsync(s => s.Code == code && s.Id != id, ct))
            throw new ConflictException($"A subject with the code '{code}' already exists.");

        subject.Name = request.Name.Trim();
        subject.Code = code;
        subject.Description = request.Description?.Trim();

        await _db.SaveChangesAsync(ct);

        return Map(subject);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var subject = await _db.Subjects.FirstOrDefaultAsync(s => s.Id == id, ct)
                      ?? throw NotFoundException.For("Subject", id);

        // Removing a subject that is being taught would cascade into assignments and
        // the submissions hanging off them.
        if (await _db.CourseSubjects.AnyAsync(cs => cs.SubjectId == id, ct))
            throw new ConflictException(
                "The subject is assigned to at least one course and cannot be deleted.");

        _db.Subjects.Remove(subject);
        await _db.SaveChangesAsync(ct);
    }

    private static SubjectDto Map(Subject s) => new(s.Id, s.Name, s.Code, s.Description);
}
