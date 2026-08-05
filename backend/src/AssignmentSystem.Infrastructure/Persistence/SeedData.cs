namespace AssignmentSystem.Infrastructure.Persistence;

/// <summary>
/// Fixed identifiers for the demo data. Hard-coding them keeps seeding idempotent
/// (a re-run updates rather than duplicates) and lets <c>database/seed.sql</c> use
/// exactly the same ids as the application seeder.
/// </summary>
public static class SeedIds
{
    private static Guid Make(string suffix) => Guid.Parse($"00000000-0000-0000-0000-{suffix}");

    // users
    public static readonly Guid Admin = Make("000000000001");
    public static readonly Guid TeacherNazmul = Make("000000000011");
    public static readonly Guid TeacherFarhana = Make("000000000012");
    public static readonly Guid StudentRafi = Make("000000000021");
    public static readonly Guid StudentTasnim = Make("000000000022");
    public static readonly Guid StudentImran = Make("000000000023");
    public static readonly Guid StudentNusrat = Make("000000000024");
    public static readonly Guid StudentSabbir = Make("000000000025");

    // subjects
    public static readonly Guid SubjectMath = Make("000000000101");
    public static readonly Guid SubjectPhysics = Make("000000000102");
    public static readonly Guid SubjectComputing = Make("000000000103");
    public static readonly Guid SubjectEnglish = Make("000000000104");

    // courses
    public static readonly Guid CourseGrade10A = Make("000000000201");
    public static readonly Guid CourseGrade11Science = Make("000000000202");

    // course subjects
    public static readonly Guid CsGrade10Math = Make("000000000301");
    public static readonly Guid CsGrade10English = Make("000000000302");
    public static readonly Guid CsGrade11Physics = Make("000000000303");
    public static readonly Guid CsGrade11Computing = Make("000000000304");

    // enrollments
    public static readonly Guid EnrollRafiG10 = Make("000000000401");
    public static readonly Guid EnrollTasnimG10 = Make("000000000402");
    public static readonly Guid EnrollImranG10 = Make("000000000403");
    public static readonly Guid EnrollImranG11 = Make("000000000404");
    public static readonly Guid EnrollNusratG11 = Make("000000000405");
    public static readonly Guid EnrollSabbirG11 = Make("000000000406");

    // assignments
    public static readonly Guid AssignmentAlgebra = Make("000000000501");
    public static readonly Guid AssignmentEssay = Make("000000000502");
    public static readonly Guid AssignmentMotionLab = Make("000000000503");
    public static readonly Guid AssignmentSortingDraft = Make("000000000504");
    public static readonly Guid AssignmentGeometryClosed = Make("000000000505");

    // submissions
    public static readonly Guid SubmissionRafiAlgebra = Make("000000000601");
    public static readonly Guid SubmissionTasnimAlgebra = Make("000000000602");
    public static readonly Guid SubmissionImranMotionLate = Make("000000000603");
    public static readonly Guid SubmissionRafiGeometry = Make("000000000604");
}
