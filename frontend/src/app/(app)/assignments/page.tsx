"use client";

import Link from "next/link";
import { useState } from "react";
import { PageHeader } from "@/components/app-shell";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Pagination,
  Select,
  Spinner,
} from "@/components/ui";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import {
  assignmentStatusTone,
  formatDateTime,
  formatRelative,
  isOverdue,
  submissionStatusLabel,
  submissionStatusTone,
} from "@/lib/format";
import type { Assignment } from "@/lib/types";
import { useAsync } from "@/lib/use-async";

const PAGE_SIZE = 10;

export default function AssignmentsPage() {
  const { user } = useAuth();
  const isStaff = user?.role === "Admin" || user?.role === "Teacher";

  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [status, setStatus] = useState("");
  const [onlyPending, setOnlyPending] = useState(false);

  const { data, error, isLoading } = useAsync(
    () =>
      api.assignments.list({
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        status: status || undefined,
        onlyPending: onlyPending || undefined,
      }),
    [page, search, status, onlyPending],
  );

  // Any filter change invalidates the current page number.
  const updateFilter = (apply: () => void) => {
    apply();
    setPage(1);
  };

  return (
    <>
      <PageHeader
        title="Assignments"
        description={
          isStaff
            ? "Work you have set, across your classes and subjects."
            : "Work set for the classes you are enrolled in."
        }
        actions={
          isStaff ? (
            <Link href="/assignments/new">
              <Button>New assignment</Button>
            </Link>
          ) : undefined
        }
      />

      <Card>
        <div className="grid gap-3 border-b border-slate-200 px-5 py-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Search" htmlFor="search">
            <Input
              id="search"
              placeholder="Title contains..."
              value={search}
              onChange={(event) => updateFilter(() => setSearch(event.target.value))}
            />
          </Field>

          <Field label="Status" htmlFor="status">
            <Select
              id="status"
              value={status}
              onChange={(event) => updateFilter(() => setStatus(event.target.value))}
            >
              <option value="">All statuses</option>
              {isStaff && <option value="Draft">Draft</option>}
              <option value="Published">Published</option>
              <option value="Closed">Closed</option>
            </Select>
          </Field>

          {!isStaff && (
            <Field label="Show" htmlFor="pending">
              <Select
                id="pending"
                value={onlyPending ? "pending" : "all"}
                onChange={(event) =>
                  updateFilter(() => setOnlyPending(event.target.value === "pending"))
                }
              >
                <option value="all">Everything</option>
                <option value="pending">Not submitted yet</option>
              </Select>
            </Field>
          )}
        </div>

        {isLoading && <Spinner label="Loading assignments" />}

        {error && (
          <div className="px-5 py-4">
            <Alert>{error}</Alert>
          </div>
        )}

        {data && !isLoading && data.items.length === 0 && (
          <EmptyState
            title="No assignments found"
            description={
              isStaff
                ? "Create one, or clear the filters above."
                : "Nothing has been set for your classes yet."
            }
          />
        )}

        {data && !isLoading && data.items.length > 0 && (
          <ul className="divide-y divide-slate-200">
            {data.items.map((assignment) => (
              <li key={assignment.id}>
                <AssignmentRow assignment={assignment} isStaff={isStaff} />
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
    </>
  );
}

function AssignmentRow({ assignment, isStaff }: { assignment: Assignment; isStaff: boolean }) {
  const overdue = isOverdue(assignment.deadline);

  return (
    <Link
      href={`/assignments/${assignment.id}`}
      className="block px-5 py-4 transition hover:bg-slate-50"
    >
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <p className="truncate text-sm font-semibold text-slate-900">{assignment.title}</p>
            <Badge tone={assignmentStatusTone[assignment.status]}>{assignment.status}</Badge>
            {assignment.mySubmission && (
              <Badge tone={submissionStatusTone[assignment.mySubmission.status]}>
                {submissionStatusLabel[assignment.mySubmission.status]}
              </Badge>
            )}
          </div>

          <p className="mt-1 text-sm text-slate-500">
            {assignment.courseName} &middot; {assignment.subjectName} &middot;{" "}
            {assignment.teacherName}
          </p>
        </div>

        <div className="text-right text-sm">
          <p className={overdue ? "font-medium text-amber-700" : "text-slate-700"}>
            Due {formatDateTime(assignment.deadline)}
          </p>
          <p className="text-xs text-slate-500">
            {formatRelative(assignment.deadline)} &middot; {assignment.maxMarks} marks
          </p>
          {isStaff && (
            <p className="mt-1 text-xs text-slate-500">
              {assignment.submissionCount} submitted &middot; {assignment.gradedCount} graded
            </p>
          )}
          {!isStaff && assignment.mySubmission?.marks != null && (
            <p className="mt-1 text-xs font-medium text-emerald-700">
              Scored {assignment.mySubmission.marks} / {assignment.maxMarks}
            </p>
          )}
        </div>
      </div>
    </Link>
  );
}
