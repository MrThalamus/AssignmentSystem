"use client";

import { useState } from "react";
import { PageHeader, RequireRole } from "@/components/app-shell";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  Field,
  Input,
  Modal,
  Spinner,
  Textarea,
} from "@/components/ui";
import { api } from "@/lib/api";
import { messageFor, useAsync } from "@/lib/use-async";

export default function SubjectsPage() {
  const [creating, setCreating] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const { data, error, isLoading, reload } = useAsync(() => api.subjects.list(), []);

  const remove = async (id: string, name: string) => {
    if (!window.confirm(`Delete "${name}"?`)) return;
    setActionError(null);

    try {
      await api.subjects.remove(id);
      reload();
    } catch (cause) {
      setActionError(messageFor(cause));
    }
  };

  return (
    <RequireRole roles={["Admin"]}>
      <PageHeader
        title="Subjects"
        description="The catalogue teachers pick from when a subject is added to a course."
        actions={<Button onClick={() => setCreating(true)}>Add subject</Button>}
      />

      {actionError && (
        <div className="mb-4">
          <Alert>{actionError}</Alert>
        </div>
      )}

      <Card>
        {isLoading && <Spinner label="Loading subjects" />}
        {error && (
          <div className="px-5 py-4">
            <Alert>{error}</Alert>
          </div>
        )}

        {data && data.length === 0 && (
          <EmptyState
            title="No subjects yet"
            description="Add a subject before setting up courses."
          />
        )}

        {data && data.length > 0 && (
          <ul className="divide-y divide-slate-200">
            {data.map((subject) => (
              <li
                key={subject.id}
                className="flex flex-wrap items-start justify-between gap-3 px-5 py-4"
              >
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-slate-900">
                    {subject.name}{" "}
                    <span className="font-normal text-slate-500">({subject.code})</span>
                  </p>
                  {subject.description && (
                    <p className="mt-0.5 text-sm text-slate-500">{subject.description}</p>
                  )}
                </div>
                <Button variant="ghost" onClick={() => remove(subject.id, subject.name)}>
                  Delete
                </Button>
              </li>
            ))}
          </ul>
        )}
      </Card>

      {creating && (
        <SubjectDialog
          onClose={() => setCreating(false)}
          onSaved={() => {
            setCreating(false);
            reload();
          }}
        />
      )}
    </RequireRole>
  );
}

function SubjectDialog({ onClose, onSaved }: { onClose: () => void; onSaved: () => void }) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [description, setDescription] = useState("");
  const [failure, setFailure] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const codeError =
    code.trim() === ""
      ? "Enter a code."
      : /^[A-Za-z0-9-]+$/.test(code.trim())
        ? undefined
        : "Use letters, digits and hyphens only.";

  const save = async () => {
    if (!name.trim() || codeError) return;

    setBusy(true);
    setFailure(null);

    try {
      await api.subjects.create({
        name: name.trim(),
        code: code.trim(),
        description: description.trim() || null,
      });
      onSaved();
    } catch (cause) {
      setFailure(messageFor(cause));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title="Add subject" onClose={onClose}>
      <div className="space-y-4">
        {failure && <Alert>{failure}</Alert>}

        <Field label="Name" htmlFor="name">
          <Input
            id="name"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Mathematics"
          />
        </Field>

        <Field label="Code" htmlFor="code" error={code ? codeError : undefined}>
          <Input
            id="code"
            value={code}
            onChange={(event) => setCode(event.target.value)}
            placeholder="MATH-101"
          />
        </Field>

        <Field label="Description (optional)" htmlFor="description">
          <Textarea
            id="description"
            rows={3}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
          />
        </Field>

        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} loading={busy} disabled={!name.trim() || Boolean(codeError)}>
            Add subject
          </Button>
        </div>
      </div>
    </Modal>
  );
}
