"use client";

import Link from "next/link";
import { useState } from "react";
import { PageHeader, RequireRole } from "@/components/app-shell";
import { Alert, Badge, Card, CardHeader, EmptyState, Select, Spinner } from "@/components/ui";
import { api } from "@/lib/api";
import type { CourseSubject, User } from "@/lib/types";
import { messageFor, useAsync } from "@/lib/use-async";

/**
 * Who teaches what, across every course at once.
 *
 * The same assignment can be made from a course's own page, but only one course at a
 * time, which makes the question an administrator actually asks - "this teacher has
 * nothing to teach, where do I fix that?" - impossible to answer without opening every
 * course in turn. This page answers it directly: idle teachers and unassigned classes
 * are surfaced at the top, and the dropdown that fixes either is on the same screen.
 */
export default function TeachingAssignmentsPage() {
  const { data, error, isLoading, reload } = useAsync(async () => {
    // Admins get every pairing from this endpoint, not just their own.
    const [courseSubjects, teacherPage] = await Promise.all([
      api.courses.teachableSubjects(),
      api.users.list({ role: "Teacher", isActive: true, pageSize: 100 }),
    ]);

    return { courseSubjects, teachers: teacherPage.items };
  }, []);

  const [actionError, setActionError] = useState<string | null>(null);

  const assign = async (courseSubjectId: string, teacherId: string | null) => {
    setActionError(null);

    try {
      await api.courses.assignTeacher(courseSubjectId, teacherId);
      reload();
    } catch (cause) {
      setActionError(messageFor(cause));
    }
  };

  if (isLoading) return <Spinner label="Loading teaching assignments" />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  const { courseSubjects, teachers } = data;

  const idleTeachers = teachers.filter(
    (teacher) => !courseSubjects.some((cs) => cs.teacherId === teacher.id),
  );

  const unassigned = courseSubjects.filter((cs) => cs.teacherId === null);

  // Grouped so the list reads as a timetable rather than a flat pile of pairings.
  const byCourse = new Map<string, CourseSubject[]>();
  for (const courseSubject of courseSubjects) {
    const existing = byCourse.get(courseSubject.courseId);
    if (existing) existing.push(courseSubject);
    else byCourse.set(courseSubject.courseId, [courseSubject]);
  }

  return (
    <RequireRole roles={["Admin"]}>
      <PageHeader
        title="Teaching assignments"
        description="A teacher can only create assignments for a class and subject they are named on."
      />

      {actionError && (
        <div className="mb-4">
          <Alert>{actionError}</Alert>
        </div>
      )}

      <div className="mb-4 space-y-3">
        {idleTeachers.length > 0 && (
          <Alert tone="warning" title="Teachers with nothing to teach">
            <p>
              {listNames(idleTeachers)} {idleTeachers.length === 1 ? "has" : "have"} no class
              assigned, so {idleTeachers.length === 1 ? "they" : "they"} cannot create any
              assignment yet. Name {idleTeachers.length === 1 ? "them" : "them"} against a subject
              below.
            </p>
          </Alert>
        )}

        {unassigned.length > 0 && (
          <Alert tone="info" title="Classes with no teacher">
            {unassigned.length} {unassigned.length === 1 ? "subject has" : "subjects have"} nobody
            responsible, so no work can be set for {unassigned.length === 1 ? "it" : "them"}.
          </Alert>
        )}
      </div>

      {courseSubjects.length === 0 ? (
        <EmptyState
          title="No classes to staff yet"
          description="Add a subject to a course first, then come back to name a teacher for it."
        />
      ) : (
        <div className="space-y-6">
          {[...byCourse.values()].map((subjects) => (
            <Card key={subjects[0].courseId}>
              <CardHeader
                title={subjects[0].courseName}
                description={subjects[0].courseCode}
                actions={
                  <Link
                    href={`/admin/courses/${subjects[0].courseId}`}
                    className="text-sm font-medium text-slate-900 underline"
                  >
                    Manage course
                  </Link>
                }
              />

              <ul className="divide-y divide-slate-200">
                {subjects.map((courseSubject) => (
                  <li
                    key={courseSubject.id}
                    className="flex flex-wrap items-center justify-between gap-3 px-5 py-4"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-slate-900">
                        {courseSubject.subjectName}{" "}
                        <span className="font-normal text-slate-500">
                          ({courseSubject.subjectCode})
                        </span>
                      </p>
                      {courseSubject.teacherId === null && (
                        <Badge tone="warning">No teacher assigned</Badge>
                      )}
                    </div>

                    <Select
                      aria-label={`Teacher for ${courseSubject.subjectName} in ${courseSubject.courseName}`}
                      className="w-auto min-w-56"
                      value={courseSubject.teacherId ?? ""}
                      onChange={(event) => assign(courseSubject.id, event.target.value || null)}
                    >
                      <option value="">No teacher assigned</option>
                      {teachers.map((teacher) => (
                        <option key={teacher.id} value={teacher.id}>
                          {teacher.fullName}
                        </option>
                      ))}
                    </Select>
                  </li>
                ))}
              </ul>
            </Card>
          ))}
        </div>
      )}
    </RequireRole>
  );
}

/** "Ayesha Rahman", or "Ayesha Rahman, Imran Khan and Sadia Noor". */
function listNames(users: User[]) {
  const names = users.map((user) => user.fullName);

  if (names.length === 1) return names[0];

  return `${names.slice(0, -1).join(", ")} and ${names[names.length - 1]}`;
}
