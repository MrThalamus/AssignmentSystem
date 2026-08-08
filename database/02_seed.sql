-- =============================================================================
--  Assignment & Submission Management System - demo data
-- =============================================================================
--  Run 01_schema.sql first.
--
--  This mirrors the seeder that runs automatically when the API starts, and is
--  provided for evaluators who would rather load the data with psql. Running
--  both is safe: every insert is ON CONFLICT DO NOTHING and the ids are the same
--  fixed values the application seeder uses.
--
--  Deadlines are relative to the moment the script runs, so the dataset always
--  contains a mix of open, overdue and closed work.
--
--  Demo credentials (passwords are PBKDF2-HMAC-SHA256, 100k iterations):
--    Admin    admin@school.edu          Admin@123
--    Teacher  nazmul.hasan@school.edu   Teacher@123
--    Teacher  farhana.akter@school.edu  Teacher@123
--    Student  rafi.ahmed@school.edu     Student@123
--    ...all other students use Student@123
--
--  Change these passwords before exposing the application to anyone.
-- =============================================================================

BEGIN;

-- ----------------------------------------------------------------- users ----

INSERT INTO users ("Id", "FullName", "Email", "PasswordHash", "Role", "IsActive", "CreatedAt")
VALUES
  ('00000000-0000-0000-0000-000000000001', 'Ayesha Rahman',  'admin@school.edu',
   '100000.VeV976eITElZqgUZN4bXsA==.qlE+36b8iJfvJH7lelFl7GiMK3GqYFpgXKfKwb/yvDw=', 'Admin',   TRUE, now()),

  ('00000000-0000-0000-0000-000000000011', 'Nazmul Hasan',   'nazmul.hasan@school.edu',
   '100000.7WZjfRZtdwwyQ/LpxDnLFg==.gEAaDjy/pL0+VxA6K4bw72WBIXFZ4VlrYVVPufaep4c=', 'Teacher', TRUE, now()),
  ('00000000-0000-0000-0000-000000000012', 'Farhana Akter',  'farhana.akter@school.edu',
   '100000.7WZjfRZtdwwyQ/LpxDnLFg==.gEAaDjy/pL0+VxA6K4bw72WBIXFZ4VlrYVVPufaep4c=', 'Teacher', TRUE, now()),

  ('00000000-0000-0000-0000-000000000021', 'Rafi Ahmed',     'rafi.ahmed@school.edu',
   '100000.uqxzmEbxfcPnQXitiQwkeQ==.QY+OCXj+IWV+9GEa07tDsHWcI9LXR0CaO5i1FyK8v6g=', 'Student', TRUE, now()),
  ('00000000-0000-0000-0000-000000000022', 'Tasnim Jahan',   'tasnim.jahan@school.edu',
   '100000.uqxzmEbxfcPnQXitiQwkeQ==.QY+OCXj+IWV+9GEa07tDsHWcI9LXR0CaO5i1FyK8v6g=', 'Student', TRUE, now()),
  ('00000000-0000-0000-0000-000000000023', 'Imran Kabir',    'imran.kabir@school.edu',
   '100000.uqxzmEbxfcPnQXitiQwkeQ==.QY+OCXj+IWV+9GEa07tDsHWcI9LXR0CaO5i1FyK8v6g=', 'Student', TRUE, now()),
  ('00000000-0000-0000-0000-000000000024', 'Nusrat Sultana', 'nusrat.sultana@school.edu',
   '100000.uqxzmEbxfcPnQXitiQwkeQ==.QY+OCXj+IWV+9GEa07tDsHWcI9LXR0CaO5i1FyK8v6g=', 'Student', TRUE, now()),
  ('00000000-0000-0000-0000-000000000025', 'Sabbir Hossain', 'sabbir.hossain@school.edu',
   '100000.uqxzmEbxfcPnQXitiQwkeQ==.QY+OCXj+IWV+9GEa07tDsHWcI9LXR0CaO5i1FyK8v6g=', 'Student', TRUE, now())
ON CONFLICT ("Id") DO NOTHING;

-- -------------------------------------------------------------- subjects ----

INSERT INTO subjects ("Id", "Name", "Code", "Description", "CreatedAt")
VALUES
  ('00000000-0000-0000-0000-000000000101', 'Mathematics',      'MATH-101', 'Algebra, geometry and trigonometry.',            now()),
  ('00000000-0000-0000-0000-000000000102', 'Physics',          'PHY-101',  'Mechanics, waves and electricity.',             now()),
  ('00000000-0000-0000-0000-000000000103', 'Computer Science', 'CSE-101',  'Programming fundamentals and data structures.', now()),
  ('00000000-0000-0000-0000-000000000104', 'English',          'ENG-101',  'Literature and composition.',                   now())
ON CONFLICT ("Id") DO NOTHING;

-- --------------------------------------------------------------- courses ----

INSERT INTO courses ("Id", "Name", "Code", "AcademicYear", "IsActive", "CreatedAt")
VALUES
  ('00000000-0000-0000-0000-000000000201', 'Grade 10 - Section A', 'G10-A',   '2026', TRUE, now()),
  ('00000000-0000-0000-0000-000000000202', 'Grade 11 - Science',   'G11-SCI', '2026', TRUE, now())
