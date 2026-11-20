import {
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  linkedSignal,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Cycle, NextMonth } from '../../models/cycle';
import { monthLabel } from '../../models/labels';
import { CyclesApi } from '../../services/cycles-api';
import { problemMessage } from '../../services/problem';

// The API picks the month and checks the due date. The form only proposes its default.
@Component({
  selector: 'app-open-month-dialog',
  templateUrl: './open-month-dialog.html',
  styleUrl: './open-month-dialog.css',
})
export class OpenMonthDialog {
  private readonly api = inject(CyclesApi);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  readonly next = input.required<NextMonth>();
  readonly opened = output<Cycle>();
  readonly cancelled = output<void>();

  protected readonly dueDate = linkedSignal(() => this.next().defaultDueDate);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly title = computed(() => `Open ${monthLabel(this.next())}`);

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
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
      this.opened.emit(await this.api.open(this.dueDate()));
    } catch (error) {
      this.error.set(problemMessage(error, 'The month was not opened.'));
    } finally {
      this.busy.set(false);
    }
  }
}
