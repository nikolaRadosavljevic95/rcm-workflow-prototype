import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Session } from './session';

// Sends the demo sign-in on every API call. A 401 means the stored user is gone, so sign out.
export const demoUserInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(Session);
  const router = inject(Router);
  const userId = session.userId();
  const withUser =
    userId === null ? request : request.clone({ setHeaders: { 'X-Demo-User': String(userId) } });

  return next(withUser).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        session.signOut();
        void router.navigateByUrl('/sign-in');
      }
      return throwError(() => error);
    }),
  );
};
