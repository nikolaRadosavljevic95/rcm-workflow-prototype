import { Component, input, linkedSignal, output } from '@angular/core';
import { AbsenceInput, AbsenceRow } from '../../models/absence';
import { User } from '../../models/user';
import { PersonRef } from '../../models/work-item';

export interface AbsenceSave {
  userId: number;
  input: AbsenceInput;
}

// New absence when no absence is given. The person of an existing absence never changes.
@Component({
  selector: 'app-absence-form',
  templateUrl: './absence-form.html',
  styleUrl: './absence-form.css',
})
export class AbsenceForm {
  readonly absence = input<AbsenceRow | null>(null);
  readonly people = input.required<readonly PersonRef[]>();
  readonly users = input.required<readonly User[]>();
  readonly busy = input(false);
  readonly error = input<string | null>(null);

  readonly save = output<AbsenceSave>();
  readonly remove = output<void>();
  readonly cancel = output<void>();

  protected readonly personId = linkedSignal<number | null>(
    () => this.absence()?.person.id ?? this.people().at(0)?.id ?? null,
  );
  protected readonly startDate = linkedSignal(() => this.absence()?.startDate ?? localToday());
  protected readonly endDate = linkedSignal(() => this.absence()?.endDate ?? localToday());
  protected readonly coverId = linkedSignal<number | null>(() => this.absence()?.cover?.id ?? null);
  protected readonly note = linkedSignal(() => this.absence()?.note ?? '');

  protected pickPerson(value: string): void {
    this.personId.set(this.people().find((person) => String(person.id) === value)?.id ?? null);
  }

  protected pickCover(value: string): void {
    this.coverId.set(this.users().find((user) => String(user.id) === value)?.id ?? null);
  }

  protected submit(): void {
    const userId = this.personId();
    if (userId === null) {
      return;
    }
    this.save.emit({
      userId,
      input: {
        startDate: this.startDate(),
        endDate: this.endDate(),
        coverUserId: this.coverId(),
        note: this.note().trim() === '' ? null : this.note().trim(),
      },
    });
  }
}

// The default date only. The API checks every date against the business calendar.
function localToday(): string {
  const now = new Date();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}
