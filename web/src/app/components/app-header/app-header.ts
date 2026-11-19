import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { User } from '../../models/user';
import { Session } from '../../services/session';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './app-header.html',
  styleUrl: './app-header.css',
})
export class AppHeader {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  readonly user = input.required<User>();
  protected readonly showOverview = computed(() => this.user().role !== 'Biller');

  protected async signOut(): Promise<void> {
    this.session.signOut();
    await this.router.navigateByUrl('/sign-in');
  }
}
