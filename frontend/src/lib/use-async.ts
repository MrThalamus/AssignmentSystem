"use client";

import { useCallback, useEffect, useState } from "react";
import { ApiError, subscribeToWarming } from "./api";

interface AsyncState<T> {
  data: T | null;
  error: string | null;
  isLoading: boolean;
  /** Re-runs the loader, e.g. after a mutation. */
  reload: () => void;
}

/**
 * Runs an async loader for a page and tracks its state.
 *
 * `deps` behaves like a `useEffect` dependency list and must hold JSON-serialisable
 * values (ids, filter strings, page numbers) - they are serialised into the key that
 * identifies one particular request.
 *
 * Loading is derived from that key rather than stored: whenever the key moves ahead
 * of the last settled result we are, by definition, loading. That keeps every state
 * update inside a promise callback, and makes a superseded response impossible to
 * apply because its key no longer matches.
 */
export function useAsync<T>(loader: () => Promise<T>, deps: unknown[]): AsyncState<T> {
  const [reloadToken, setReloadToken] = useState(0);
  const [settled, setSettled] = useState<{
    key: string;
    data: T | null;
    error: string | null;
  } | null>(null);

  const key = `${reloadToken}:${JSON.stringify(deps)}`;
  const isCurrent = settled?.key === key;

  useEffect(() => {
    let active = true;

    loader()
      .then((data) => {
        if (active) setSettled({ key, data, error: null });
      })
      .catch((cause: unknown) => {
        if (active) setSettled({ key, data: null, error: messageFor(cause) });
      });

    return () => {
      active = false;
    };
    // `loader` is a fresh closure on every render; the key is what actually decides
    // when the request should run again.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);

  const reload = useCallback(() => setReloadToken((token) => token + 1), []);

  return {
    data: isCurrent ? settled.data : null,
    error: isCurrent ? settled.error : null,
    isLoading: !isCurrent,
    reload,
  };
}

/**
 * True while some request has been outstanding long enough to look stuck, which on
 * the free tier usually means the host is still waking. Lets a screen say so instead
 * of showing a spinner that reads as a hang.
 */
export function useIsWarming() {
  const [isWarming, setIsWarming] = useState(false);

  useEffect(() => subscribeToWarming(setIsWarming), []);

  return isWarming;
}

export function messageFor(cause: unknown) {
  if (cause instanceof ApiError) return cause.message;
  if (cause instanceof Error) return cause.message;
  return "Something went wrong. Please try again.";
}
