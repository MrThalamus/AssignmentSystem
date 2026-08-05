"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Badge, Button, Card, CardHeader, Field, Input, Textarea } from "@/components/ui";
import { api } from "@/lib/api";
import {
  formatDateTime,
  isOverdue,
  submissionStatusLabel,
  submissionStatusTone,
} from "@/lib/format";
import type { Assignment, Submission } from "@/lib/types";
import { messageFor, useAsync } from "@/lib/use-async";

const schema = z.object({
  content: z.string().trim().min(1, "Write your answer before submitting.").max(20_000),
  attachmentUrl: z
    .string()
    .trim()
    .max(2000)
    .refine((value) => value === "" || /^https?:\/\//i.test(value), {
      message: "Enter a full link starting with http:// or https://",
    }),
});

type Values = z.infer<typeof schema>;

/**
 * A student's own view of one assignment: their answer, its status, and the marks
 * and feedback once a teacher has looked at it.
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
  const [failure, setFailure] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      content: submission?.content ?? "",
      attachmentUrl: submission?.attachmentUrl ?? "",
    },
  });

  const isGraded = submission?.status === "Graded";
  const wasReturned = submission?.status === "Returned";
  const overdue = isOverdue(assignment.deadline);

  // What the server will actually allow, restated here so the form can explain
  // itself rather than only failing on submit.
  const windowIsOpen =
    assignment.status === "Published" && (!overdue || assignment.allowLateSubmission);
  const canEdit = !isGraded && (wasReturned || (windowIsOpen && (!submission || assignment.allowResubmission)));

  const submit = handleSubmit(async (values) => {
    setFailure(null);
    setSaved(false);

    const attachmentUrl = values.attachmentUrl.trim() || null;

    try {
      if (submission) {
        await api.submissions.update(submission.id, { content: values.content, attachmentUrl });
      } else {
        await api.submissions.submit({
          assignmentId: assignment.id,
          content: values.content,
          attachmentUrl,
        });
      }

      setSaved(true);
      onSaved();
    } catch (cause) {
      setFailure(messageFor(cause));
    }
  });

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

        {wasReturned && (
          <Alert tone="info" title="Returned for revision">
            Your teacher has asked you to submit this again. You can still edit it even though the
            deadline has passed.
          </Alert>
        )}

        {isGraded && (
          <Alert tone="info">
            This submission has been graded and can no longer be edited.
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

        {canEdit ? (
          <form onSubmit={submit} noValidate className="space-y-4">
            {failure && <Alert>{failure}</Alert>}
            {saved && <Alert tone="success">Your submission has been saved.</Alert>}

            {overdue && assignment.allowLateSubmission && !submission && (
              <Alert tone="info">
                The deadline has passed. Your submission will be accepted but recorded as late.
              </Alert>
            )}

            <Field label="Your answer" htmlFor="content" error={errors.content?.message}>
              <Textarea id="content" rows={10} {...register("content")} />
            </Field>

            <Field
              label="Attachment link (optional)"
              htmlFor="attachmentUrl"
              error={errors.attachmentUrl?.message}
              hint="Paste a link to a document or repository. File uploads are not supported."
            >
              <Input
                id="attachmentUrl"
                type="url"
                placeholder="https://drive.example.com/my-work"
                {...register("attachmentUrl")}
              />
            </Field>

            <div className="flex justify-end">
              <Button type="submit" loading={isSubmitting}>
                {submission ? "Update submission" : "Submit"}
              </Button>
            </div>
          </form>
        ) : (
          submission && (
            <div>
              <p className="whitespace-pre-wrap text-sm text-slate-700">{submission.content}</p>
              {submission.attachmentUrl && (
                <a
                  href={submission.attachmentUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="mt-3 inline-block text-sm font-medium text-slate-900 underline"
                >
                  Open attachment
                </a>
              )}
            </div>
          )
        )}
      </div>
    </Card>
  );
}
