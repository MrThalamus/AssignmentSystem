"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
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
  Select,
  Spinner,
} from "@/components/ui";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { formatDate } from "@/lib/format";
import type { User, UserRole } from "@/lib/types";
import { messageFor, useAsync } from "@/lib/use-async";

const PAGE_SIZE = 15;

const createSchema = z.object({
  fullName: z.string().trim().min(1, "Enter the person's full name.").max(150),
  email: z.string().trim().min(1, "Enter an email address.").email("Enter a valid email address."),
  password: z
    .string()
    .min(8, "The password must be at least 8 characters long.")
    .regex(/[A-Za-z]/, "The password must contain at least one letter.")
    .regex(/[0-9]/, "The password must contain at least one digit."),
  role: z.enum(["Admin", "Teacher", "Student"]),
});

type CreateValues = z.infer<typeof createSchema>;

export default function UsersPage() {
  const { user: signedInUser } = useAuth();

  const [page, setPage] = useState(1);
  const [role, setRole] = useState("");
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const { data, error, isLoading, reload } = useAsync(
    () =>
      api.users.list({
        page,
        pageSize: PAGE_SIZE,
        role: (role || undefined) as UserRole | undefined,
        search: search || undefined,
      }),
    [page, role, search],
  );

  const toggleActive = async (target: User) => {
    setActionError(null);

    try {
      if (target.isActive) {
        await api.users.deactivate(target.id);
      } else {
        await api.users.update(target.id, {
          fullName: target.fullName,
          email: target.email,
          isActive: true,
        });
      }
      reload();
    } catch (cause) {
      setActionError(messageFor(cause));
    }
  };

  return (
    <RequireRole roles={["Admin"]}>
      <PageHeader
        title="Users"
        description="Accounts for administrators, teachers and students."
        actions={<Button onClick={() => setCreating(true)}>Add user</Button>}
      />

      {actionError && (
        <div className="mb-4">
          <Alert>{actionError}</Alert>
        </div>
      )}

      <Card>
        <div className="grid gap-3 border-b border-slate-200 px-5 py-4 sm:grid-cols-2 lg:grid-cols-3">
          <Field label="Search" htmlFor="search">
            <Input
              id="search"
              placeholder="Name or email"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value);
                setPage(1);
              }}
            />
          </Field>
          <Field label="Role" htmlFor="role">
            <Select
              id="role"
              value={role}
              onChange={(event) => {
                setRole(event.target.value);
                setPage(1);
              }}
            >
              <option value="">All roles</option>
              <option value="Admin">Administrator</option>
              <option value="Teacher">Teacher</option>
              <option value="Student">Student</option>
            </Select>
          </Field>
        </div>

        {isLoading && <Spinner label="Loading users" />}
        {error && (
          <div className="px-5 py-4">
            <Alert>{error}</Alert>
          </div>
        )}

        {data && !isLoading && data.items.length === 0 && <EmptyState title="No users found" />}

        {data && data.items.length > 0 && (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-slate-200 text-sm">
              <thead className="bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
                <tr>
                  <th className="px-5 py-3 font-medium">Name</th>
                  <th className="px-5 py-3 font-medium">Email</th>
                  <th className="px-5 py-3 font-medium">Role</th>
                  <th className="px-5 py-3 font-medium">Status</th>
                  <th className="px-5 py-3 font-medium">Created</th>
                  <th className="px-5 py-3" />
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200">
                {data.items.map((row) => (
                  <tr key={row.id}>
                    <td className="px-5 py-3 font-medium text-slate-900">{row.fullName}</td>
                    <td className="px-5 py-3 text-slate-600">{row.email}</td>
                    <td className="px-5 py-3">
                      <Badge tone={row.role === "Admin" ? "info" : "neutral"}>{row.role}</Badge>
                    </td>
                    <td className="px-5 py-3">
                      <Badge tone={row.isActive ? "success" : "neutral"}>
                        {row.isActive ? "Active" : "Deactivated"}
                      </Badge>
                    </td>
                    <td className="px-5 py-3 text-slate-500">{formatDate(row.createdAt)}</td>
                    <td className="px-5 py-3 text-right">
                      <Button
                        variant="ghost"
                        onClick={() => toggleActive(row)}
                        disabled={row.id === signedInUser?.id}
                        title={
                          row.id === signedInUser?.id
                            ? "You cannot deactivate your own account."
                            : undefined
                        }
                      >
                        {row.isActive ? "Deactivate" : "Reactivate"}
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
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
        <CreateUserDialog
          onClose={() => setCreating(false)}
          onCreated={() => {
            setCreating(false);
            reload();
          }}
        />
      )}
    </RequireRole>
  );
}

function CreateUserDialog({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: () => void;
}) {
  const [failure, setFailure] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<CreateValues>({
    resolver: zodResolver(createSchema),
    defaultValues: { fullName: "", email: "", password: "", role: "Student" },
  });

  const submit = handleSubmit(async (values) => {
    setFailure(null);

    try {
      await api.users.create(values);
      onCreated();
    } catch (cause) {
      setFailure(messageFor(cause));
    }
  });

  return (
    <Modal open title="Add user" onClose={onClose}>
      <form onSubmit={submit} noValidate className="space-y-4">
        {failure && <Alert>{failure}</Alert>}

        <Field label="Full name" htmlFor="fullName" error={errors.fullName?.message}>
          <Input id="fullName" {...register("fullName")} />
        </Field>

        <Field label="Email address" htmlFor="email" error={errors.email?.message}>
          <Input id="email" type="email" {...register("email")} />
        </Field>

        <Field
          label="Temporary password"
          htmlFor="password"
          error={errors.password?.message}
          hint="At least 8 characters, with a letter and a digit. The user can change it later."
        >
          <Input id="password" type="text" {...register("password")} />
        </Field>

        <Field
          label="Role"
          htmlFor="newRole"
          error={errors.role?.message}
          hint="A role cannot be changed later, because enrollments and teaching assignments depend on it."
        >
          <Select id="newRole" {...register("role")}>
            <option value="Student">Student</option>
            <option value="Teacher">Teacher</option>
            <option value="Admin">Administrator</option>
          </Select>
        </Field>

        <div className="flex justify-end gap-2">
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button type="submit" loading={isSubmitting}>
            Create user
          </Button>
        </div>
      </form>
    </Modal>
  );
}
