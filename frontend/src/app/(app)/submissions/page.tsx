"use client";

import Link from "next/link";
import { useState } from "react";
import { PageHeader, RequireRole } from "@/components/app-shell";
import {
  Alert,
  Badge,
  Card,
  EmptyState,
  Field,
  Pagination,
  Select,
  Spinner,
} from "@/components/ui";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { formatDateTime, submissionStatusLabel, submissionStatusTone } from "@/lib/format";
import { useAsync } from "@/lib/use-async";

const PAGE_SIZE = 15;

/**
 * A flat list across every assignment: a student's own work, or - for a teacher -
 * everything handed in for the classes they are responsible for.
 */
export default function SubmissionsPage() {
  const { user } = useAuth();
  const isTeacher = user?.role === "Teacher";

  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");

  const { data, error, isLoading } = useAsync(
    () => api.submissions.list({ page, pageSize: PAGE_SIZE, status: status || undefined }),
    [page, status],
  );

  return (
    <RequireRole roles={["Teacher", "Student"]}>
      <PageHeader
        title="Submissions"
        description={
          isTeacher
            ? "Everything handed in for your classes, newest first."
            : "Everything you have handed in, newest first."
        }
      />

      <Card>
        <div className="border-b border-slate-200 px-5 py-4 sm:max-w-xs">
          <Field label="Status" htmlFor="status">
            <Select
              id="status"
              value={status}
              onChange={(event) => {
                setStatus(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All statuses</option>
              <option value="Submitted">Submitted</option>
              <option value="Late">Submitted late</option>
              <option value="Graded">Graded</option>
              <option value="Returned">Returned for revision</option>
            </Select>
          </Field>
        </div>

        {isLoading && <Spinner label="Loading submissions" />}

        {error && (
          <div className="px-5 py-4">
            <Alert>{error}</Alert>
          </div>
        )}

        {data && !isLoading && data.items.length === 0 && (
          <EmptyState
            title="Nothing here yet"
            description={
              isTeacher
                ? "Submissions appear once your students start handing work in."
                : "Open an assignment to submit your first answer."
            }
          />
        )}

        {data && data.items.length > 0 && (
          <ul className="divide-y divide-slate-200">
            {data.items.map((submission) => (
              <li key={submission.id}>
                <Link
                  href={`/assignments/${submission.assignmentId}`}
                  className="block px-5 py-4 transition hover:bg-slate-50"
                >
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="truncate text-sm font-semibold text-slate-900">
                          {submission.assignmentTitle}
                        </p>
                        <Badge tone={submissionStatusTone[submission.status]}>
                          {submissionStatusLabel[submission.status]}
                        </Badge>
                      </div>
                      <p className="mt-0.5 text-sm text-slate-500">
                        {isTeacher ? submission.studentName : "Your submission"} &middot; handed in{" "}
                        {formatDateTime(submission.submittedAt)}
                      </p>
                    </div>

                    <div className="text-right text-sm">
                      {submission.marks !== null ? (
                        <p className="font-semibold text-slate-900">
                          {submission.marks} / {submission.assignmentMaxMarks}
                        </p>
                      ) : (
                        <p className="text-slate-500">Not graded</p>
                      )}
                      <p className="text-xs text-slate-500">
                        Due {formatDateTime(submission.assignmentDeadline)}
                      </p>
                    </div>
                  </div>
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
    </RequireRole>
  );
}
