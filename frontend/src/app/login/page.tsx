"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Input } from "@/components/ui";
import { homeRouteFor, useAuth } from "@/lib/auth-context";
import { messageFor } from "@/lib/use-async";

const loginSchema = z.object({
  email: z.string().min(1, "Enter your email address.").email("Enter a valid email address."),
  password: z.string().min(1, "Enter your password."),
});

type LoginValues = z.infer<typeof loginSchema>;

const demoAccounts = [
  { role: "Administrator", email: "admin@school.edu", password: "Admin@123" },
  { role: "Teacher", email: "nazmul.hasan@school.edu", password: "Teacher@123" },
  { role: "Student", email: "rafi.ahmed@school.edu", password: "Student@123" },
];

export default function LoginPage() {
  const { signIn, user, isLoading } = useAuth();
  const router = useRouter();
  const [failure, setFailure] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: "", password: "" },
  });

  // Somebody who is already signed in has no business on the login screen.
  useEffect(() => {
    if (!isLoading && user) {
      router.replace(homeRouteFor(user.role));
    }
  }, [user, isLoading, router]);

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null);

    try {
      const signedIn = await signIn(values.email, values.password);
      router.replace(homeRouteFor(signedIn.role));
    } catch (cause) {
      setFailure(messageFor(cause));
    }
  });

  const fillDemoAccount = (email: string, password: string) => {
    setValue("email", email, { shouldValidate: true });
    setValue("password", password, { shouldValidate: true });
  };

  return (
    <div className="flex min-h-screen items-center justify-center px-4 py-10">
      <div className="w-full max-w-md space-y-5">
        <div className="text-center">
          <h1 className="text-xl font-semibold text-slate-900">
            Assignment &amp; Submission System
          </h1>
          <p className="mt-1 text-sm text-slate-500">Sign in to continue.</p>
        </div>

        <Card className="p-6">
          <form onSubmit={onSubmit} noValidate className="space-y-4">
            {failure && <Alert>{failure}</Alert>}

            <Field label="Email address" htmlFor="email" error={errors.email?.message}>
              <Input
                id="email"
                type="email"
                autoComplete="username"
                placeholder="you@school.edu"
                {...register("email")}
              />
            </Field>

            <Field label="Password" htmlFor="password" error={errors.password?.message}>
              <Input
                id="password"
                type="password"
                autoComplete="current-password"
                {...register("password")}
              />
            </Field>

            <Button type="submit" loading={isSubmitting} className="w-full">
              Sign in
            </Button>
          </form>
        </Card>

        <Card className="p-5">
          <p className="text-sm font-medium text-slate-800">Demo accounts</p>
          <p className="mt-0.5 text-xs text-slate-500">
            Created by the seed data. Select one to fill the form.
          </p>
          <ul className="mt-3 space-y-2">
            {demoAccounts.map((account) => (
              <li key={account.email}>
                <button
                  type="button"
                  onClick={() => fillDemoAccount(account.email, account.password)}
                  className="w-full rounded-md border border-slate-200 px-3 py-2 text-left transition hover:border-slate-300 hover:bg-slate-50"
                >
                  <span className="block text-sm font-medium text-slate-800">{account.role}</span>
                  <span className="block text-xs text-slate-500">
                    {account.email} &middot; {account.password}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        </Card>
      </div>
    </div>
  );
}
