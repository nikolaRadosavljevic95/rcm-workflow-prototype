import { Component, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Role, User } from '../../models/user';
import { DemoApi } from '../../services/demo-api';
import { Session, homePath } from '../../services/session';

const groups: readonly { role: Role; label: string }[] = [
  { role: 'Manager', label: 'Managers' },
  { role: 'Supervisor', label: 'Supervisors' },
  { role: 'Biller', label: 'Billers' },
];

@Component({
  selector: 'app-sign-in',
  templateUrl: './sign-in.html',
  styleUrl: './sign-in.css',
})
export class SignInPage {
  private readonly demoApi = inject(DemoApi);
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly users = signal<User[]>([]);
  protected readonly message = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly groups = computed(() =>
    groups.map((group) => ({
      label: group.label,
      users: this.users().filter((user) => user.role === group.role),
    })),
  );

  constructor() {
    void this.loadUsers();
  }

  protected async signIn(user: User): Promise<void> {
    this.busy.set(true);
    try {
      const me = await this.session.signIn(user.id);
      await this.router.navigateByUrl(homePath(me.role));
    } catch {
      this.message.set(`Could not sign in as ${user.name}.`);
    } finally {
      this.busy.set(false);
    }
  }

  protected async reset(): Promise<void> {
    this.busy.set(true);
    try {
      await this.demoApi.reset();
      this.session.signOut();
      await this.loadUsers();
      this.message.set('Demo data was reset.');
    } catch {
      this.message.set('Reset failed. Is the API running?');
    } finally {
      this.busy.set(false);
    }
  }

  private async loadUsers(): Promise<void> {
    try {
      this.users.set(await this.demoApi.getUsers());
    } catch {
      this.message.set('The API is not reachable. Start it with: dotnet run --project src/Rcm.Api');
    }
  }
}
