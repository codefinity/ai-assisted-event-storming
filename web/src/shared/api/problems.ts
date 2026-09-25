import type { FetchBaseQueryError } from '@reduxjs/toolkit/query';
import type { SerializedError } from '@reduxjs/toolkit';

/** One problem with one field, as the API reports it (RFC 9457 extension member `errors`). */
export interface FieldProblem {
  pointer?: string;
  field?: string;
  code: string;
  detail: string;
  fix?: string;
}

/** An RFC 9457 Problem Details body with the API's extension members. */
export interface Problem {
  type: string;
  title: string;
  status: number;
  detail?: string;
  code?: string;
  fix?: string;
  errors?: FieldProblem[];
  traceId?: string;
}

/** A failure as the board hub reports it. */
export interface HubFailure {
  kind: string;
  code: string;
  message: string;
  field?: string | null;
  fix?: string | null;
}

export function isProblem(value: unknown): value is Problem {
  return typeof value === 'object' && value !== null && 'title' in value && 'status' in value;
}

const offline: Problem = {
  type: 'about:blank',
  title: 'The server could not be reached.',
  status: 0,
  fix: 'Check your connection and try again.',
};

/** Whatever RTK Query or fetch rejected with, as a Problem the UI can show. */
export function toProblem(error: FetchBaseQueryError | SerializedError | undefined | unknown): Problem | undefined {
  if (error === undefined || error === null) {
    return undefined;
  }

  if (typeof error === 'object' && 'status' in error) {
    const { status } = error as FetchBaseQueryError;
    const data = (error as { data?: unknown }).data;
    if (isProblem(data)) {
      return data;
    }

    if (status === 'FETCH_ERROR' || status === 'TIMEOUT_ERROR') {
      return offline;
    }

    return {
      type: 'about:blank',
      title: 'Something went wrong.',
      status: typeof status === 'number' ? status : 500,
      detail: typeof (error as { error?: unknown }).error === 'string' ? (error as unknown as { error: string }).error : undefined,
    };
  }

  if (typeof error === 'object' && 'message' in error) {
    return { type: 'about:blank', title: String((error as SerializedError).message ?? 'Something went wrong.'), status: 500 };
  }

  return { type: 'about:blank', title: 'Something went wrong.', status: 500 };
}

/** The first problem reported for a field (a camelCase path such as `email` or `elements[2].type`). */
export function fieldProblem(problem: Problem | undefined, field: string): FieldProblem | undefined {
  return problem?.errors?.find((error) => error.field === field);
}

/** The problems that belong to none of the given fields, so a form can show them above itself. */
export function otherProblems(problem: Problem | undefined, fields: readonly string[]): FieldProblem[] {
  return problem?.errors?.filter((error) => !error.field || !fields.includes(error.field)) ?? [];
}
