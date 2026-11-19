import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Role, User } from '../models/user';

const storageKey = 'rcm.demoUserId';

export function homePath(role: Role): string {
  return role === 'Biller' ? '/my-work' : '/overview';
}

// The demo sign-in: the chosen user id in localStorage, and that user as loaded from /api/me.
@Injectable({ providedIn: 'root' })
export class Session {
  private readonly http = inject(HttpClient);

  readonly userId = signal<number | null>(readStoredId());
  readonly user = signal<User | null>(null);

  async signIn(userId: number): Promise<User> {
    localStorage.setItem(storageKey, String(userId));
    this.userId.set(userId);
    return this.reload();
  }

  // Loads the current user once per page load. Null when nobody is signed in.
  async current(): Promise<User | null> {
    if (this.userId() === null) {
      return null;
    }
    return this.user() ?? this.reload();
  }

  signOut(): void {
    localStorage.removeItem(storageKey);
    this.userId.set(null);
    this.user.set(null);
  }

  private async reload(): Promise<User> {
    const me = await firstValueFrom(this.http.get<User>('/api/me'));
    this.user.set(me);
    return me;
  }
}

function readStoredId(): number | null {
  const id = Number(localStorage.getItem(storageKey));
  return Number.isInteger(id) && id > 0 ? id : null;
}
