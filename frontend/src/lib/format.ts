import type { AssignmentStatus, SubmissionStatus } from "./types";

/**
 * Dates are rendered on the client only. Formatting them during SSR would use the
 * server's locale and time zone and then be replaced on hydration, which React
 * reports as a mismatch.
 */
const dateTimeFormat = new Intl.DateTimeFormat(undefined, {
  dateStyle: "medium",
  timeStyle: "short",
});

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" });

export const formatDateTime = (iso: string) => dateTimeFormat.format(new Date(iso));

export const formatDate = (iso: string) => dateFormat.format(new Date(iso));

/** "in 3 days" / "2 hours ago", for deadlines where the exact minute matters less. */
export function formatRelative(iso: string, now: Date = new Date()) {
  const target = new Date(iso);
  const diffMs = target.getTime() - now.getTime();
  const relative = new Intl.RelativeTimeFormat(undefined, { numeric: "auto" });

  const units: [Intl.RelativeTimeFormatUnit, number][] = [
    ["day", 1000 * 60 * 60 * 24],
    ["hour", 1000 * 60 * 60],
    ["minute", 1000 * 60],
  ];

  for (const [unit, ms] of units) {
    const value = diffMs / ms;
    if (Math.abs(value) >= 1) {
      return relative.format(Math.round(value), unit);
    }
  }

  return "now";
}

export const isOverdue = (deadlineIso: string, now: Date = new Date()) =>
  new Date(deadlineIso).getTime() < now.getTime();

/** Converts an ISO instant into the value a `datetime-local` input expects. */
export function toDateTimeLocalValue(iso: string) {
  const date = new Date(iso);
  const offsetMs = date.getTimezoneOffset() * 60 * 1000;
  return new Date(date.getTime() - offsetMs).toISOString().slice(0, 16);
}

/** The inverse: a local wall-clock value back to a UTC instant for the API. */
export const fromDateTimeLocalValue = (value: string) => new Date(value).toISOString();

/** "412 KB" / "2.4 MB" - enough for a student to sanity-check what they attached. */
export function formatFileSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`;

  const kilobytes = bytes / 1024;
  if (kilobytes < 1024) return `${Math.round(kilobytes)} KB`;

  return `${(kilobytes / 1024).toFixed(1)} MB`;
}

// ------------------------------------------------------------------- badges

type BadgeTone = "neutral" | "info" | "success" | "warning" | "danger";

export const assignmentStatusTone: Record<AssignmentStatus, BadgeTone> = {
  Draft: "neutral",
  Published: "success",
  Closed: "warning",
};

export const submissionStatusTone: Record<SubmissionStatus, BadgeTone> = {
  Submitted: "info",
  Late: "warning",
  Graded: "success",
  Returned: "danger",
};

export const submissionStatusLabel: Record<SubmissionStatus, string> = {
  Submitted: "Submitted",
  Late: "Submitted late",
  Graded: "Graded",
  Returned: "Returned for revision",
};
