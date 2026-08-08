"use client";

import { useState } from "react";
import {
  DownloadSubmissionButton,
  SubmissionFileSummary,
  SubmissionFileViewer,
} from "@/components/submission-file";
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  EmptyState,
  Field,
  Input,
  Modal,
  Select,
  Spinner,
  Textarea,
} from "@/components/ui";
import { api } from "@/lib/api";
import { formatDateTime, submissionStatusLabel, submissionStatusTone } from "@/lib/format";
import type { Assignment, Submission, SubmissionStatus } from "@/lib/types";
import { messageFor, useAsync } from "@/lib/use-async";

/** The teacher's view of everything handed in for one assignment. */
export function GradingPanel({ assignment }: { assignment: Assignment }) {
  const [status, setStatus] = useState("");
  const [grading, setGrading] = useState<Submission | null>(null);

  const { data, error, isLoading, reload } = useAsync(
    () => api.assignments.submissions(assignment.id, { status: status || undefined, pageSize: 100 }),
    [assignment.id, status],
  );

  return (
    <Card>
      <CardHeader
        title="Submissions"
        description={
          data
            ? `${data.totalCount} submitted · ${
                data.items.filter((s) => s.status === "Graded").length
              } graded`
            : undefined
        }
        actions={
          <Select
            aria-label="Filter by status"
            value={status}
            onChange={(event) => setStatus(event.target.value)}
            className="w-auto"
          >
            <option value="">All statuses</option>
            <option value="Submitted">Submitted</option>
            <option value="Late">Late</option>
            <option value="Graded">Graded</option>
            <option value="Returned">Returned</option>
          </Select>
        }
      />

      {isLoading && <Spinner label="Loading submissions" />}
      {error && (
        <div className="px-5 py-4">
          <Alert>{error}</Alert>
        </div>
      )}

      {data && !isLoading && data.items.length === 0 && (
        <EmptyState
          title="Nothing handed in yet"
          description="Submissions will appear here as students send their work."
        />
      )}

      {data && data.items.length > 0 && (
        <ul className="divide-y divide-slate-200">
          {data.items.map((submission) => (
            <li key={submission.id} className="px-5 py-4">
              <SubmissionRow
                submission={submission}
                maxMarks={assignment.maxMarks}
                onGrade={() => setGrading(submission)}
                onChanged={reload}
              />
            </li>
          ))}
        </ul>
      )}

      {grading && (
        <GradeDialog
          submission={grading}
          maxMarks={assignment.maxMarks}
          onClose={() => setGrading(null)}
          onGraded={() => {
            setGrading(null);
            reload();
          }}
        />
      )}
    </Card>
  );
}

function SubmissionRow({
  submission,
  maxMarks,
  onGrade,
  onChanged,
}: {
  submission: Submission;
  maxMarks: number;
  onGrade: () => void;
  onChanged: () => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  const changeStatus = async (status: SubmissionStatus) => {
    setBusy(true);
    setFailure(null);

    try {
      await api.submissions.changeStatus(submission.id, status);
      onChanged();
    } catch (cause) {
      setFailure(messageFor(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-sm font-semibold text-slate-900">{submission.studentName}</p>
            <Badge tone={submissionStatusTone[submission.status]}>
              {submissionStatusLabel[submission.status]}
            </Badge>
            {submission.attemptCount > 1 && (
              <Badge>Attempt {submission.attemptCount}</Badge>
            )}
          </div>
          <p className="mt-0.5 text-xs text-slate-500">
            {submission.studentEmail} &middot; handed in {formatDateTime(submission.submittedAt)}
          </p>
          <div className="mt-1.5">
            <SubmissionFileSummary submission={submission} />
          </div>
        </div>

        <div className="flex items-center gap-2">
          {submission.marks !== null && (
            <span className="text-sm font-semibold text-slate-900">
              {submission.marks} / {maxMarks}
            </span>
          )}
          <Button variant="ghost" onClick={() => setExpanded((open) => !open)}>
            {expanded ? "Hide" : "Read"}
          </Button>
          <DownloadSubmissionButton submission={submission} variant="ghost" />
          <Button variant="secondary" onClick={onGrade} disabled={busy}>
            {submission.status === "Graded" ? "Re-grade" : "Grade"}
          </Button>
          {submission.status !== "Returned" && (
            <Button
              variant="ghost"
              loading={busy}
              onClick={() => changeStatus("Returned")}
              title="Send back so the student can revise it"
            >
              Return
            </Button>
          )}
        </div>
      </div>

      {failure && <Alert>{failure}</Alert>}

      {expanded && (
        <div className="rounded-md bg-slate-50 px-4 py-3">
          {/* Mounted only once opened, so a page of submissions does not fetch every
              PDF the teacher has not asked to read. */}
          <SubmissionFileViewer submission={submission} />
          {submission.feedback && (
            <p className="mt-3 border-t border-slate-200 pt-3 text-sm text-slate-600">
              <span className="font-medium text-slate-800">Your feedback: </span>
              {submission.feedback}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function GradeDialog({
  submission,
  maxMarks,
  onClose,
  onGraded,
}: {
  submission: Submission;
  maxMarks: number;
  onClose: () => void;
  onGraded: () => void;
}) {
  const [marks, setMarks] = useState(submission.marks?.toString() ?? "");
  const [feedback, setFeedback] = useState(submission.feedback ?? "");
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const numericMarks = Number(marks);
  const marksError =
    marks === ""
      ? "Enter the marks awarded."
      : Number.isNaN(numericMarks)
        ? "Marks must be a number."
        : numericMarks < 0
          ? "Marks cannot be negative."
          : numericMarks > maxMarks
            ? `Marks cannot exceed the maximum of ${maxMarks}.`
            : undefined;

  const save = async () => {
    if (marksError) return;

    setBusy(true);
    setFailure(null);

    try {
      await api.submissions.grade(submission.id, {
        marks: numericMarks,
        feedback: feedback.trim() || null,
      });
      onGraded();
    } catch (cause) {
      setFailure(messageFor(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Grade ${submission.studentName}'s submission`} onClose={onClose}>
      <div className="space-y-4">
        {failure && <Alert>{failure}</Alert>}

        {/* The work being marked, alongside the marks, so the teacher does not have to
            close the dialog to check something. */}
        <div className="rounded-md bg-slate-50 p-3">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
            <SubmissionFileSummary submission={submission} />
            <DownloadSubmissionButton submission={submission} variant="ghost" />
          </div>
          <SubmissionFileViewer submission={submission} className="h-64" />
        </div>

        <Field label={`Marks (out of ${maxMarks})`} htmlFor="marks" error={marksError}>
          <Input
            id="marks"
            type="number"
            min={0}
            max={maxMarks}
            step="0.5"
            value={marks}
            onChange={(event) => setMarks(event.target.value)}
          />
        </Field>

        <Field label="Feedback (optional)" htmlFor="feedback">
          <Textarea
            id="feedback"
            rows={5}
            value={feedback}
            onChange={(event) => setFeedback(event.target.value)}
            placeholder="What went well, and what to work on next time."
          />
        </Field>

        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} loading={busy} disabled={Boolean(marksError)}>
            Save marks
          </Button>
        </div>
      </div>
    </Modal>
  );
}
