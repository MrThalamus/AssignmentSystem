"use client";

import { useParams } from "next/navigation";
import { useState } from "react";
import { PageHeader, RequireRole } from "@/components/app-shell";
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  EmptyState,
  Field,
  Select,
  Spinner,
} from "@/components/ui";
import { api } from "@/lib/api";
import { formatDate } from "@/lib/format";
import { messageFor, useAsync } from "@/lib/use-async";

/**
 * Everything an administrator sets up for one class: which subjects it covers, who
 * teaches each of them, and which students are enrolled.
 */
export default function CourseDetailPage() {
  const { id } = useParams<{ id: string }>();

  const { data, error, isLoading, reload } = useAsync(
    async () => ({
      course: await api.courses.get(id),
      courseSubjects: await api.courses.listSubjects(id),
      enrollments: await api.courses.listStudents(id),
      subjects: await api.subjects.list(),
      teachers: (await api.users.list({ role: "Teacher", isActive: true, pageSize: 100 })).items,
      students: (await api.users.list({ role: "Student", isActive: true, pageSize: 100 })).items,
    }),
    [id],
  );

  const [actionError, setActionError] = useState<string | null>(null);

  const run = async (action: () => Promise<unknown>) => {
    setActionError(null);

    try {
      await action();
      reload();
    } catch (cause) {
      setActionError(messageFor(cause));
    }
  };

  if (isLoading) return <Spinner label="Loading the course" />;
  if (error) return <Alert>{error}</Alert>;
  if (!data) return null;

  const { course, courseSubjects, enrollments, subjects, teachers, students } = data;

  const unusedSubjects = subjects.filter(
    (subject) => !courseSubjects.some((cs) => cs.subjectId === subject.id),
  );

  const unenrolledStudents = students.filter(
    (student) => !enrollments.some((enrollment) => enrollment.studentId === student.id),
  );

  return (
    <RequireRole roles={["Admin"]}>
      <PageHeader
        title={course.name}
        description={`${course.code} · academic year ${course.academicYear}`}
        actions={
          <Badge tone={course.isActive ? "success" : "neutral"}>
            {course.isActive ? "Active" : "Inactive"}
          </Badge>
        }
      />

      {actionError && (
        <div className="mb-4">
          <Alert>{actionError}</Alert>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader
            title="Subjects and teachers"
            description="A teacher can only create assignments for a subject they are named on."
          />

          {courseSubjects.length === 0 && (
            <EmptyState
              title="No subjects yet"
              description="Add a subject so teachers can set work for this class."
            />
          )}

          {courseSubjects.length > 0 && (
            <ul className="divide-y divide-slate-200">
              {courseSubjects.map((courseSubject) => (
                <li key={courseSubject.id} className="space-y-2 px-5 py-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="text-sm font-semibold text-slate-900">
                      {courseSubject.subjectName}{" "}
                      <span className="font-normal text-slate-500">
                        ({courseSubject.subjectCode})
                      </span>
                    </p>
                    <Button
                      variant="ghost"
                      onClick={() => run(() => api.courses.removeSubject(courseSubject.id))}
                    >
                      Remove
                    </Button>
                  </div>

                  <Select
                    aria-label={`Teacher for ${courseSubject.subjectName}`}
                    value={courseSubject.teacherId ?? ""}
                    onChange={(event) =>
                      run(() =>
                        api.courses.assignTeacher(courseSubject.id, event.target.value || null),
                      )
                    }
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
          )}

          {unusedSubjects.length > 0 && (
            <div className="border-t border-slate-200 px-5 py-4">
              <Field label="Add a subject to this course" htmlFor="addSubject">
                <Select
                  id="addSubject"
                  value=""
                  onChange={(event) => {
                    if (!event.target.value) return;
                    run(() => api.courses.addSubject(id, { subjectId: event.target.value }));
                  }}
                >
                  <option value="">Choose a subject...</option>
                  {unusedSubjects.map((subject) => (
                    <option key={subject.id} value={subject.id}>
                      {subject.name} ({subject.code})
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
          )}
        </Card>

        <Card>
          <CardHeader
            title="Enrolled students"
            description={`${enrollments.length} student${enrollments.length === 1 ? "" : "s"} can see this class's published work.`}
          />

          {enrollments.length === 0 && <EmptyState title="Nobody is enrolled yet" />}

          {enrollments.length > 0 && (
            <ul className="divide-y divide-slate-200">
              {enrollments.map((enrollment) => (
                <li
                  key={enrollment.id}
                  className="flex flex-wrap items-center justify-between gap-3 px-5 py-3"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-slate-900">
                      {enrollment.studentName}
                    </p>
                    <p className="truncate text-xs text-slate-500">
                      {enrollment.studentEmail} &middot; enrolled {formatDate(enrollment.enrolledAt)}
                    </p>
                  </div>
                  <Button
                    variant="ghost"
                    onClick={() => run(() => api.courses.removeStudent(id, enrollment.studentId))}
                  >
                    Remove
                  </Button>
                </li>
              ))}
            </ul>
          )}

          {unenrolledStudents.length > 0 && (
            <div className="border-t border-slate-200 px-5 py-4">
              <Field label="Enroll a student" htmlFor="addStudent">
                <Select
                  id="addStudent"
                  value=""
                  onChange={(event) => {
                    if (!event.target.value) return;
                    run(() => api.courses.enrollStudents(id, [event.target.value]));
                  }}
                >
                  <option value="">Choose a student...</option>
                  {unenrolledStudents.map((student) => (
                    <option key={student.id} value={student.id}>
                      {student.fullName} ({student.email})
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
          )}
        </Card>
      </div>
    </RequireRole>
  );
}
