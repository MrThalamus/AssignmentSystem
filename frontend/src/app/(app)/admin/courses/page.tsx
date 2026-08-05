"use client";

import Link from "next/link";
import { useState } from "react";
import { PageHeader, RequireRole } from "@/components/app-shell";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Modal,
  Pagination,
  Spinner,
} from "@/components/ui";
import { api } from "@/lib/api";
import { messageFor, useAsync } from "@/lib/use-async";

const PAGE_SIZE = 12;

export default function CoursesPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);

  const { data, error, isLoading, reload } = useAsync(
    () => api.courses.list({ page, pageSize: PAGE_SIZE, search: search || undefined }),
    [page, search],
  );

  return (
    <RequireRole roles={["Admin"]}>
      <PageHeader
        title="Courses"
        description="A course is the class an assignment is set for. Open one to manage its subjects, teachers and students."
        actions={<Button onClick={() => setCreating(true)}>Add course</Button>}
      />

      <Card>
        <div className="border-b border-slate-200 px-5 py-4 sm:max-w-xs">
          <Field label="Search" htmlFor="search">
            <Input
              id="search"
              placeholder="Name or code"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
          </Field>
        </div>

        {isLoading && <Spinner label="Loading courses" />}
        {error && (
          <div className="px-5 py-4">
            <Alert>{error}</Alert>
          </div>
        )}

        {data && !isLoading && data.items.length === 0 && <EmptyState title="No courses found" />}

        {data && data.items.length > 0 && (
          <ul className="divide-y divide-slate-200">
            {data.items.map((course) => (
              <li key={course.id}>
                <Link
                  href={`/admin/courses/${course.id}`}
                  className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 transition hover:bg-slate-50"
                >
                  <div>
                    <div className="flex items-center gap-2">
                      <p className="text-sm font-semibold text-slate-900">{course.name}</p>
                      <Badge tone={course.isActive ? "success" : "neutral"}>
                        {course.isActive ? "Active" : "Inactive"}
                      </Badge>
                    </div>
                    <p className="mt-0.5 text-sm text-slate-500">
                      {course.code} &middot; academic year {course.academicYear}
                    </p>
                  </div>

                  <p className="text-sm text-slate-600">
                    {course.enrolledStudentCount} student
                    {course.enrolledStudentCount === 1 ? "" : "s"} &middot; {course.subjectCount}{" "}
                    subject{course.subjectCount === 1 ? "" : "s"}
                  </p>
                </Link>
              </li>
            ))}
          </ul>
        )}

        {data && (
          <Pagination
            page={data.page}
            totalPages={data.totalPages}
            totalCount={data.totalCount}
            onChange={setPage}
          />
        )}
      </Card>

      {creating && (
        <CourseDialog
          onClose={() => setCreating(false)}
          onSaved={() => {
            setCreating(false);
            reload();
          }}
        />
      )}
    </RequireRole>
  );
}

function CourseDialog({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [academicYear, setAcademicYear] = useState(String(new Date().getFullYear()));
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const codeError =
    code.trim() === ""
      ? "Enter a code."
      : /^[A-Za-z0-9-]+$/.test(code.trim())
        ? undefined
        : "Use letters, digits and hyphens only.";

  const save = async () => {
    if (!name.trim() || codeError || !academicYear.trim()) return;

    setBusy(true);
    setFailure(null);

    try {
      await api.courses.create({
        name: name.trim(),
        code: code.trim(),
        academicYear: academicYear.trim(),
        isActive: true,
      });
      onSaved();
    } catch (cause) {
      setFailure(messageFor(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title="Add course" onClose={onClose}>
      <div className="space-y-4">
        {failure && <Alert>{failure}</Alert>}

        <Field label="Name" htmlFor="name">
          <Input
            id="name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Grade 10 - Section A"
          />
        </Field>

        <Field label="Code" htmlFor="code" error={code ? codeError : undefined}>
          <Input
            id="code"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="G10-A"
          />
        </Field>

        <Field label="Academic year" htmlFor="year">
          <Input
            id="year"
            value={academicYear}
            onChange={(event) => setAcademicYear(event.target.value)}
          />
        </Field>

        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} loading={busy} disabled={!name.trim() || Boolean(codeError)}>
            Add course
          </Button>
        </div>
      </div>
    </Modal>
  );
}