ON CONFLICT ("Id") DO NOTHING;

-- ------------------------------------------------------- course subjects ----
-- Each row is one teacher's responsibility: "this subject, for this class".

INSERT INTO course_subjects ("Id", "CourseId", "SubjectId", "TeacherId", "CreatedAt")
VALUES
  ('00000000-0000-0000-0000-000000000301', '00000000-0000-0000-0000-000000000201',
   '00000000-0000-0000-0000-000000000101', '00000000-0000-0000-0000-000000000011', now()),
  ('00000000-0000-0000-0000-000000000302', '00000000-0000-0000-0000-000000000201',
   '00000000-0000-0000-0000-000000000104', '00000000-0000-0000-0000-000000000012', now()),
  ('00000000-0000-0000-0000-000000000303', '00000000-0000-0000-0000-000000000202',
   '00000000-0000-0000-0000-000000000102', '00000000-0000-0000-0000-000000000012', now()),
  ('00000000-0000-0000-0000-000000000304', '00000000-0000-0000-0000-000000000202',
   '00000000-0000-0000-0000-000000000103', '00000000-0000-0000-0000-000000000011', now())
ON CONFLICT ("Id") DO NOTHING;

-- ----------------------------------------------------------- enrollments ----
-- Imran sits in both classes, which makes cross-course filtering visible.

INSERT INTO enrollments ("Id", "CourseId", "StudentId", "EnrolledAt")
VALUES
  ('00000000-0000-0000-0000-000000000401', '00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000021', now()),
  ('00000000-0000-0000-0000-000000000402', '00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000022', now()),
  ('00000000-0000-0000-0000-000000000403', '00000000-0000-0000-0000-000000000201', '00000000-0000-0000-0000-000000000023', now()),
  ('00000000-0000-0000-0000-000000000404', '00000000-0000-0000-0000-000000000202', '00000000-0000-0000-0000-000000000023', now()),
  ('00000000-0000-0000-0000-000000000405', '00000000-0000-0000-0000-000000000202', '00000000-0000-0000-0000-000000000024', now()),
  ('00000000-0000-0000-0000-000000000406', '00000000-0000-0000-0000-000000000202', '00000000-0000-0000-0000-000000000025', now())
ON CONFLICT ("Id") DO NOTHING;

-- ----------------------------------------------------------- assignments ----

INSERT INTO assignments (
  "Id", "CourseSubjectId", "CreatedByTeacherId", "Title", "Description",
  "MaxMarks", "Deadline", "Status", "AllowLateSubmission", "AllowResubmission",
  "CreatedAt", "PublishedAt")
VALUES
  -- Open, plenty of time left.
  ('00000000-0000-0000-0000-000000000501', '00000000-0000-0000-0000-000000000301',
   '00000000-0000-0000-0000-000000000011',
   'Quadratic Equations Worksheet',
   'Solve the ten quadratic equations in chapter 4 and show each step of your working.',
   20.00, now() + interval '7 days', 'Published', FALSE, TRUE,
   now() - interval '3 days', now() - interval '3 days'),

  -- Open, due soon.
  ('00000000-0000-0000-0000-000000000502', '00000000-0000-0000-0000-000000000302',
   '00000000-0000-0000-0000-000000000012',
   'Descriptive Essay: A Place I Remember',
   'Write a 500-word descriptive essay about a place that matters to you.',
   25.00, now() + interval '2 days', 'Published', TRUE, TRUE,
   now() - interval '5 days', now() - interval '5 days'),

  -- Overdue but still accepting work, so late submissions can be demonstrated.
  ('00000000-0000-0000-0000-000000000503', '00000000-0000-0000-0000-000000000303',
   '00000000-0000-0000-0000-000000000012',
   'Laws of Motion - Lab Report',
   'Submit the lab report for the trolley-and-ramp experiment, including your data table.',
   30.00, now() - interval '2 days', 'Published', TRUE, TRUE,
   now() - interval '12 days', now() - interval '12 days'),

  -- Draft: visible to its teacher and admins only.
  ('00000000-0000-0000-0000-000000000504', '00000000-0000-0000-0000-000000000304',
   '00000000-0000-0000-0000-000000000011',
   'Sorting Algorithms Comparison',
   'Implement bubble sort and merge sort, then compare their running times on the given inputs.',
   40.00, now() + interval '14 days', 'Draft', FALSE, TRUE,
   now() - interval '1 day', NULL),

  -- Closed: still readable, no longer accepting work.
  ('00000000-0000-0000-0000-000000000505', '00000000-0000-0000-0000-000000000301',
   '00000000-0000-0000-0000-000000000011',
   'Geometry Problem Set',
   'Prove the six geometry statements listed in the handout.',
   15.00, now() - interval '10 days', 'Closed', FALSE, FALSE,
   now() - interval '20 days', now() - interval '20 days')
ON CONFLICT ("Id") DO NOTHING;

-- ----------------------------------------------------------- submissions ----
--
-- Deliberately empty. A submission is a PDF a student uploaded, so seeding one would
-- mean committing an invented document to the repository. Sign in as a student, open
-- a published assignment and upload a real PDF instead - the graded, late and
-- returned states are all reachable from there by grading or returning the work as
-- the teacher who owns the assignment.

COMMIT;
