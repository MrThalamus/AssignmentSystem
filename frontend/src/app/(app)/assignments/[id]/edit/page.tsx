"use client";

import { useParams, useRouter } from "next/navigation";
import { PageHeader, RequireRole } from "@/components/app-shell";
import { AssignmentForm } from "@/components/assignment-form";
import { Alert, Spinner } from "@/components/ui";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/use-async";

export default function EditAssignmentPage() {
  const router = useRouter();
  const { id } = useParams<{ id: string }>();

  const { data, error, isLoading } = useAsync(
    // Independent of each other, so fetch them side by side rather than in sequence.
    async () => {
      const [assignment, courseSubjects] = await Promise.all([
        api.assignments.get(id),
        api.courses.teachableSubjects(),
      ]);

      return { assignment, courseSubjects };
    },
    [id],
  );

  return (
    <RequireRole roles={["Admin", "Teacher"]}>
      <PageHeader title="Edit assignment" description="Changes are visible to students at once." />

      {isLoading && <Spinner label="Loading the assignment" />}
      {error && <Alert>{error}</Alert>}

      {data && (
        <div className="max-w-3xl">
          <AssignmentForm
            assignment={data.assignment}
            courseSubjects={data.courseSubjects}
            submitLabel="Save changes"
            onCancel={() => router.push(`/assignments/${id}`)}
            onSubmit={async (values) => {
              await api.assignments.update(id, values);
              router.push(`/assignments/${id}`);
            }}
          />
        </div>
      )}
    </RequireRole>
  );
}
