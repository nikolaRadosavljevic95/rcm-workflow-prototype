import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Role, User } from '../models/user';
import { Session, homePath } from './session';

// Allows signed-in users, limited to route.data.roles when the route sets it.
export const signedInGuard: CanActivateFn = async (route) => {
  const router = inject(Router);
  const user = await currentUser(inject(Session));
  if (user === null) {
    return router.parseUrl('/sign-in');
  }

  const roles: readonly Role[] | undefined = route.data['roles'];
  return roles === undefined || roles.includes(user.role) ? true : router.parseUrl(homePath(user.role));
};

// Sends the root URL to the user's home screen.
export const homeGuard: CanActivateFn = async () => {
  const router = inject(Router);
  const user = await currentUser(inject(Session));
  return router.parseUrl(user === null ? '/sign-in' : homePath(user.role));
};

// A failed /api/me (unknown user, API down) counts as not signed in. The sign-in page shows why.
async function currentUser(session: Session): Promise<User | null> {
  try {
    return await session.current();
  } catch {
    return null;
  }
}
