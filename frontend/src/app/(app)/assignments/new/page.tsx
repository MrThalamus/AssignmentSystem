"use client";

import { useRouter } from "next/navigation";
import { PageHeader, RequireRole } from "@/components/app-shell";
import { AssignmentForm } from "@/components/assignment-form";
import { Alert, Spinner } from "@/components/ui";
import { api } from "@/lib/api";
import { useAsync } from "@/lib/use-async";

export default function NewAssignmentPage() {
  const router = useRouter();
  const { data: courseSubjects, error, isLoading } = useAsync(
    () => api.courses.teachableSubjects(),
    [],
  );

  return (
    <RequireRole roles={["Admin", "Teacher"]}>
      <PageHeader
        title="New assignment"
        description="Save it as a draft while you work on it, or publish it straight to the class."
      />

      {isLoading && <Spinner label="Loading your classes" />}
      {error && <Alert>{error}</Alert>}

      {courseSubjects && (
        <div className="max-w-3xl">
          <AssignmentForm
            courseSubjects={courseSubjects}
            submitLabel="Create"
            onCancel={() => router.push("/assignments")}
            onSubmit={async (values) => {
              const created = await api.assignments.create(values);
              router.push(`/assignments/${created.id}`);
            }}
          />
        </div>
      )}
    </RequireRole>
  );
}
