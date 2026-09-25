import type { Problem } from '@/shared/api/problems';

export function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName.replace(/[\\/:*?"<>|]+/g, '-');
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}

export function downloadJson(value: unknown, fileName: string) {
  downloadBlob(new Blob([JSON.stringify(value, null, 2)], { type: 'application/json' }), fileName);
}

export type ReadResult = { ok: true; value: unknown } | { ok: false; problem: Problem };

/** Reads a chosen file as JSON, with a readable problem when it is not JSON at all. */
export async function readJsonFile(file: File): Promise<ReadResult> {
  if (file.size > 2 * 1024 * 1024) {
    return { ok: false, problem: { type: 'about:blank', title: 'That file is too large.', status: 413, fix: 'A Board Document can be at most 2 MB.' } };
  }

  try {
    return { ok: true, value: JSON.parse(await file.text()) };
  } catch (error) {
    return {
      ok: false,
      problem: {
        type: 'about:blank',
        title: 'Not valid JSON',
        status: 400,
        detail: `“${file.name}” is not valid JSON${error instanceof Error ? ` (${error.message})` : ''}.`,
        fix: 'Choose a Board Document exported from a board, or one that follows the Board Document schema.',
      },
    };
  }
}
