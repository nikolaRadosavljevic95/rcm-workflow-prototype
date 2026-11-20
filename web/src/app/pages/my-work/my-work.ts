import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ItemTable } from '../../components/item-table/item-table';
import { MonthPicker, cycleIdFrom } from '../../components/month-picker/month-picker';
import { MyWork } from '../../models/work-item';
import { problemMessage } from '../../services/problem';
import { Session } from '../../services/session';
import { WorkItemsApi } from '../../services/work-items-api';

@Component({
  selector: 'app-my-work',
  imports: [ItemTable, MonthPicker],
  templateUrl: './my-work.html',
})
export class MyWorkPage {
  private readonly api = inject(WorkItemsApi);
  private readonly session = inject(Session);

  // Bound from the "cycle" query parameter.
  readonly cycle = input<string>();

  protected readonly work = signal<MyWork | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly coveringFor = computed(() =>
    (this.session.user()?.coveringFor ?? []).map((person) => person.name).join(' and '),
  );

  constructor() {
    effect(() => {
      const cycleId = cycleIdFrom(this.cycle());
      untracked(() => void this.load(cycleId));
    });
  }

  private async load(cycleId: number | null): Promise<void> {
    try {
      const [work] = await Promise.all([this.api.myWork(cycleId), this.session.refresh()]);
      this.work.set(work);
      this.message.set(null);
    } catch (error) {
      this.message.set(problemMessage(error, 'Could not load your work.'));
    }
  }
}
