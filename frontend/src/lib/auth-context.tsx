"use client";

import { useRouter } from "next/navigation";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { ApiError, api, session } from "./api";
import type { AuthenticatedUser, UserRole } from "./types";

interface AuthContextValue {
  user: AuthenticatedUser | null;
  /** True until the stored session has been checked, so guards do not flash. */
  isLoading: boolean;
  signIn: (email: string, password: string) => Promise<AuthenticatedUser>;
  signOut: () => void;
  hasRole: (...roles: UserRole[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [user, setUser] = useState<AuthenticatedUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const signOut = useCallback(() => {
    session.clear();
    setUser(null);
    router.replace("/login");
  }, [router]);

  useEffect(() => {
    let active = true;

    // Restore the session from storage and confirm it against the API: a stored
    // token can be expired, or belong to an account that has since been deactivated.
    // The result is applied in a callback so the effect never sets state
    // synchronously and trigger a cascading render.
    const restore = async () => {
      const stored = session.getUser();

      if (!session.getToken() || !stored) {
        return null;
      }

      try {
        return await api.auth.me();
      } catch (error) {
        if (error instanceof ApiError && (error.isUnauthenticated || error.status === 403)) {
          session.clear();
          return null;
        }

        // A network blip should not sign a working session out; trust what we stored.
        return stored;
      }
    };

    void restore().then((restored) => {
      if (!active) return;
      setUser(restored);
      setIsLoading(false);
    });

    return () => {
      active = false;
    };
  }, []);

  const signIn = useCallback(async (email: string, password: string) => {
    const response = await api.auth.login(email, password);
    session.save(response.accessToken, response.user);
    setUser(response.user);
    return response.user;
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isLoading,
      signIn,
      signOut,
      hasRole: (...roles: UserRole[]) => (user ? roles.includes(user.role) : false),
    }),
    [user, isLoading, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);

  if (!context) {
    throw new Error("useAuth must be used inside an AuthProvider.");
  }

  return context;
}

/** Where each role lands after signing in. */
export const homeRouteFor = (role: UserRole) => (role === "Admin" ? "/admin/users" : "/assignments");
