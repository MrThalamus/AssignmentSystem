"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Card, Field, Input, PasswordInput } from "@/components/ui";
import { api } from "@/lib/api";
import { homeRouteFor, useAuth } from "@/lib/auth-context";
import { messageFor, useIsWarming } from "@/lib/use-async";

const loginSchema = z.object({
  email: z.string().min(1, "Enter your email address.").email("Enter a valid email address."),
  password: z.string().min(1, "Enter your password."),
});

type LoginValues = z.infer<typeof loginSchema>;

export default function LoginPage() {
  const { signIn, user, isLoading } = useAuth();
  const router = useRouter();
  const [failure, setFailure] = useState<string | null>(null);
  const isWarming = useIsWarming();

  // Start the host waking as soon as the screen loads, so the sign-in request itself
  // does not have to sit through a cold start. It is deliberately fire-and-forget: a
  // failure here means the real request will report the problem properly, and there
  // is nothing useful to say about a warm-up.
  useEffect(() => {
    void api.health.wake().catch(() => {});
  }, []);

  const {
    register,
    handleSubmit,
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

            {isWarming && !failure && (
              <Alert tone="info" title="Waking the demo server">
                It runs on a free tier that stops it after a quiet period. This first
                request can take up to a minute; everything is immediate once it is
                running.
              </Alert>
            )}

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
              <PasswordInput
                id="password"
                autoComplete="current-password"
                {...register("password")}
              />
            </Field>

            <Button type="submit" loading={isSubmitting} className="w-full">
              Sign in
            </Button>
          </form>
        </Card>
      </div>
    </div>
  );
}
