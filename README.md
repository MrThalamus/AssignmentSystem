# Assignment & Submission Management System

A role-based web application for a school or college. Teachers create assignments for
a class and subject, students hand their work in as PDFs, and teachers return marks
and feedback.

- **Backend** — ASP.NET Core 10 Web API (C#), EF Core, PostgreSQL, JWT authentication, Swagger
- **Frontend** — Next.js 16 (App Router), React 19, TypeScript, Tailwind CSS
- **Tests** — 247 xUnit tests covering business rules, authorisation, the submission workflow and the upload endpoints over HTTP

---

## Table of contents

- [Live demo](#live-demo)
- [Demo credentials](#demo-credentials)
- [Running locally](#running-locally)
- [Database setup](#database-setup)
- [Running the tests](#running-the-tests)
- [Running with Docker (optional)](#running-with-docker-optional)
- [Deployment](#deployment)
- [Features by role](#features-by-role)
- [Business rules](#business-rules)
- [Architecture](#architecture)
- [Data model](#data-model)
- [API reference](#api-reference)
- [Project structure](#project-structure)
- [Assumptions](#assumptions)
- [Known limitations](#known-limitations)

---

## Live demo

| | |
| --- | --- |
| Application | <https://assignment-system-alpha.vercel.app> |
| API | <https://assignmentsystem.onrender.com> |
| Swagger | <https://assignmentsystem.onrender.com/swagger> |
| Health | <https://assignmentsystem.onrender.com/health> |
| Readiness | <https://assignmentsystem.onrender.com/health/db> |

> **The API sleeps when idle.** It runs on a free tier that shuts the container
> down after 15 minutes without traffic, so the **first request after a quiet
> period can take up to a minute** while it wakes and reconnects. Everything is
> immediate once it is warm. If the first page load looks stuck, give it a moment
> rather than assuming it is broken.

The login screen starts waking the host as soon as it loads, so the sign-in request
itself does not usually have to wait, and it says so on screen if the wait runs long.
To avoid the delay altogether, point an uptime checker at `/health/db` every ten
minutes — see [Keeping the demo warm](#keeping-the-demo-warm).

Sign in with any account from the table below. Please treat the deployment as a
throwaway demonstration — the credentials are published here, so anyone can sign
in as an administrator and change the data.

---

## Demo credentials

The demo dataset is inserted automatically the first time the API starts.

| Role | Email | Password | Teaches |
| --- | --- | --- | --- |
| Admin | `admin@school.edu` | `Admin@123` | — |
| Teacher | `habib.wahid@school.edu` | `Teacher@123` | Physics (Grade 10) |
| Teacher | `nazmul.hasan@school.edu` | `Teacher@123` | Maths (Grade 10), Computer Science (Grade 11) |
| Teacher | `farhana.akter@school.edu` | `Teacher@123` | English (Grade 10), Physics (Grade 11) |
| Student | `rafi.ahmed@school.edu` | `Student@123` | — |
| Student | `tasnim.jahan@school.edu` | `Student@123` | — |
| Student | `imran.kabir@school.edu` | `Student@123` | — |
| Student | `nusrat.sultana@school.edu` | `Student@123` | — |
| Student | `sabbir.hossain@school.edu` | `Student@123` | — |

Every teacher shares one password because it comes from a single configuration value
(`Seed__TeacherPassword`), not from a per-account secret.

The three teachers own **different** classes, and the students are enrolled in
**different** courses, so the role scoping is visible immediately: sign in as one
teacher and you will not see the other's assignments.

Passwords come from configuration (`Seed__AdminPassword` and friends) — they are not
hard-coded secrets. Change them before exposing the application to anyone.

---

## Running locally

### Prerequisites

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org) (developed against 24)
- [PostgreSQL 14+](https://www.postgresql.org/download/)

### 1. Create the database — optional

You do not need to do this. EF Core's `Migrate()` creates the database if it is
missing, so a running PostgreSQL server is all the API needs. Create it explicitly
only if you want a specific owner or encoding:

```bash
createdb assignment_system
```

### 2. Start the API

The API refuses to start without a JWT signing key of at least 32 bytes — that is
deliberate, so a deployment cannot accidentally sign tokens with a weak or empty key.

**bash / zsh**

```bash
cd backend/src/AssignmentSystem.Api

export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=assignment_system;Username=postgres;Password=postgres"
export Jwt__Key="$(openssl rand -base64 48)"
export ASPNETCORE_URLS="http://localhost:5080"

dotnet run
```

**PowerShell**

```powershell
cd backend\src\AssignmentSystem.Api

$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=assignment_system;Username=postgres;Password=postgres"
$env:Jwt__Key = [Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))
$env:ASPNETCORE_URLS = "http://localhost:5080"

dotnet run
```

Note the `__` (double underscore) — that is how ASP.NET Core maps an environment
variable onto a nested configuration section (`ConnectionStrings:DefaultConnection`).

On startup the API applies migrations, seeds the demo data, and serves Swagger at
<http://localhost:5080/swagger>.

### 3. Start the frontend

```bash
cd frontend
npm install

# Point the browser at the API. Also accepted as an environment variable.
echo "NEXT_PUBLIC_API_BASE_URL=http://localhost:5080" > .env.local

npm run dev
```

Open <http://localhost:3000> and sign in with any account from the table above.

---

## Database setup

There are three ways to get a working schema. **The first is the default and needs no
manual steps.**

### Option 1 — automatic (recommended)

The API runs `Database.MigrateAsync()` at startup and then seeds the demo data. Both
steps are switchable:

```
Database__AutoMigrate=false   # skip migrations
Seed__Enabled=false           # skip demo data
```

### Option 2 — EF Core CLI

```bash
dotnet tool install --global dotnet-ef      # once

cd backend
dotnet ef database update \
  --project src/AssignmentSystem.Infrastructure \
  --startup-project src/AssignmentSystem.Infrastructure
```

The `--startup-project` points at the Infrastructure project because it contains an
`IDesignTimeDbContextFactory`. This keeps the tooling from having to boot the API
host, which refuses to start without a JWT key that is irrelevant to a migration.

### Option 3 — plain SQL

For evaluators who would rather use `psql`. Both scripts are idempotent and can be
re-run safely.

```bash
createdb assignment_system
psql -d assignment_system -f database/01_schema.sql
psql -d assignment_system -f database/02_seed.sql
```

| File | Contents |
| --- | --- |
| `database/01_schema.sql` | Every table, index and foreign key. Generated from the EF migrations with `--idempotent`, so it also records them in `__EFMigrationsHistory` and the API will not try to re-apply them. |
| `database/02_seed.sql` | The same demo dataset the application seeder inserts, using the same fixed ids. Deadlines are relative to `now()`, so the data always contains a mix of open, overdue and closed work. No submissions — see below. |

**The demo data contains no submissions.** A submission is a PDF a student uploaded,
and seeding one would mean committing an invented document to the repository. So the
dataset stops at assignments waiting to be answered: sign in as a student, open a
published assignment and upload a real PDF. Grading, returning for revision and the
late flag are all reachable from there.

Both scripts were executed against PostgreSQL 17 during development and re-verified
against PostgreSQL 18 after the move to PDF submissions, each time run twice to
confirm they are idempotent.

---

## Running the tests

```bash
cd backend
dotnet test
```

**247 tests, all passing.** They are grouped by what they protect:

| Area | Tests | What they cover |
| --- | --- | --- |
| `Domain/AssignmentRulesTests` | 20 | Publish, unpublish, close, edit and delete rules on the entity itself |
| `Domain/SubmissionRulesTests` | 19 | On-time vs late, revision limits, grading bounds, return-for-revision |
| `Services/AssignmentAuthorizationTests` | 16 | Who can see and change which assignment |
| `Services/AssignmentWorkflowTests` | 15 | Creating, publishing, retiring, filtering, paging |
| `Services/SubmissionWorkflowTests` | 29 | The full submission workflow, PDF validation, download scoping |
| `Services/UserAndAcademicRuleTests` | 18 | Account, course, enrollment and teacher-assignment rules |
| `Api/SubmissionUploadEndpointTests` | 7 | The upload and download endpoints over real HTTP, on a live host |
| `Security/EndpointAuthorizationTests` | 75 | Reads `[Authorize]` back off every controller action |
| `Security/AuthenticationTests` | 9 | Login, password change, token claims |
| `Security/PasswordHashingTests` | 8 | PBKDF2 hashing, salting and malformed input |
| `Persistence/QueryTranslationTests` | 15 | Every list query compiles to SQL on a relational provider |
| `Persistence/PostgresConnectionStringTests` | 16 | Converting the URI form hosted providers issue into what Npgsql accepts |

Three of those deserve a note, because they exist to catch mistakes that are otherwise
invisible until runtime:

**`EndpointAuthorizationTests`** reads the `[Authorize]` attributes off the
controllers by reflection. Forgetting a role restriction on a new endpoint is easy and
silent, so a new action that is unprotected — or protected with the wrong roles — fails
the build rather than shipping.

**`QueryTranslationTests`** runs every list query against SQLite. The rest of the suite
uses the EF Core in-memory provider, which evaluates LINQ as plain objects and happily
accepts expressions that no database can translate. This actually happened during
development: two queries ordered their results *after* projecting to a DTO, which
passed in memory and threw `InvalidOperationException` against PostgreSQL. These tests
force each query to compile to real SQL.

**`SubmissionUploadEndpointTests`** boots the actual API in memory and talks to it over
HTTP. The service tests call services directly, which leaves everything between the
wire and the service untested — and a submission is `multipart/form-data`, not JSON, so
that gap covers the binding that turns a browser's upload into an `IFormFile`. The
round trip is asserted byte for byte: a PDF that gains or loses a single byte in
transit is a PDF that no longer opens.

### End-to-end verification

Beyond the unit tests, the running API was exercised against a real PostgreSQL
instance — authentication, role enforcement across all three roles, the submission
and grading workflow, and the assignment lifecycle — covering 56 request/response
assertions.

The PDF upload path was verified the same way against PostgreSQL 18: a real PDF
uploaded over `multipart/form-data`, downloaded again, and compared byte for byte;
a renamed non-PDF rejected with a per-field message; the file returning 404 to another
student and 401 to an anonymous caller while the owning teacher receives it; and
deleting the submission removing its `submission_files` row by cascade.

---

## Running with Docker (optional)

A Compose stack is included for anyone who would rather not install .NET, Node and
PostgreSQL separately. **[Running locally](#running-locally) is the primary route** —
it is the one these instructions were written and verified against.

```bash
cp .env.example .env
# edit .env: set POSTGRES_PASSWORD and a Jwt__Key of at least 32 bytes
docker compose up --build
```

| | |
| --- | --- |
| Web UI | <http://localhost:3000> |
| Swagger | <http://localhost:5080/swagger> |
| Health check | <http://localhost:5080/health> |
| Readiness check | <http://localhost:5080/health/db> |

The API applies its EF Core migrations and inserts the demo data on startup, so
there is nothing to create by hand.

Migrations are kept as an unbroken chain rather than being squashed, because a deployed
database records the ids it has already applied. Rewriting history — even into a
migration describing an identical schema — leaves the deployment with an id it has
never seen, and the API then tries to create tables that already exist and refuses to
start. Adding a migration is always safe; replacing one is not.

The backend image is the one deployed to Render, so it is exercised on every deploy.
The Compose stack as a whole has not been run on the development machine — see
[known limitations](#known-limitations).

---

## Deployment

The live demo runs on three free services: **Neon** for PostgreSQL, **Render** for
the API container, and **Vercel** for the frontend. Neon hosts the database rather
than Render because Render deletes free PostgreSQL instances after 30 days.

The order matters — each service needs a URL the previous step produces.

### 1. Database — Neon

Create a project and copy its connection string. Paste it exactly as given:

```
postgresql://user:password@ep-xxx.aws.neon.tech/neondb?sslmode=require
```

Npgsql itself only understands the keyword form, so the application converts the
URI on startup — see
[`PostgresConnectionString`](backend/src/AssignmentSystem.Infrastructure/Persistence/PostgresConnectionString.cs).
Either form works, and TLS is required by default. Rewriting the string by hand is
error-prone and no longer necessary.

### 2. API — Render

In the dashboard choose **New → Blueprint** and select this repository. Render reads
[`render.yaml`](render.yaml) and creates the service with the right Docker build,
health check and environment.

Supply the one value it asks for:

| Variable | Value |
| --- | --- |
| `ConnectionStrings__DefaultConnection` | the keyword-form string from step 1 |

`Jwt__Key` is generated by Render, so no signing key is ever committed. On first
boot the API creates the schema and inserts the demo data by itself.

Leave `Cors__AllowedOrigins__0` blank for now — the frontend does not exist yet.
Note the service URL, e.g. `https://assignment-system-api.onrender.com`.

### 3. Frontend — Vercel

Import the repository and set **Root Directory** to `frontend`. Before the first
build, add:

| Variable | Value |
| --- | --- |
| `NEXT_PUBLIC_API_BASE_URL` | the Render URL from step 2 |

`NEXT_PUBLIC_*` values are compiled into the browser bundle, so this must be set
**before** the build. Changing it later requires a redeploy, not just a restart.

### 4. Close the loop

Back in Render, set `Cors__AllowedOrigins__0` to the Vercel URL and redeploy. Until
this is done the browser blocks every API call as a cross-origin request, which
shows up as a login that silently fails.

### Keeping the demo warm

Two independent idle timers stand between a visitor and a fast first page: Render
stops the container after 15 minutes, and Neon suspends the database compute after
about five. Pointing a free uptime checker (UptimeRobot, cron-job.org) at
`/health/db` every ten minutes resets both, because that endpoint opens a real
connection — `/health` answers without touching the database and so keeps only the
container awake.

Watch the free allowance: Render grants roughly 750 instance-hours a month, and
staying up around the clock spends about 730 of them, which leaves nothing for a
second service. Scheduling the check for a twelve-hour daily window costs around 365
hours and still covers any working day.

### Refreshing the demo data

Deadlines are calculated relative to the moment the database is first seeded, and
seeding is idempotent, so it never rewrites them. After a few weeks every assignment
will read as overdue. To restore a realistic mix of open, overdue and closed work,
drop the tables in Neon and restart the Render service — it rebuilds and reseeds
from scratch.

---

## Features by role

### Admin

- Create accounts for any role; deactivate accounts; reset passwords
- Manage the subject catalogue and the course (class) list
- Add subjects to a course and assign the teacher responsible for each pairing
- Staff every class from one screen (**Teaching**), which flags teachers who have no
  class yet and subjects nobody is responsible for — a teacher cannot set any work
  until they are named against a class and subject
- Enrol and remove students
- See every assignment and submission in the system

### Teacher

- Create, edit and delete assignments for the course subjects they are responsible for
- Save as a draft, publish, revert to draft, or close an assignment
- Read every submission for their own assignments
- Award marks and write feedback
- Return work for revision, which lets a student resubmit even after the deadline

### Student

- See published and closed assignments for the courses they are enrolled in — never a draft
- Filter to work they have not yet handed in
- Submit their work as a PDF (up to 10 MB)
- Replace it before the deadline, if the assignment allows revision
- See their status, marks and the teacher's feedback

All three can change their own password.

---

## Business rules

These are enforced in the domain layer and covered by tests.

**Assignment lifecycle**

1. A new assignment starts as a **draft** and is invisible to students.
2. Publishing requires a deadline in the future.
3. A published assignment can be reverted to draft **only** while no one has submitted.
4. Closing stops submissions but leaves the assignment readable.
5. A closed assignment cannot be edited or re-published.
6. Maximum marks cannot change once any submission has been graded — otherwise already-awarded marks could exceed the new maximum.
7. An assignment with submissions cannot be deleted; it must be closed instead.
8. An assignment cannot be created for a course subject with no teacher, or for an inactive course.

**Submissions**

9. Only an enrolled student may submit, and only to a published assignment.
10. Submitting after the deadline is rejected unless the assignment allows late work, in which case it is accepted and flagged **Late**.
11. One submission per student per assignment — enforced by a unique index as well as in code. Posting again replaces the existing file and increments its attempt count.
12. A submission can be revised only while the assignment is open **and** allows revision.
13. Every submission is a PDF of at most 10 MB. The file's *bytes* must begin with `%PDF-`, so a renamed document is rejected rather than reaching a teacher who cannot open it. A revision overwrites the stored file.
14. Graded work is frozen; the teacher must return it before it can change.
15. Work returned for revision can be resubmitted **even after the deadline** — the teacher asked for it.
16. Marks must fall between zero and the assignment's maximum.
17. Returning a graded submission clears its marks, so the next attempt is graded fresh.

**Authorisation**

18. A teacher may only read and manage assignments for course subjects assigned to them.
19. A teacher may only grade submissions belonging to their own assignments.
20. A student may only read their own submissions, and cannot filter the list to another student.
21. Students are never shown class-wide submission counts.
22. An admin cannot deactivate their own account, and the last active administrator cannot be deactivated.

**Data integrity**

23. Accounts are deactivated, never deleted — assignments, submissions and marks reference them.
24. A subject that is taught somewhere cannot be deleted.
25. A course with assignments cannot be deleted.
26. A student who has submitted work cannot be removed from that course.
27. A course subject with assignments cannot be left without a teacher.

---

## Architecture

The backend follows a four-project clean architecture. Dependencies point inward:
`Api → Infrastructure → Application → Domain`.

```
AssignmentSystem.Domain          Entities, enums, and the rules that govern them.
                                 No dependencies on anything.

AssignmentSystem.Application     DTOs, validators, and the services that orchestrate
                                 a use case. Talks to the database through the
                                 IApplicationDbContext interface, never to EF directly.

AssignmentSystem.Infrastructure  EF Core DbContext and configuration, migrations,
                                 JWT issuing, password hashing, the seeder.

AssignmentSystem.Api             Controllers, authentication, Swagger, logging,
                                 and the global exception handler.
```

### Decisions worth explaining

**Rules live on the entities, not in the services.** `Assignment.Publish()`,
`Submission.Grade()` and their siblings enforce their own preconditions and throw
`BusinessRuleViolationException`. A service cannot put an assignment into an invalid
state by forgetting a check, and the rules can be tested without a database. Services
are left with the two things they are actually for: deciding *who* may call an
operation, and persisting the result.

**Authorisation is applied at the query, not after it.** Every read starts from
`ApplyRoleScope`, which narrows the queryable before any filter is applied. A student's
query cannot return another class's work even if a later filter is wrong, because those
rows were never in the result set. Role attributes on the controllers are a coarse first
gate; the fine-grained "is this yours" decision is in the service, where it has the data
to make it.

**`CourseSubject` is the unit of ownership.** Rather than relating an assignment to a
course and a subject independently, it hangs off the *pairing* of the two — "Maths, for
Grade 10-A" — which also names the teacher responsible. This makes both teacher
ownership and student visibility a single lookup instead of a guess spread across two
relationships, and it means reassigning a class to a new teacher transfers their
assignments in one update.

**404 rather than 403 where existence is itself a secret.** A student asking for a draft
assignment gets "not found", because telling them "forbidden" would confirm the draft
exists. A teacher asking about another teacher's assignment gets 403 on writes, since
staff already know their colleagues' classes exist.

**Login failures are indistinguishable.** An unknown email, a wrong password and a
deactivated account all return the same message, so the endpoint cannot be used to
discover which addresses are registered. There is a test asserting this.

**`IDateTimeProvider` instead of `DateTime.UtcNow`.** Deadlines are the heart of this
system, so tests drive the clock explicitly rather than sleeping or depending on when
they happen to run.

**Enums are stored and transmitted as text.** `Published`, `Graded` and so on are
readable in raw SQL and in API responses, and reordering the enum cannot silently
change the meaning of existing rows.

**Passwords use PBKDF2-HMAC-SHA256** with a per-password salt and 100,000 iterations,
with the iteration count stored alongside each hash so the work factor can be raised
later without locking anyone out. Verification is a fixed-time comparison.

---

## Data model

```
     users                          courses                    subjects
  ┌───────────┐                  ┌───────────┐              ┌───────────┐
  │ id        │                  │ id        │              │ id        │
  │ full_name │                  │ name      │              │ name      │
  │ email  ∪  │                  │ code   ∪  │              │ code   ∪  │
  │ password  │                  │ year      │              └─────┬─────┘
  │ role      │                  │ is_active │                    │
  │ is_active │                  └─────┬─────┘                    │
  └─────┬─────┘                        │                          │
        │                              │                          │
        │        ┌─────────────────────┴──────────────────────────┘
        │        │
        │   course_subjects ────────────── one subject, taught once per course,
        │   ┌──────────────┐               by one teacher
        ├──▶│ course_id    │
        │   │ subject_id   │  unique (course_id, subject_id)
        │   │ teacher_id ──┼──▶ users
        │   └───────┬──────┘
        │           │
        │           │        assignments
        │           │      ┌────────────────────┐
        │           └─────▶│ course_subject_id  │
        │                  │ created_by_teacher │
        ├─────────────────▶│ title, description │
        │                  │ max_marks          │
        │                  │ deadline           │
        │                  │ status             │  Draft | Published | Closed
        │                  │ allow_late         │
        │                  │ allow_resubmission │
        │                  └─────────┬──────────┘
        │                            │
        │      enrollments           │       submissions
        │   ┌──────────────┐         │     ┌────────────────────┐
        ├──▶│ student_id   │         └────▶│ assignment_id      │
        │   │ course_id    │               │ student_id      ───┼──▶ users
        │   └──────────────┘               │ file_name          │
        │     unique                       │ content_type       │
        │     (course_id, student_id)      │ file_size_bytes    │
        │                                  │ status             │  Submitted | Late
        │                                  │ is_late            │  Graded | Returned
        │                                  │ attempt_count      │
        │                                  │ marks, feedback    │
        └─────────────────────────────────▶│ graded_by_teacher  │
                                           └─────────┬──────────┘
                                             unique  │ 1:1
                                             (assignment_id, student_id)
                                                     ▼
                                           ┌────────────────────┐
                                           │  submission_files  │
                                           │ submission_id (PK) │
                                           │ content   (bytea)  │
                                           └────────────────────┘
```

**Why one `users` table for all three roles.** Admin, teacher and student share the
same identity fields; splitting them would add joins without adding information.
Role-specific data lives in the link tables — a teacher appears in
`course_subjects.teacher_id`, a student in `enrollments`.

**Delete behaviour** is chosen per relationship rather than left to the default:

| Relationship | On delete | Why |
| --- | --- | --- |
| `course_subjects` → `courses` | Cascade | Removing a course removes the subjects taught in it |
| `course_subjects` → `subjects` | Restrict | A subject in use must not disappear from a course |
| `course_subjects` → `users` (teacher) | Restrict | A teacher with classes must not be deletable |
| `assignments` → `course_subjects` | Cascade | An assignment is meaningless without its pairing |
| `assignments` → `users` (author) | Restrict | Preserve the audit trail |
| `submissions` → `assignments` | Cascade | Submitted work belongs to its assignment |
| `submissions` → `users` (student) | Cascade | A student's work goes with them |
| `submissions` → `users` (grader) | Set null | Keep the marks, forget who gave them |
| `submission_files` → `submissions` | Cascade | A withdrawn submission must not leave its PDF behind |

In practice these rarely fire, because the service layer refuses the delete first with
a clear message. They are the backstop.

---

## API reference

Every endpoint requires a bearer token except `POST /api/auth/login` and the two
health probes below. Full interactive documentation is at `/swagger`.

### Health — anonymous

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/health` | Liveness. Answers without touching the database, so a database outage cannot restart a healthy container. This is what Render probes. |
| GET | `/health/db` | Readiness. Opens a connection and runs `SELECT 1`; returns 503 if that fails. Also what an uptime checker should call to keep the serverless database awake. |

### Auth

| Method | Route | Roles |
| --- | --- | --- |
| POST | `/api/auth/login` | anonymous |
| GET | `/api/auth/me` | any |
| POST | `/api/auth/change-password` | any |

### Users — admin only

| Method | Route |
| --- | --- |
| GET | `/api/users` *(filters: role, isActive, search; paged)* |
| GET | `/api/users/{id}` |
| POST | `/api/users` |
| PUT | `/api/users/{id}` |
| POST | `/api/users/{id}/reset-password` |
| DELETE | `/api/users/{id}` *(deactivates)* |

### Subjects

| Method | Route | Roles |
| --- | --- | --- |
| GET | `/api/subjects` | Admin, Teacher |
| GET | `/api/subjects/{id}` | Admin, Teacher |
| POST · PUT · DELETE | `/api/subjects[/{id}]` | Admin |

### Courses

| Method | Route | Roles |
| --- | --- | --- |
| GET | `/api/courses` *(paged)* | Admin, Teacher |
| GET | `/api/courses/{id}` | Admin, Teacher |
| POST · PUT · DELETE | `/api/courses[/{id}]` | Admin |
| GET | `/api/courses/{id}/subjects` | Admin, Teacher |
| POST | `/api/courses/{id}/subjects` | Admin |
| PUT | `/api/courses/subjects/{id}/teacher` | Admin |
| DELETE | `/api/courses/subjects/{id}` | Admin |
| GET | `/api/courses/teachable-subjects` | Admin, Teacher |
| GET | `/api/courses/{id}/students` | Admin, Teacher |
| POST | `/api/courses/{id}/students` | Admin |
| DELETE | `/api/courses/{id}/students/{studentId}` | Admin |

### Assignments

| Method | Route | Roles |
| --- | --- | --- |
| GET | `/api/assignments` *(filters: course, subject, status, search, onlyPending, due range; paged)* | any — scoped by role |
| GET | `/api/assignments/{id}` | any — scoped by role |
| POST · PUT · DELETE | `/api/assignments[/{id}]` | Admin, Teacher |
| POST | `/api/assignments/{id}/publish` | Admin, Teacher |
| POST | `/api/assignments/{id}/unpublish` | Admin, Teacher |
| POST | `/api/assignments/{id}/close` | Admin, Teacher |
| GET | `/api/assignments/{id}/submissions` | Admin, Teacher |

### Submissions

| Method | Route | Roles |
| --- | --- | --- |
| GET | `/api/submissions` *(filters: assignment, course, student, status; paged)* | any — scoped by role |
| GET | `/api/submissions/{id}` | any — scoped by role |
| GET | `/api/submissions/{id}/file` *(the PDF itself)* | any — scoped by role |
| POST | `/api/submissions` *(`multipart/form-data`: `assignmentId`, `file`)* | Student |
| PUT | `/api/submissions/{id}` *(`multipart/form-data`: `file`)* | Student |
| POST | `/api/submissions/{id}/grade` | Admin, Teacher |
| PUT | `/api/submissions/{id}/status` | Admin, Teacher |

### Errors

Failures come back as RFC 7807 problem details:

| Status | Meaning |
| --- | --- |
| 400 | Validation failure — a `ValidationProblemDetails` with per-field messages |
| 401 | Missing, malformed or expired token |
| 403 | Authenticated, but not allowed to touch this record |
| 404 | Not found, or deliberately hidden from this caller |
| 409 | A business rule refused the request in the current state |
| 500 | Unexpected — details go to the log, never to the client |

---

## Project structure

```
AssignmentSystem/
├── README.md
├── docker-compose.yml
├── .env.example
│
├── backend/
│   ├── AssignmentSystem.slnx
│   ├── Dockerfile
│   ├── src/
│   │   ├── AssignmentSystem.Domain/
│   │   │   ├── Entities/            User, Subject, Course, CourseSubject,
│   │   │   │                        Enrollment, Assignment, Submission
│   │   │   ├── Enums/
│   │   │   └── Exceptions/
│   │   ├── AssignmentSystem.Application/
│   │   │   ├── Common/              Interfaces, exceptions, paging, validation
│   │   │   └── Features/            Auth, Users, Academics, Assignments, Submissions
│   │   │                            — DTOs, validators and services per feature
│   │   ├── AssignmentSystem.Infrastructure/
│   │   │   ├── Persistence/         DbContext, entity configuration, migrations, seeder
│   │   │   └── Security/            JWT, PBKDF2 hashing, system clock
│   │   └── AssignmentSystem.Api/
│   │       ├── Controllers/
│   │       ├── Middleware/          Global exception handler
│   │       ├── Security/            Current user, role constants
│   │       └── Program.cs
│   └── tests/AssignmentSystem.Tests/
│       ├── Domain/                  Entity rule tests
│       ├── Services/                Authorisation and workflow tests
│       ├── Security/                Auth, hashing, endpoint attribute tests
│       ├── Persistence/             SQL translation tests
│       └── TestSupport/             Fake clock, stub caller, seeded test world
│
├── database/
│   ├── 01_schema.sql
│   └── 02_seed.sql
│
└── frontend/
    ├── Dockerfile
    └── src/
        ├── app/
        │   ├── login/
        │   └── (app)/               Signed-in routes, sharing the app shell
        │       ├── assignments/     List, detail, new, edit
        │       ├── submissions/
        │       ├── admin/           Users, courses, subjects, teaching
        │       └── settings/
        ├── components/              App shell, UI primitives, feature panels
        └── lib/                     API client, auth context, formatting, types
```

---

## Assumptions

The brief left these open. Each was resolved the way a school would most likely expect,
and the reasoning is recorded here as requested.

1. **"Class or course" is one concept, not two.** A `Course` is the group of students an
   assignment is set for — "Grade 10 - Section A" works as well as "CSE 2nd Semester".
   Modelling both separately would have doubled the structure for no gain.

2. **A subject is taught by one teacher per course.** Co-teaching would need a
   many-to-many between `course_subjects` and teachers; the single-teacher rule keeps
   ownership unambiguous, which is what the authorisation rules depend on.

3. **A student has one submission per assignment.** Re-submitting replaces the PDF and
   increments `attempt_count` rather than creating a second row, so "the student's
   work" is never ambiguous when grading. The history of previous attempts is not
   retained — see limitations.

4. **A submission is a PDF, and the PDF lives in the database.** One format keeps
   grading predictable: every teacher can open every submission without hunting for an
   application, and the file renders inline in the browser. It is accepted only if the
   bytes really start with `%PDF-`, so renaming a `.docx` does not get past it, and it
   is capped at 10 MB.

   The bytes go in a `submission_files` table (`bytea`), one row per submission, rather
   than in object storage. The API runs on a free tier with an ephemeral filesystem, so
   local disk would lose every upload on restart, and S3 would add a credential and a
   second failure mode for a dataset measured in megabytes. Keeping the blob in its own
   table — never joined by the list and grading queries — means a page of submissions
   still costs kilobytes; only `GET /api/submissions/{id}/file` ever reads a file.
   Object storage is the right answer at a scale this project does not have.

5. **A role cannot be changed after an account is created.** Moving an account between
   roles would strand its enrollments or teaching assignments. Deactivate and create a
   new account instead.

6. **Accounts are deactivated, never deleted.** Assignments, submissions and marks
   reference their author. `DELETE /api/users/{id}` deactivates, and a deactivated
   account cannot log in.

7. **Publishing requires a future deadline, but saving a draft does not.** A teacher
   drafting work should not be blocked by a placeholder date; students should never see
   work they cannot hand in on time.

8. **A closed assignment stays readable.** Students keep access to the description,
   their submission and their marks after the window shuts.

9. **"Change the submission status when necessary"** is implemented as
   `PUT /api/submissions/{id}/status`, restricted so that *Graded* cannot be set without
   marks. Returning work clears the previous result so the next attempt is marked fresh.

10. **Times are UTC throughout.** The API accepts and returns ISO-8601 UTC; the browser
    renders deadlines in the viewer's own time zone.

11. **Access tokens only, valid for 8 hours.** Refresh tokens and rotation are real
    production requirements but add moving parts the brief did not ask for. See
    limitations.

12. **Students see their own results only.** Class-wide submission and grading counts
    are staff information and are zeroed out in a student's response.

---

## Known limitations

Stated plainly rather than hidden.

- **No refresh tokens.** When the 8-hour access token expires the user signs in again.
  Production would want short-lived access tokens plus rotating refresh tokens in an
  httpOnly cookie.

- **The token is held in `localStorage`.** This is the pragmatic choice for a
  client-rendered SPA, but it is readable by any script on the page, so it trades XSS
  resistance for simplicity. An httpOnly cookie with CSRF protection is the safer
  production answer.

- **Frontend route guards are cosmetic.** Every rule is enforced by the API — the UI
  guards exist so a mistyped URL shows a clear message instead of a wall of 403s. Role
  enforcement is *only* trustworthy because it is server-side.

- **No submission history.** A revision replaces the previous PDF. Keeping every attempt
  would mean a separate `submission_attempts` table and a file row per attempt.

- **Uploads are not virus-scanned.** The bytes are checked for being a PDF and nothing
  more. A real deployment would pass them through a scanner before storing them, and
  would serve them from a separate origin so a malicious file cannot reach the app's
  cookies. Here they are stored as received and served with a fixed `application/pdf`
  content type, never the one the uploader claimed.

- **PDFs are stored in PostgreSQL, not object storage.** Fine at this size, per
  assumption 4; at real volume it would bloat backups and every restore.

- **No notifications.** No email or in-app alert when work is set, submitted or graded.

- **No rate limiting on login.** Passwords are hashed with a 100k-iteration KDF, which
  slows offline attacks, but nothing throttles online guessing. Production needs
  per-IP and per-account limits, and lockout.

- **The in-memory test provider does not validate SQL.** This is why
  `QueryTranslationTests` exists — the gap is closed, but only for the queries it
  covers. New list queries should be added to it.

- **SQLite stands in for PostgreSQL in those translation tests.** It proves a query
  compiles to SQL, not that PostgreSQL produces identical results. Behavioural
  correctness is covered by the service tests, and the API was verified end-to-end
  against real PostgreSQL.

- **The Docker Compose stack has not been run end to end.** Docker was not available
  on the development machine, so the local instructions are what the setup steps were
  verified against. The backend image is exercised on every deploy to Render, but the
  frontend image and the Compose file itself have not been built — hence Docker being
  offered as an optional convenience rather than the headline setup route.

- **Pagination is offset-based**, which is fine at this scale but drifts under
  concurrent inserts on large tables. Keyset pagination would be the fix.

- **`Seed__Enabled` defaults to true.** Convenient for evaluation; it should be turned
  off for any real deployment.
