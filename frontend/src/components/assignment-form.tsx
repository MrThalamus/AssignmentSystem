"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Checkbox, Field, Input, Select, Textarea } from "@/components/ui";
import { fromDateTimeLocalValue, toDateTimeLocalValue } from "@/lib/format";
import type { Assignment, CourseSubject } from "@/lib/types";

/**
 * Mirrors the server-side validators. The API validates independently - this exists
 * so a teacher is told about a bad value before a round trip, not instead of it.
 */
const schema = z.object({
  courseSubjectId: z.string().min(1, "Choose the class and subject."),
  title: z.string().trim().min(1, "Give the assignment a title.").max(200),
  description: z.string().trim().min(1, "Describe what students have to do.").max(10_000),
  maxMarks: z.coerce
    .number({ message: "Enter the maximum marks." })
    .gt(0, "Maximum marks must be greater than zero.")
    .max(1000, "Maximum marks cannot exceed 1000."),
  deadline: z.string().min(1, "Set a deadline."),
  allowLateSubmission: z.boolean(),
  allowResubmission: z.boolean(),
});

export type AssignmentFormValues = z.input<typeof schema>;

interface AssignmentFormProps {
  courseSubjects: CourseSubject[];
  /** Supplied when editing; the class/subject is fixed once work has been set. */
  assignment?: Assignment;
  submitLabel: string;
  onSubmit: (values: {
    courseSubjectId: string;
    title: string;
    description: string;
    maxMarks: number;
    deadline: string;
    allowLateSubmission: boolean;
    allowResubmission: boolean;
    publishNow: boolean;
  }) => Promise<void>;
  onCancel: () => void;
}

export function AssignmentForm({
  courseSubjects,
  assignment,
  submitLabel,
  onSubmit,
  onCancel,
}: AssignmentFormProps) {
  const isEditing = Boolean(assignment);
  const [failure, setFailure] = useState<string | null>(null);
  const [publishNow, setPublishNow] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<AssignmentFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      courseSubjectId: assignment?.courseSubjectId ?? courseSubjects[0]?.id ?? "",
      title: assignment?.title ?? "",
      description: assignment?.description ?? "",
      maxMarks: assignment?.maxMarks ?? 20,
      deadline: assignment
        ? toDateTimeLocalValue(assignment.deadline)
        : toDateTimeLocalValue(defaultDeadline()),
      allowLateSubmission: assignment?.allowLateSubmission ?? false,
      allowResubmission: assignment?.allowResubmission ?? true,
    },
  });

  const submit = handleSubmit(async (values) => {
    setFailure(null);

    try {
      await onSubmit({
        courseSubjectId: values.courseSubjectId,
        title: values.title.trim(),
        description: values.description.trim(),
        maxMarks: Number(values.maxMarks),
        deadline: fromDateTimeLocalValue(values.deadline),
        allowLateSubmission: values.allowLateSubmission,
        allowResubmission: values.allowResubmission,
        publishNow,
      });
    } catch (cause) {
      setFailure(cause instanceof Error ? cause.message : "Could not save the assignment.");
    }
  });

  if (courseSubjects.length === 0) {
    return (
      <Alert tone="info" title="No classes assigned">
        You are not responsible for any class and subject yet. An administrator has to assign one
        before you can create an assignment.
      </Alert>
    );
  }

  return (
    <Card className="p-6">
      <form onSubmit={submit} noValidate className="space-y-5">
        {failure && <Alert>{failure}</Alert>}

        <Field
          label="Class and subject"
          htmlFor="courseSubjectId"
          error={errors.courseSubjectId?.message}
          hint={isEditing ? "This cannot be moved once the assignment exists." : undefined}
        >
          <Select id="courseSubjectId" disabled={isEditing} {...register("courseSubjectId")}>
            {courseSubjects.map((courseSubject) => (
              <option key={courseSubject.id} value={courseSubject.id}>
                {courseSubject.courseName} — {courseSubject.subjectName}
              </option>
            ))}
          </Select>
        </Field>

        <Field label="Title" htmlFor="title" error={errors.title?.message}>
          <Input id="title" placeholder="Quadratic equations worksheet" {...register("title")} />
        </Field>

        <Field
          label="Description"
          htmlFor="description"
          error={errors.description?.message}
          hint="Explain the task, and what students should hand in."
        >
          <Textarea id="description" rows={6} {...register("description")} />
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label="Maximum marks" htmlFor="maxMarks" error={errors.maxMarks?.message}>
            <Input id="maxMarks" type="number" min={1} max={1000} step="0.5" {...register("maxMarks")} />
          </Field>

          <Field
            label="Deadline"
            htmlFor="deadline"
            error={errors.deadline?.message}
            hint="Shown to students in their own time zone."
          >
            <Input id="deadline" type="datetime-local" {...register("deadline")} />
          </Field>
        </div>

        <div className="space-y-3 rounded-md bg-slate-50 px-4 py-3">
          <Checkbox
            label="Accept submissions after the deadline"
            description="Late work is still accepted, and clearly flagged as late."
            {...register("allowLateSubmission")}
          />
          <Checkbox
            label="Allow students to update their submission"
            description="Turn this off to give students a single attempt."
            {...register("allowResubmission")}
          />
        </div>

        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-slate-200 pt-4">
          <Button type="button" variant="ghost" onClick={onCancel}>
            Cancel
          </Button>

          {!isEditing && (
            <Button
              type="submit"
              variant="secondary"
              loading={isSubmitting && !publishNow}
              onClick={() => setPublishNow(false)}
            >
              Save as draft
            </Button>
          )}

          <Button
            type="submit"
            loading={isSubmitting && (publishNow || isEditing)}
            onClick={() => setPublishNow(!isEditing)}
          >
            {isEditing ? submitLabel : "Save and publish"}
          </Button>
        </div>
      </form>
    </Card>
  );
}

/** A week out, on the hour - a sensible default a teacher can adjust. */
function defaultDeadline() {
  const deadline = new Date();
  deadline.setDate(deadline.getDate() + 7);
  deadline.setHours(23, 59, 0, 0);
  return deadline.toISOString();
}
