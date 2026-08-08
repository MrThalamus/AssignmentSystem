"use client";

import { useRef, useState } from "react";
import {
  DownloadSubmissionButton,
  SubmissionFileSummary,
  SubmissionFileViewer,
} from "@/components/submission-file";
import { Alert, Badge, Button, Card, CardHeader, Field } from "@/components/ui";
import { api } from "@/lib/api";
import {
  formatDateTime,
  formatFileSize,
  isOverdue,
  submissionStatusLabel,
  submissionStatusTone,
} from "@/lib/format";
import type { Assignment, Submission } from "@/lib/types";
import { messageFor, useAsync } from "@/lib/use-async";

/**
 * Mirrors the server's rule so the file can be rejected before it is uploaded rather
 * than after a student has waited for ten megabytes to travel.
 */
const MAX_FILE_BYTES = 10 * 1024 * 1024;

/**
 * A student's own view of one assignment: the PDF they handed in, its status, and the
 * marks and feedback once a teacher has looked at it.
 */
export function StudentSubmissionPanel({ assignment }: { assignment: Assignment }) {
  const { data, error, isLoading, reload } = useAsync(
    async () => {
      const page = await api.submissions.list({ assignmentId: assignment.id, pageSize: 1 });
      return page.items[0] ?? null;
    },
    [assignment.id],
  );

  if (isLoading) return null;
  if (error) return <Alert>{error}</Alert>;

  return <SubmissionEditor assignment={assignment} submission={data} onSaved={reload} />;
}

function SubmissionEditor({
  assignment,
  submission,
  onSaved,
}: {
  assignment: Assignment;
  submission: Submission | null;
  onSaved: () => void;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState<string | null>(null);
  const [failure, setFailure] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [busy, setBusy] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const isGraded = submission?.status === "Graded";
  const wasReturned = submission?.status === "Returned";
  const overdue = isOverdue(assignment.deadline);

  // What the server will actually allow, restated here so the form can explain
  // itself rather than only failing on submit.
  const windowIsOpen =
    assignment.status === "Published" && (!overdue || assignment.allowLateSubmission);
  const canEdit = !isGraded && (wasReturned || (windowIsOpen && (!submission || assignment.allowResubmission)));

  const chooseFile = (chosen: File | null) => {
    setSaved(false);
    setFailure(null);

    if (!chosen) {
      setFile(null);
      setFileError(null);
      return;
    }

    // Checked by name and by size only: the browser cannot see inside the file, so
    // whether it is really a PDF is settled by the server.
    if (!chosen.name.toLowerCase().endsWith(".pdf")) {
      setFile(null);
      setFileError("Only PDF files can be submitted.");
      return;
    }

    if (chosen.size > MAX_FILE_BYTES) {
      setFile(null);
      setFileError(`That file is ${formatFileSize(chosen.size)}. The limit is 10 MB.`);
      return;
    }

    setFile(chosen);
    setFileError(null);
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();

    if (!file) {
      setFileError(
        submission ? "Choose the PDF you want to replace it with." : "Choose the PDF to hand in.",
      );
      return;
    }

    setBusy(true);
    setFailure(null);
    setSaved(false);

    try {
      if (submission) {
        await api.submissions.update(submission.id, file);
      } else {
        await api.submissions.submit(assignment.id, file);
      }

      // The stored file is now the source of truth, so the staged one is cleared
      // rather than left looking like an unsaved change.
      setFile(null);
      if (inputRef.current) inputRef.current.value = "";

      setSaved(true);
      onSaved();
    } catch (cause) {
      setFailure(messageFor(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader
        title="Your submission"
        description={
          submission
            ? `Handed in ${formatDateTime(submission.submittedAt)}${
                submission.attemptCount > 1 ? ` · attempt ${submission.attemptCount}` : ""
              }`
            : "You have not submitted anything for this assignment yet."
        }
        actions={
          submission ? (
            <Badge tone={submissionStatusTone[submission.status]}>
              {submissionStatusLabel[submission.status]}
            </Badge>
          ) : undefined
        }
      />

      <div className="space-y-4 px-5 py-4">
        {submission && (submission.marks !== null || submission.feedback) && (
          <div className="rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3">
            {submission.marks !== null && (
              <p className="text-sm font-semibold text-emerald-900">
                {submission.marks} / {assignment.maxMarks} marks
              </p>
            )}
            {submission.feedback && (
              <p className="mt-1 whitespace-pre-wrap text-sm text-emerald-900">
                {submission.feedback}
              </p>
            )}
            {submission.gradedByTeacherName && submission.gradedAt && (
              <p className="mt-2 text-xs text-emerald-800">
                Marked by {submission.gradedByTeacherName} on {formatDateTime(submission.gradedAt)}
              </p>
            )}
          </div>
        )}

        {submission && (
          <div className="rounded-md border border-slate-200 bg-slate-50 px-4 py-3">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <SubmissionFileSummary submission={submission} />
              <DownloadSubmissionButton submission={submission} variant="ghost" />
            </div>
            <SubmissionFileViewer submission={submission} className="mt-3 h-96" />
          </div>
        )}

        {wasReturned && (
          <Alert tone="info" title="Returned for revision">
            Your teacher has asked you to submit this again. You can still upload a new PDF even
            though the deadline has passed.
          </Alert>
        )}

        {isGraded && (
          <Alert tone="info">
            This submission has been graded and can no longer be replaced.
          </Alert>
        )}

        {!canEdit && !isGraded && !windowIsOpen && (
          <Alert tone="info">
            {assignment.status === "Closed"
              ? "This assignment is closed and is no longer accepting submissions."
              : "The deadline has passed and this assignment does not accept late submissions."}
          </Alert>
        )}

        {!canEdit && !isGraded && windowIsOpen && submission && !assignment.allowResubmission && (
          <Alert tone="info">
            This assignment allows a single attempt, so your submission cannot be changed.
          </Alert>
        )}

        {canEdit && (
          <form onSubmit={submit} noValidate className="space-y-4">
            {failure && <Alert>{failure}</Alert>}
            {saved && <Alert tone="success">Your submission has been saved.</Alert>}

            {overdue && assignment.allowLateSubmission && !submission && (
              <Alert tone="info">
                The deadline has passed. Your submission will be accepted but recorded as late.
              </Alert>
            )}

            <Field
              label={submission ? "Replace with a new PDF" : "Your work, as a PDF"}
              htmlFor="file"
              error={fileError ?? undefined}
              hint="PDF only, up to 10 MB. Export your document as a PDF before uploading."
            >
              <input
                id="file"
                ref={inputRef}
                type="file"
                accept="application/pdf,.pdf"
                onChange={(event) => chooseFile(event.target.files?.[0] ?? null)}
                className="block w-full cursor-pointer rounded-md border border-slate-300 text-sm text-slate-700 file:mr-3 file:cursor-pointer file:rounded-l-md file:border-0 file:bg-slate-100 file:px-4 file:py-2 file:text-sm file:font-medium file:text-slate-800 hover:file:bg-slate-200"
              />
            </Field>

            {file && (
              <p className="text-sm text-slate-600">
                Ready to upload: <span className="font-medium text-slate-900">{file.name}</span> (
                {formatFileSize(file.size)})
              </p>
            )}

            <div className="flex justify-end">
              <Button type="submit" loading={busy} disabled={!file}>
                {submission ? "Replace submission" : "Submit"}
              </Button>
            </div>
          </form>
        )}
      </div>
    </Card>
  );
}
