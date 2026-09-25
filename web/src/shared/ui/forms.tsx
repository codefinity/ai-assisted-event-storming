'use client';

import { useId, type InputHTMLAttributes, type ReactNode, type SelectHTMLAttributes } from 'react';
import { fieldProblem, otherProblems, type Problem } from '../api/problems';

interface FieldShellProps {
  label: string;
  hint?: ReactNode;
  problem?: Problem;
  /** The API's camelCase field path this input answers for. */
  name: string;
  children: (props: { id: string; describedBy: string | undefined; invalid: boolean }) => ReactNode;
}

function FieldShell({ label, hint, problem, name, children }: FieldShellProps) {
  const id = useId();
  const error = fieldProblem(problem, name);
  const hintId = hint ? `${id}-hint` : undefined;
  const errorId = error ? `${id}-error` : undefined;
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined;

  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children({ id, describedBy, invalid: error !== undefined })}
      {hint && (
        <span id={hintId} className="hint">
          {hint}
        </span>
      )}
      {error && (
        <span id={errorId} className="error-text" role="alert">
          {error.detail}
          {error.fix ? ` ${error.fix}` : ''}
        </span>
      )}
    </div>
  );
}

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'name'> {
  label: string;
  name: string;
  hint?: ReactNode;
  problem?: Problem;
}

export function TextField({ label, name, hint, problem, className, ...input }: TextFieldProps) {
  return (
    <FieldShell label={label} hint={hint} problem={problem} name={name}>
      {({ id, describedBy, invalid }) => (
        <input
          id={id}
          name={name}
          className={`input ${className ?? ''}`}
          aria-describedby={describedBy}
          aria-invalid={invalid || undefined}
          {...input}
        />
      )}
    </FieldShell>
  );
}

export interface SelectFieldProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'name'> {
  label: string;
  name: string;
  hint?: ReactNode;
  problem?: Problem;
  options: ReadonlyArray<{ value: string; label: string }>;
}

export function SelectField({ label, name, hint, problem, options, className, ...select }: SelectFieldProps) {
  return (
    <FieldShell label={label} hint={hint} problem={problem} name={name}>
      {({ id, describedBy, invalid }) => (
        <select id={id} name={name} className={`select ${className ?? ''}`} aria-describedby={describedBy} aria-invalid={invalid || undefined} {...select}>
          {options.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      )}
    </FieldShell>
  );
}

/**
 * A problem's headline, its fix, and any field errors the form has no input for. Fields that have
 * an input show their own errors next to it.
 */
export function FormProblem({ problem, fields = [] }: { problem?: Problem; fields?: readonly string[] }) {
  if (!problem) return null;
  const others = otherProblems(problem, fields);
  const onlyFieldErrors = (problem.errors?.length ?? 0) > 0 && others.length === 0;
  if (onlyFieldErrors) return null;

  return (
    <div role="alert" className="form-problem">
      <strong>{problem.detail ?? problem.title}</strong>
      {problem.fix && <span> {problem.fix}</span>}
      {others.length > 0 && (
        <ul>
          {others.map((error, index) => (
            <li key={`${error.pointer ?? error.field}-${index}`}>
              {error.field && <code>{error.field}</code>} {error.detail}
              {error.fix && <span className="muted"> {error.fix}</span>}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
