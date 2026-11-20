import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { AbsenceForm, AbsenceSave } from '../../components/absence-form/absence-form';
import { AbsenceRow, Absences } from '../../models/absence';
import { absenceTodayLabels } from '../../models/labels';
import { User } from '../../models/user';
import { AbsencesApi } from '../../services/absences-api';
import { problemMessage } from '../../services/problem';

@Component({
  selector: 'app-absences',
  imports: [AbsenceForm, DatePipe],
  templateUrl: './absences.html',
  styleUrl: './absences.css',
})
export class AbsencesPage {
  private readonly api = inject(AbsencesApi);

  protected readonly data = signal<Absences | null>(null);
  protected readonly users = signal<User[]>([]);
  protected readonly message = signal<string | null>(null);
  protected readonly formError = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly todayLabels = absenceTodayLabels;

  // 'new' while recording a new absence, the row while editing one, empty when the form is closed.
  protected readonly editing = signal<AbsenceRow | 'new' | null>(null);
  protected readonly editedAbsence = computed(() => {
    const editing = this.editing();
    return editing === 'new' ? null : editing;
  });

  constructor() {
    void this.load();
  }

  protected startNew(): void {
    this.formError.set(null);
    this.editing.set('new');
  }

  protected edit(row: AbsenceRow): void {
    this.formError.set(null);
    this.editing.set(row);
  }

  protected close(): void {
    this.editing.set(null);
  }

  protected async save(change: AbsenceSave): Promise<void> {
    const editing = this.editing();
    await this.run(async () => {
      if (editing === 'new') {
        await this.api.create(change.userId, change.input);
      } else if (editing !== null) {
        await this.api.update(editing.id, change.input);
      }
    }, 'The absence was not saved.');
  }

  protected async remove(): Promise<void> {
    const editing = this.editing();
    if (editing === null || editing === 'new') {
      return;
    }
    await this.run(() => this.api.delete(editing.id), 'The absence was not deleted.');
  }

  // Runs a change, then closes the form and reloads. API errors stay in the form.
  private async run(change: () => Promise<unknown>, fallback: string): Promise<void> {
    this.busy.set(true);
    try {
      await change();
      this.editing.set(null);
      await this.load();
    } catch (error) {
      this.formError.set(problemMessage(error, fallback));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      const [data, users] = await Promise.all([this.api.list(), this.api.activeUsers()]);
      this.data.set(data);
      this.users.set(users);
      this.message.set(null);
    } catch (error) {
      this.message.set(problemMessage(error, 'Could not load absences.'));
    }
  }
}
