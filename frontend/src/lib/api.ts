import type {
  Assignment,
  AuthResponse,
  AuthenticatedUser,
  Course,
  CourseSubject,
  Enrollment,
  PagedResult,
  Submission,
  Subject,
  SubmissionStatus,
  User,
  UserRole,
} from "./types";

// Trimmed before use: this value is typically pasted into a hosting dashboard, and
// a stray tab or newline rides along more often than not. Browsers happen to strip
// whitespace while parsing a URL, so the damage is invisible until something else
// concatenates the value and fails for no apparent reason.
const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL?.trim().replace(/\/+$/, "") || "http://localhost:5080";

const TOKEN_STORAGE_KEY = "assignment-system.token";
const USER_STORAGE_KEY = "assignment-system.user";

/**
 * An error carrying whatever the API told us. `fieldErrors` comes from the
 * ValidationProblemDetails body so forms can show messages against the right input
 * instead of dumping one generic string at the top.
 */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    message: string,
    readonly fieldErrors?: Record<string, string[]>,
  ) {
    super(message);
    this.name = "ApiError";
  }

  get isUnauthenticated() {
    return this.status === 401;
  }
}

// ------------------------------------------------------------------- session

export const session = {
  getToken: () =>
    typeof window === "undefined" ? null : window.localStorage.getItem(TOKEN_STORAGE_KEY),

  getUser: (): AuthenticatedUser | null => {
    if (typeof window === "undefined") return null;
    const raw = window.localStorage.getItem(USER_STORAGE_KEY);
    if (!raw) return null;
    try {
      return JSON.parse(raw) as AuthenticatedUser;
    } catch {
      // A corrupt entry should log the user out rather than crash every page.
      return null;
    }
  },

  save: (token: string, user: AuthenticatedUser) => {
    window.localStorage.setItem(TOKEN_STORAGE_KEY, token);
    window.localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
  },

  clear: () => {
    window.localStorage.removeItem(TOKEN_STORAGE_KEY);
    window.localStorage.removeItem(USER_STORAGE_KEY);
  },
};

// --------------------------------------------------------------- the request

type QueryValue = string | number | boolean | undefined | null;

function buildUrl(path: string, query?: Record<string, QueryValue>) {
  const url = new URL(`${API_BASE_URL}${path}`);

  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== null && value !== "") {
      url.searchParams.set(key, String(value));
    }
  }

  return url.toString();
}

async function request<T>(
  path: string,
  options: RequestInit & { query?: Record<string, QueryValue> } = {},
): Promise<T> {
  const { query, ...init } = options;
  const token = session.getToken();

  const response = await fetch(buildUrl(path, query), {
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init.headers,
    },
    cache: "no-store",
  });

  if (response.status === 204) {
    return undefined as T;
  }

  const body = await response.text();
  const parsed: unknown = body ? safeJsonParse(body) : null;

  if (!response.ok) {
    throw toApiError(response.status, parsed);
  }

  return parsed as T;
}

function safeJsonParse(body: string): unknown {
  try {
    return JSON.parse(body);
  } catch {
    return { title: body };
  }
}

/** Turns an RFC 7807 problem-details body into something a component can render. */
function toApiError(status: number, body: unknown): ApiError {
  const problem = (body ?? {}) as {
    title?: string;
    detail?: string;
    errors?: Record<string, string[]>;
  };

  const fieldErrors = problem.errors;

  const message =
    problem.detail ??
    (fieldErrors ? Object.values(fieldErrors).flat()[0] : undefined) ??
    problem.title ??
    defaultMessageFor(status);

  return new ApiError(status, message, fieldErrors);
}

function defaultMessageFor(status: number) {
  switch (status) {
    case 401:
      return "Your session has expired. Please sign in again.";
    case 403:
      return "You do not have permission to do that.";
    case 404:
      return "That item could not be found.";
    default:
      return "Something went wrong. Please try again.";
  }
}

const get = <T,>(path: string, query?: Record<string, QueryValue>) =>
  request<T>(path, { method: "GET", query });

const post = <T,>(path: string, body?: unknown) =>
  request<T>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) });

const put = <T,>(path: string, body?: unknown) =>
  request<T>(path, { method: "PUT", body: body === undefined ? undefined : JSON.stringify(body) });

const del = <T,>(path: string) => request<T>(path, { method: "DELETE" });

// ------------------------------------------------------------------ endpoints

