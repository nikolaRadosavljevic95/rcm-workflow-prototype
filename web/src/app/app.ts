import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppHeader } from './components/app-header/app-header';
import { Session } from './services/session';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, AppHeader],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly session = inject(Session);
}
