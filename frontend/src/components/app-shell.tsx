"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useAuth } from "@/lib/auth-context";
import type { UserRole } from "@/lib/types";
import { Button, Spinner, cx } from "./ui";

interface NavItem {
  href: string;
  label: string;
  roles: UserRole[];
}

const navigation: NavItem[] = [
  { href: "/assignments", label: "Assignments", roles: ["Admin", "Teacher", "Student"] },
  { href: "/submissions", label: "Submissions", roles: ["Teacher", "Student"] },
  { href: "/admin/users", label: "Users", roles: ["Admin"] },
  { href: "/admin/courses", label: "Courses", roles: ["Admin"] },
  { href: "/admin/teaching", label: "Teaching", roles: ["Admin"] },
  { href: "/admin/subjects", label: "Subjects", roles: ["Admin"] },
  { href: "/settings", label: "Settings", roles: ["Admin", "Teacher", "Student"] },
];

const roleLabels: Record<UserRole, string> = {
  Admin: "Administrator",
  Teacher: "Teacher",
  Student: "Student",
};

/**
 * The signed-in chrome. The role check here only decides what to render - every
 * endpoint behind these links enforces the same rules server-side, so a user who
 * navigates straight to a URL still gets a 403 rather than data.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const { user, isLoading, signOut } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (!isLoading && !user) {
      router.replace("/login");
    }
  }, [isLoading, user, router]);

  if (isLoading || !user) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Spinner label="Checking your session" />
      </div>
    );
  }

  const visibleNavigation = navigation.filter((item) => item.roles.includes(user.role));

  return (
    <div className="min-h-screen bg-slate-50">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6">
          <Link href="/assignments" className="text-sm font-semibold text-slate-900">
            Assignment &amp; Submission System
          </Link>

          <div className="flex items-center gap-3">
            <div className="text-right">
              <p className="text-sm font-medium text-slate-900">{user.fullName}</p>
              <p className="text-xs text-slate-500">{roleLabels[user.role]}</p>
            </div>
            <Button variant="secondary" onClick={signOut}>
              Sign out
            </Button>
          </div>
        </div>

        <nav className="mx-auto max-w-7xl px-4 sm:px-6">
          <ul className="-mb-px flex gap-1 overflow-x-auto">
            {visibleNavigation.map((item) => {
              const active = pathname === item.href || pathname.startsWith(`${item.href}/`);

              return (
                <li key={item.href}>
                  <Link
                    href={item.href}
                    aria-current={active ? "page" : undefined}
                    className={cx(
                      "block whitespace-nowrap border-b-2 px-3 py-2.5 text-sm font-medium transition",
                      active
                        ? "border-slate-900 text-slate-900"
                        : "border-transparent text-slate-500 hover:border-slate-300 hover:text-slate-800",
                    )}
                  >
                    {item.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
      </header>

      <main className="mx-auto max-w-7xl px-4 py-6 sm:px-6 sm:py-8">{children}</main>
    </div>
  );
}

/**
 * Hides a page from roles it was not built for. The backend is still the authority;
 * this exists so a mistyped URL shows a clear message instead of a wall of 403s.
 */
export function RequireRole({ roles, children }: { roles: UserRole[]; children: ReactNode }) {
  const { user } = useAuth();

  if (user && !roles.includes(user.role)) {
    return (
      <div className="rounded-lg border border-slate-200 bg-white px-5 py-12 text-center">
        <p className="text-sm font-medium text-slate-800">This page is not available to you.</p>
        <p className="mt-1 text-sm text-slate-500">
          Your account is signed in as {roleLabels[user.role].toLowerCase()}.
        </p>
      </div>
    );
  }

  return <>{children}</>;
}

export function PageHeader({
  title,
  description,
  actions,
}: {
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="text-xl font-semibold text-slate-900">{title}</h1>
        {description && <p className="mt-1 text-sm text-slate-500">{description}</p>}
      </div>
      {actions && <div className="flex items-center gap-2">{actions}</div>}
    </div>
  );
}
