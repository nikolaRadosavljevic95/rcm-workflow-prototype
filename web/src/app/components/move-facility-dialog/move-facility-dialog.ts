import {
  Component,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Facilities, MoveFacilityResult } from '../../models/facility';
import { FacilityRef } from '../../models/work-item';
import { FacilitiesApi } from '../../services/facilities-api';
import { problemMessage } from '../../services/problem';

// The current owner comes from the facility list, because this month's item may still belong to someone else.
@Component({
  selector: 'app-move-facility-dialog',
  templateUrl: './move-facility-dialog.html',
  styleUrl: './move-facility-dialog.css',
})
export class MoveFacilityDialog {
  private readonly api = inject(FacilitiesApi);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  readonly facility = input.required<FacilityRef>();
  readonly moved = output<MoveFacilityResult>();
  readonly cancelled = output<void>();

  protected readonly data = signal<Facilities | null>(null);
  protected readonly newOwnerId = signal<number | null>(null);
  protected readonly reason = signal('');
  protected readonly moveOpenItem = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly currentOwner = computed(
    () => this.data()?.facilities.find((row) => row.id === this.facility().id)?.owner ?? null,
  );

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
    effect(() => {
      this.facility();
      untracked(() => void this.load());
    });
  }

  protected pickOwner(value: string): void {
    this.newOwnerId.set(this.data()?.eligibleOwners.find((person) => String(person.id) === value)?.id ?? null);
  }

  // Esc closes the native dialog on its own. The parent removes this component instead.
  protected dismiss(event: Event): void {
    event.preventDefault();
    if (!this.busy()) {
      this.cancelled.emit();
    }
  }

  protected async confirm(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      this.moved.emit(
        await this.api.move(this.facility().id, {
          newOwnerId: this.newOwnerId(),
          reason: this.reason(),
          moveOpenItem: this.moveOpenItem(),
        }),
      );
    } catch (error) {
      this.error.set(problemMessage(error, 'The facility was not moved.'));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(): Promise<void> {
    try {
      this.data.set(await this.api.list());
    } catch (error) {
      this.error.set(problemMessage(error, 'Could not load the people who can own a facility.'));
    }
  }
}
