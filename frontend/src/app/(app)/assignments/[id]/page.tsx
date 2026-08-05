"use client";

import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { PageHeader } from "@/components/app-shell";
import { GradingPanel } from "@/components/grading-panel";
import { StudentSubmissionPanel } from "@/components/student-submission-panel";
import { Alert, Badge, Button, Card, CardHeader, Spinner } from "@/components/ui";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { assignmentStatusTone, formatDateTime, formatRelative, isOverdue } from "@/lib/format";
import type { Assignment } from "@/lib/types";
import { messageFor, useAsync } from "@/lib/use-async";

export default function AssignmentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { user } = useAuth();
  const isStaff = user?.role === "Admin" || user?.role === "Teacher";

  const { data: assignment, error, isLoading, reload } = useAsync(
    () => api.assignments.get(id),
    [id],
  );

  if (isLoading) return <Spinner label="Loading the assignment" />;
  if (error) return <Alert>{error}</Alert>;
  if (!assignment) return null;

  return (
    <>
      <PageHeader
        title={assignment.title}
        description={`${assignment.courseName} · ${assignment.subjectName} · set by ${assignment.teacherName}`}
        actions={isStaff ? <StaffActions assignment={assignment} onChanged={reload} /> : undefined}
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card>
            <CardHeader
              title="Details"
              actions={
                <Badge tone={assignmentStatusTone[assignment.status]}>{assignment.status}</Badge>
              }
            />
            <div className="px-5 py-4">
              <p className="whitespace-pre-wrap text-sm text-slate-700">{assignment.description}</p>
            </div>
          </Card>

          {isStaff ? (
            <GradingPanel assignment={assignment} />
          ) : (
            <StudentSubmissionPanel assignment={assignment} />
          )}
        </div>

        <aside className="space-y-6">
          <Card>
            <CardHeader title="At a glance" />
            <dl className="divide-y divide-slate-200 text-sm">
              <Row label="Deadline">
                <span className={isOverdue(assignment.deadline) ? "text-amber-700" : undefined}>
                  {formatDateTime(assignment.deadline)}
                </span>
                <span className="block text-xs text-slate-500">
                  {formatRelative(assignment.deadline)}
                </span>
              </Row>
              <Row label="Maximum marks">{assignment.maxMarks}</Row>
              <Row label="Late submissions">
                {assignment.allowLateSubmission ? "Accepted, flagged as late" : "Not accepted"}
              </Row>
              <Row label="Updates">
                {assignment.allowResubmission
                  ? "Students may revise before the deadline"
                  : "One attempt only"}
              </Row>
              {isStaff && (
                <>
                  <Row label="Submitted">{assignment.submissionCount}</Row>
                  <Row label="Graded">{assignment.gradedCount}</Row>
                  <Row label="Published">
                    {assignment.publishedAt ? formatDateTime(assignment.publishedAt) : "Not yet"}
                  </Row>
                </>
              )}
            </dl>
          </Card>
        </aside>
      </div>
    </>
  );
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 px-5 py-3">
      <dt className="text-slate-500">{label}</dt>
      <dd className="text-right font-medium text-slate-900">{children}</dd>
    </div>
  );
}

function StaffActions({
  assignment,
  onChanged,
}: {
  assignment: Assignment;
  onChanged: () => void;
}) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  const run = async (action: () => Promise<unknown>, afterDelete = false) => {
    setBusy(true);
    setFailure(null);

    try {
      await action();
      if (afterDelete) router.push("/assignments");
      else onChanged();
    } catch (cause) {
      setFailure(messageFor(cause));
    } finally {
      setBusy(false);
    }
  };

  const confirmDelete = () => {
    if (!window.confirm(`Delete "${assignment.title}"? This cannot be undone.`)) return;
    void run(() => api.assignments.remove(assignment.id), true);
  };

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <Link href={`/assignments/${assignment.id}/edit`}>
          <Button variant="secondary">Edit</Button>
        </Link>

        {assignment.status === "Draft" && (
          <Button loading={busy} onClick={() => run(() => api.assignments.publish(assignment.id))}>
            Publish
          </Button>
        )}

        {assignment.status === "Published" && (
          <>
            <Button
              variant="secondary"
              loading={busy}
              onClick={() => run(() => api.assignments.unpublish(assignment.id))}
            >
              Back to draft
            </Button>
            <Button
              variant="secondary"
              loading={busy}
              onClick={() => run(() => api.assignments.close(assignment.id))}
            >
              Close
            </Button>
          </>
        )}

        <Button variant="danger" disabled={busy} onClick={confirmDelete}>
          Delete
        </Button>
      </div>

      {failure && (
        <div className="max-w-md">
          <Alert>{failure}</Alert>
        </div>
      )}
    </div>
  );
}
