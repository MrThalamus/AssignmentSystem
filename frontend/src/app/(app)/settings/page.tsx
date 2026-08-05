"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { PageHeader } from "@/components/app-shell";
import { Alert, Button, Card, CardHeader, Field, Input } from "@/components/ui";
import { api } from "@/lib/api";
import { useAuth } from "@/lib/auth-context";
import { messageFor } from "@/lib/use-async";

const schema = z
  .object({
    currentPassword: z.string().min(1, "Enter your current password."),
    newPassword: z
      .string()
      .min(8, "The password must be at least 8 characters long.")
      .regex(/[A-Za-z]/, "The password must contain at least one letter.")
      .regex(/[0-9]/, "The password must contain at least one digit."),
    confirmPassword: z.string().min(1, "Type the new password again."),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    path: ["confirmPassword"],
    message: "The two passwords do not match.",
  })
  .refine((values) => values.newPassword !== values.currentPassword, {
    path: ["newPassword"],
    message: "The new password must be different from the current one.",
  });

type Values = z.infer<typeof schema>;

export default function SettingsPage() {
  const { user } = useAuth();
  const [failure, setFailure] = useState<string | null>(null);
  const [succeeded, setSucceeded] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: "", newPassword: "", confirmPassword: "" },
  });

  const submit = handleSubmit(async (values) => {
    setFailure(null);
    setSucceeded(false);

    try {
      await api.auth.changePassword(values.currentPassword, values.newPassword);
      setSucceeded(true);
      reset();
    } catch (cause) {
      setFailure(messageFor(cause));
    }
  });

  return (
    <>
      <PageHeader title="Settings" description="Your account details and password." />

      <div className="grid max-w-4xl gap-6 md:grid-cols-2">
        <Card>
          <CardHeader title="Account" />
          <dl className="divide-y divide-slate-200 text-sm">
            <div className="flex justify-between gap-4 px-5 py-3">
              <dt className="text-slate-500">Name</dt>
              <dd className="font-medium text-slate-900">{user?.fullName}</dd>
            </div>
            <div className="flex justify-between gap-4 px-5 py-3">
              <dt className="text-slate-500">Email</dt>
              <dd className="font-medium text-slate-900">{user?.email}</dd>
            </div>
            <div className="flex justify-between gap-4 px-5 py-3">
              <dt className="text-slate-500">Role</dt>
              <dd className="font-medium text-slate-900">{user?.role}</dd>
            </div>
          </dl>
        </Card>

        <Card>
          <CardHeader title="Change password" />
          <form onSubmit={submit} noValidate className="space-y-4 px-5 py-4">
            {failure && <Alert>{failure}</Alert>}
            {succeeded && <Alert tone="success">Your password has been changed.</Alert>}

            <Field
              label="Current password"
              htmlFor="currentPassword"
              error={errors.currentPassword?.message}
            >
              <Input
                id="currentPassword"
                type="password"
                autoComplete="current-password"
                {...register("currentPassword")}
              />
            </Field>

            <Field label="New password" htmlFor="newPassword" error={errors.newPassword?.message}>
              <Input
                id="newPassword"
                type="password"
                autoComplete="new-password"
                {...register("newPassword")}
              />
            </Field>

            <Field
              label="Confirm new password"
              htmlFor="confirmPassword"
              error={errors.confirmPassword?.message}
            >
              <Input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                {...register("confirmPassword")}
              />
            </Field>

            <div className="flex justify-end">
              <Button type="submit" loading={isSubmitting}>
                Change password
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </>
  );
}
