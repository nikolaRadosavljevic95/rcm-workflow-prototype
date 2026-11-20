import { DatePipe, Location } from '@angular/common';
import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { ActionPanel, ActionRequest } from '../../components/action-panel/action-panel';
import { FlagList } from '../../components/flag-list/flag-list';
import { MoneyPipe } from '../../components/money.pipe';
import { StatusBadge } from '../../components/status-badge/status-badge';
import { monthLabel, statusLabels } from '../../models/labels';
import { WorkItemDetail } from '../../models/work-item';
import { isConflict, problemMessage } from '../../services/problem';
import { WorkItemsApi } from '../../services/work-items-api';
import { actorText, eventText } from './event-text';

@Component({
  selector: 'app-item',
  imports: [ActionPanel, DatePipe, FlagList, MoneyPipe, StatusBadge],
  templateUrl: './item.html',
  styleUrl: './item.css',
})
export class ItemPage {
  private readonly api = inject(WorkItemsApi);
  private readonly location = inject(Location);

  // Bound from the :id route parameter.
  readonly id = input.required<string>();

  protected readonly detail = signal<WorkItemDetail | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly comment = signal('');
  protected readonly statusLabels = statusLabels;
  protected readonly monthLabel = monthLabel;
  protected readonly actorText = actorText;
  protected readonly eventText = eventText;

  constructor() {
    effect(() => {
      const id = Number(this.id());
      untracked(() => void this.load(id));
    });
  }

  protected back(): void {
    this.location.back();
  }

  // An arrow function so the action panel can call it with this page bound.
  protected readonly act = async (request: ActionRequest): Promise<boolean> => {
    const current = this.detail();
    if (current === null) {
      return false;
    }
    return this.run(current.item.id, () =>
      this.api.transition(current.item.id, request.action, request.text, current.item.version),
    );
  };

  protected async addComment(): Promise<void> {
    const current = this.detail();
    const text = this.comment().trim();
    if (current === null || text === '') {
      return;
    }
    if (await this.run(current.item.id, () => this.api.comment(current.item.id, text))) {
      this.comment.set('');
    }
  }

  // Sends a change and shows the item the API returns. On a 409 the item is reloaded so the user sees the newer state.
  private async run(id: number, send: () => Promise<WorkItemDetail>): Promise<boolean> {
    this.busy.set(true);
    try {
      this.detail.set(await send());
      this.message.set(null);
      return true;
    } catch (error) {
      if (isConflict(error)) {
        await this.load(id);
        this.message.set('Someone changed this item. Reloaded.');
      } else {
        this.message.set(problemMessage(error, 'The change was not saved.'));
      }
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  private async load(id: number): Promise<void> {
    try {
      this.detail.set(await this.api.get(id));
      this.message.set(null);
    } catch (error) {
      this.detail.set(null);
      this.message.set(problemMessage(error, 'Could not load the item.'));
    }
  }
}
