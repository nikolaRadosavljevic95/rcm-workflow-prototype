import { HttpErrorResponse } from '@angular/common/http';

// The API sends rule failures as ProblemDetails. Show its detail, or the fallback when there is none.
export function problemMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const body: unknown = error.error;
    if (typeof body === 'object' && body !== null && 'detail' in body && typeof body.detail === 'string') {
      return body.detail;
    }
  }
  return fallback;
}

export function isConflict(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 409;
}
