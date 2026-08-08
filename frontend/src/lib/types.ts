/**
 * Mirrors of the API contracts. Hand-written rather than generated so the shape the
 * UI relies on is visible in one place; the backend serialises enums as their names,
 * which is why these are string unions rather than numbers.
 */

export type UserRole = "Admin" | "Teacher" | "Student";

export type AssignmentStatus = "Draft" | "Published" | "Closed";

export type SubmissionStatus = "Submitted" | "Late" | "Graded" | "Returned";

export interface AuthenticatedUser {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: AuthenticatedUser;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface User {
  id: string;
  fullName: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  createdAt: string;
}

export interface Subject {
  id: string;
  name: string;
  code: string;
  description: string | null;
}

export interface Course {
  id: string;
  name: string;
  code: string;
  academicYear: string;
  isActive: boolean;
  enrolledStudentCount: number;
  subjectCount: number;
}

export interface CourseSubject {
  id: string;
  courseId: string;
  courseName: string;
  courseCode: string;
  subjectId: string;
  subjectName: string;
  subjectCode: string;
  teacherId: string | null;
  teacherName: string | null;
}

export interface Enrollment {
  id: string;
  courseId: string;
  studentId: string;
  studentName: string;
  studentEmail: string;
  enrolledAt: string;
}

export interface StudentSubmissionSummary {
  id: string;
  status: SubmissionStatus;
  isLate: boolean;
  submittedAt: string;
  marks: number | null;
}

export interface Assignment {
  id: string;
  title: string;
  description: string;
  maxMarks: number;
  deadline: string;
  status: AssignmentStatus;
  allowLateSubmission: boolean;
  allowResubmission: boolean;
  createdAt: string;
  publishedAt: string | null;
  courseSubjectId: string;
  courseId: string;
  courseName: string;
  courseCode: string;
  subjectId: string;
  subjectName: string;
  teacherId: string;
  teacherName: string;
  submissionCount: number;
  gradedCount: number;
  /** Populated for students only: their own submission, if they have one. */
  mySubmission: StudentSubmissionSummary | null;
}

export interface Submission {
  id: string;
  assignmentId: string;
  assignmentTitle: string;
  assignmentMaxMarks: number;
  assignmentDeadline: string;
  studentId: string;
  studentName: string;
  studentEmail: string;
  /** The name of the uploaded PDF. Its bytes are fetched separately, on demand. */
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  status: SubmissionStatus;
  isLate: boolean;
  attemptCount: number;
  submittedAt: string;
  updatedAt: string | null;
  marks: number | null;
  feedback: string | null;
  gradedAt: string | null;
  gradedByTeacherName: string | null;
}
