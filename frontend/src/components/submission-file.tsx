"use client";

import { useEffect, useMemo, useState } from "react";
import { Alert, Button, Spinner } from "@/components/ui";
import { api } from "@/lib/api";
import { formatFileSize } from "@/lib/format";
import type { Submission } from "@/lib/types";
import { messageFor, useAsync } from "@/lib/use-async";

/**
 * Loads a submitted PDF and hands the browser an object URL for it.
 *
 * The file cannot simply be linked to: the API authenticates with a bearer token held
 * in local storage, and neither `<a href>` nor `<iframe src>` sends one. So the bytes
 * are fetched with the token attached and turned into a blob URL, which the viewer can
 * point at instead.
 */
function useSubmissionFile(submissionId: string) {
  const { data: blob, error } = useAsync(() => api.submissions.file(submissionId), [submissionId]);

  // Derived from the blob rather than stored, so no state is set while an effect runs.
  const url = useMemo(() => (blob ? URL.createObjectURL(blob) : null), [blob]);

  useEffect(() => {
    // A blob URL keeps the file alive in memory until it is revoked, so the PDF would
    // outlive the panel that displayed it otherwise.
    return () => {
      if (url) URL.revokeObjectURL(url);
    };
  }, [url]);

  return { url, error };
}

/** Downloads the PDF under its original name. */
export function DownloadSubmissionButton({
  submission,
  variant = "secondary",
}: {
  submission: Submission;
  variant?: "primary" | "secondary" | "ghost";
}) {
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  const download = async () => {
    setBusy(true);
    setFailure(null);

    try {
      const blob = await api.submissions.file(submission.id);
      const url = URL.createObjectURL(blob);

      // A synthetic click is the only way to name a downloaded blob; the anchor never
      // enters the document, so there is nothing to clean up but the URL itself.
      const link = document.createElement("a");
      link.href = url;
      link.download = submission.fileName;
      link.click();

      URL.revokeObjectURL(url);
    } catch (cause) {
      setFailure(messageFor(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <Button variant={variant} onClick={download} loading={busy}>
        Download
      </Button>
      {failure && <Alert>{failure}</Alert>}
    </>
  );
}

/** An inline preview of the PDF, for reading it without leaving the page. */
export function SubmissionFileViewer({
  submission,
  className = "h-[32rem]",
}: {
  submission: Submission;
  className?: string;
}) {
  const { url, error } = useSubmissionFile(submission.id);

  if (error) return <Alert>{error}</Alert>;
  if (!url) return <Spinner label="Loading the PDF" />;

  return (
    <object
      data={url}
      type="application/pdf"
      aria-label={`${submission.fileName}, submitted by ${submission.studentName}`}
      className={`w-full rounded-md border border-slate-200 ${className}`}
    >
      {/* Shown only where the browser has no built-in PDF viewer. */}
      <div className="px-4 py-6 text-center text-sm text-slate-600">
        <p>This browser cannot display PDFs inline.</p>
        <div className="mt-3 flex justify-center">
          <DownloadSubmissionButton submission={submission} />
        </div>
      </div>
    </object>
  );
}

/** The file's name and size, for places that list a submission without showing it. */
export function SubmissionFileSummary({ submission }: { submission: Submission }) {
  return (
    <span className="inline-flex items-center gap-2 text-sm text-slate-700">
      <svg
        aria-hidden="true"
        viewBox="0 0 16 16"
        className="h-4 w-4 shrink-0 fill-rose-600"
      >
        <path d="M4 1h5l3 3v11H4V1Zm5 1.5V4.5h2L9 2.5Z" />
      </svg>
      <span className="truncate font-medium">{submission.fileName}</span>
      <span className="text-slate-500">{formatFileSize(submission.fileSizeBytes)}</span>
    </span>
  );
}