export const api = {
  auth: {
    login: (email: string, password: string) =>
      post<AuthResponse>("/api/auth/login", { email, password }),
    me: () => get<AuthenticatedUser>("/api/auth/me"),
    changePassword: (currentPassword: string, newPassword: string) =>
      post<void>("/api/auth/change-password", { currentPassword, newPassword }),
  },

  users: {
    list: (query: {
      role?: UserRole;
      isActive?: boolean;
      search?: string;
      page?: number;
      pageSize?: number;
    }) => get<PagedResult<User>>("/api/users", query),
    create: (body: {
      fullName: string;
      email: string;
      password: string;
      role: UserRole;
    }) => post<User>("/api/users", body),
    update: (id: string, body: { fullName: string; email: string; isActive: boolean }) =>
      put<User>(`/api/users/${id}`, body),
    resetPassword: (id: string, newPassword: string) =>
      post<void>(`/api/users/${id}/reset-password`, { newPassword }),
    deactivate: (id: string) => del<void>(`/api/users/${id}`),
  },

  subjects: {
    list: () => get<Subject[]>("/api/subjects"),
    create: (body: { name: string; code: string; description?: string | null }) =>
      post<Subject>("/api/subjects", body),
    update: (id: string, body: { name: string; code: string; description?: string | null }) =>
      put<Subject>(`/api/subjects/${id}`, body),
    remove: (id: string) => del<void>(`/api/subjects/${id}`),
  },

  courses: {
    list: (query: { isActive?: boolean; search?: string; page?: number; pageSize?: number }) =>
      get<PagedResult<Course>>("/api/courses", query),
    get: (id: string) => get<Course>(`/api/courses/${id}`),
    create: (body: { name: string; code: string; academicYear: string; isActive: boolean }) =>
      post<Course>("/api/courses", body),
    update: (
      id: string,
      body: { name: string; code: string; academicYear: string; isActive: boolean },
    ) => put<Course>(`/api/courses/${id}`, body),
    remove: (id: string) => del<void>(`/api/courses/${id}`),

    listSubjects: (courseId: string) =>
      get<CourseSubject[]>(`/api/courses/${courseId}/subjects`),
    addSubject: (courseId: string, body: { subjectId: string; teacherId?: string | null }) =>
      post<CourseSubject>(`/api/courses/${courseId}/subjects`, body),
    assignTeacher: (courseSubjectId: string, teacherId: string | null) =>
      put<CourseSubject>(`/api/courses/subjects/${courseSubjectId}/teacher`, { teacherId }),
    removeSubject: (courseSubjectId: string) =>
      del<void>(`/api/courses/subjects/${courseSubjectId}`),

    teachableSubjects: () => get<CourseSubject[]>("/api/courses/teachable-subjects"),

    listStudents: (courseId: string) => get<Enrollment[]>(`/api/courses/${courseId}/students`),
    enrollStudents: (courseId: string, studentIds: string[]) =>
      post<Enrollment[]>(`/api/courses/${courseId}/students`, { studentIds }),
    removeStudent: (courseId: string, studentId: string) =>
      del<void>(`/api/courses/${courseId}/students/${studentId}`),
  },

  assignments: {
    list: (query: {
      courseId?: string;
      subjectId?: string;
      status?: string;
      search?: string;
      onlyPending?: boolean;
      page?: number;
      pageSize?: number;
    }) => get<PagedResult<Assignment>>("/api/assignments", query),
    get: (id: string) => get<Assignment>(`/api/assignments/${id}`),
    create: (body: {
      courseSubjectId: string;
      title: string;
      description: string;
      maxMarks: number;
      deadline: string;
      allowLateSubmission: boolean;
      allowResubmission: boolean;
      publishNow: boolean;
    }) => post<Assignment>("/api/assignments", body),
    update: (
      id: string,
      body: {
        title: string;
        description: string;
        maxMarks: number;
        deadline: string;
        allowLateSubmission: boolean;
        allowResubmission: boolean;
      },
    ) => put<Assignment>(`/api/assignments/${id}`, body),
    publish: (id: string) => post<Assignment>(`/api/assignments/${id}/publish`),
    unpublish: (id: string) => post<Assignment>(`/api/assignments/${id}/unpublish`),
    close: (id: string) => post<Assignment>(`/api/assignments/${id}/close`),
    remove: (id: string) => del<void>(`/api/assignments/${id}`),
    submissions: (id: string, query: { status?: string; page?: number; pageSize?: number } = {}) =>
      get<PagedResult<Submission>>(`/api/assignments/${id}/submissions`, query),
  },

  submissions: {
    list: (query: {
      assignmentId?: string;
      courseId?: string;
      studentId?: string;
      status?: string;
      page?: number;
      pageSize?: number;
    }) => get<PagedResult<Submission>>("/api/submissions", query),
    get: (id: string) => get<Submission>(`/api/submissions/${id}`),
    submit: (body: { assignmentId: string; content: string; attachmentUrl?: string | null }) =>
      post<Submission>("/api/submissions", body),
    update: (id: string, body: { content: string; attachmentUrl?: string | null }) =>
      put<Submission>(`/api/submissions/${id}`, body),
    grade: (id: string, body: { marks: number; feedback?: string | null }) =>
      post<Submission>(`/api/submissions/${id}/grade`, body),
    changeStatus: (id: string, status: SubmissionStatus) =>
      put<Submission>(`/api/submissions/${id}/status`, { status }),
  },
};
