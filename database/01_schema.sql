CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE TABLE courses (
        "Id" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Code" character varying(30) NOT NULL,
        "AcademicYear" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_courses" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE TABLE subjects (
        "Id" uuid NOT NULL,
        "Name" character varying(150) NOT NULL,
        "Code" character varying(30) NOT NULL,
        "Description" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_subjects" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL,
        "FullName" character varying(150) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "PasswordHash" character varying(500) NOT NULL,
        "Role" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE TABLE course_subjects (
        "Id" uuid NOT NULL,
        "CourseId" uuid NOT NULL,
        "SubjectId" uuid NOT NULL,
        "TeacherId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_course_subjects" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_course_subjects_courses_CourseId" FOREIGN KEY ("CourseId") REFERENCES courses ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_course_subjects_subjects_SubjectId" FOREIGN KEY ("SubjectId") REFERENCES subjects ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_course_subjects_users_TeacherId" FOREIGN KEY ("TeacherId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE TABLE enrollments (
        "Id" uuid NOT NULL,
        "CourseId" uuid NOT NULL,
        "StudentId" uuid NOT NULL,
        "EnrolledAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_enrollments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_enrollments_courses_CourseId" FOREIGN KEY ("CourseId") REFERENCES courses ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_enrollments_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE TABLE assignments (
        "Id" uuid NOT NULL,
        "CourseSubjectId" uuid NOT NULL,
        "CreatedByTeacherId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Description" character varying(10000) NOT NULL,
        "MaxMarks" numeric(6,2) NOT NULL,
        "Deadline" timestamp with time zone NOT NULL,
        "Status" character varying(20) NOT NULL,
        "AllowLateSubmission" boolean NOT NULL,
        "AllowResubmission" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "PublishedAt" timestamp with time zone,
        CONSTRAINT "PK_assignments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_assignments_course_subjects_CourseSubjectId" FOREIGN KEY ("CourseSubjectId") REFERENCES course_subjects ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_assignments_users_CreatedByTeacherId" FOREIGN KEY ("CreatedByTeacherId") REFERENCES users ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE TABLE submissions (
        "Id" uuid NOT NULL,
        "AssignmentId" uuid NOT NULL,
        "StudentId" uuid NOT NULL,
        "Content" character varying(20000) NOT NULL,
        "AttachmentUrl" character varying(2000),
        "Status" character varying(20) NOT NULL,
        "IsLate" boolean NOT NULL,
        "AttemptCount" integer NOT NULL DEFAULT 1,
        "SubmittedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "Marks" numeric(6,2),
        "Feedback" character varying(5000),
        "GradedAt" timestamp with time zone,
        "GradedByTeacherId" uuid,
        CONSTRAINT "PK_submissions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_submissions_assignments_AssignmentId" FOREIGN KEY ("AssignmentId") REFERENCES assignments ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_submissions_users_GradedByTeacherId" FOREIGN KEY ("GradedByTeacherId") REFERENCES users ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_submissions_users_StudentId" FOREIGN KEY ("StudentId") REFERENCES users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_assignments_CourseSubjectId" ON assignments ("CourseSubjectId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_assignments_CreatedByTeacherId" ON assignments ("CreatedByTeacherId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_assignments_Deadline" ON assignments ("Deadline");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_assignments_Status" ON assignments ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_course_subjects_CourseId_SubjectId" ON course_subjects ("CourseId", "SubjectId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_course_subjects_SubjectId" ON course_subjects ("SubjectId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_course_subjects_TeacherId" ON course_subjects ("TeacherId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_courses_Code" ON courses ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_enrollments_CourseId_StudentId" ON enrollments ("CourseId", "StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_enrollments_StudentId" ON enrollments ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_subjects_Code" ON subjects ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_submissions_AssignmentId_StudentId" ON submissions ("AssignmentId", "StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_submissions_GradedByTeacherId" ON submissions ("GradedByTeacherId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_submissions_Status" ON submissions ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_submissions_StudentId" ON submissions ("StudentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_Email" ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    CREATE INDEX "IX_users_Role" ON users ("Role");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260805072040_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260805072040_InitialCreate', '10.0.10');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    DELETE FROM submissions;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    ALTER TABLE submissions DROP COLUMN "AttachmentUrl";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    ALTER TABLE submissions DROP COLUMN "Content";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    ALTER TABLE submissions ADD "ContentType" character varying(100) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    ALTER TABLE submissions ADD "FileName" character varying(255) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    ALTER TABLE submissions ADD "FileSizeBytes" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    CREATE TABLE submission_files (
        "SubmissionId" uuid NOT NULL,
        "Content" bytea NOT NULL,
        CONSTRAINT "PK_submission_files" PRIMARY KEY ("SubmissionId"),
        CONSTRAINT "FK_submission_files_submissions_SubmissionId" FOREIGN KEY ("SubmissionId") REFERENCES submissions ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260808064008_SubmitAssignmentsAsPdf') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260808064008_SubmitAssignmentsAsPdf', '10.0.10');
    END IF;
END $EF$;
COMMIT;

