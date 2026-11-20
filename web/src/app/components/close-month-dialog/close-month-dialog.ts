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
import { ClosePreview, Cycle } from '../../models/cycle';
import { monthLabel } from '../../models/labels';
import { CyclesApi } from '../../services/cycles-api';
import { problemMessage } from '../../services/problem';
import { StatusBadge } from '../status-badge/status-badge';

// Shows what the close turns Incomplete before the manager confirms it.
@Component({
  selector: 'app-close-month-dialog',
  imports: [StatusBadge],
  templateUrl: './close-month-dialog.html',
  styleUrl: './close-month-dialog.css',
})
export class CloseMonthDialog {
  private readonly api = inject(CyclesApi);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  readonly cycle = input.required<Cycle>();
  readonly closed = output<Cycle>();
  readonly cancelled = output<void>();

  protected readonly preview = signal<ClosePreview | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly title = computed(() => `Close ${monthLabel(this.cycle())}`);

  constructor() {
    afterNextRender(() => this.dialog().nativeElement.showModal());
    effect(() => {
      const cycleId = this.cycle().id;
      untracked(() => void this.load(cycleId));
    });
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
      this.closed.emit(await this.api.close(this.cycle().id));
    } catch (error) {
      this.error.set(problemMessage(error, 'The month was not closed.'));
    } finally {
      this.busy.set(false);
    }
  }

  private async load(cycleId: number): Promise<void> {
    try {
      this.preview.set(await this.api.closePreview(cycleId));
    } catch (error) {
      this.error.set(problemMessage(error, 'Could not load the close preview.'));
    }
  }
}
