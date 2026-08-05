import type { ReactNode } from "react";
import { AppShell } from "@/components/app-shell";

/**
 * Wraps every signed-in route. The group folder keeps the shared chrome out of the
 * URL, so these pages stay at /assignments, /admin/users and so on.
 */
export default function AuthenticatedLayout({ children }: { children: ReactNode }) {
  return <AppShell>{children}</AppShell>;
}
