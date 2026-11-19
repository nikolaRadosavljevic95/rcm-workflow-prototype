import { Component, computed, input, output, signal } from '@angular/core';
import { actionLabels } from '../../models/labels';
import { WorkflowAction } from '../../models/work-item';

export interface ActionRequest {
  action: WorkflowAction;
  text: string | null;
}

// Block, Submit and Return ask for text before they are sent (docs/04, Item page).
const textPrompts: Partial<Record<WorkflowAction, string>> = {
  Block: 'Reason',
  Submit: 'Summary',
  Return: 'Comment for the owner',
};

@Component({
  selector: 'app-action-panel',
  templateUrl: './action-panel.html',
  styleUrl: './action-panel.css',
})
export class ActionPanel {
  readonly actions = input.required<readonly WorkflowAction[]>();
  readonly busy = input(false);
  readonly act = output<ActionRequest>();

  protected readonly labels = actionLabels;
  protected readonly pending = signal<WorkflowAction | null>(null);
  protected readonly text = signal('');
  protected readonly prompt = computed(() => {
    const action = this.pending();
    return action === null ? null : (textPrompts[action] ?? null);
  });

  protected choose(action: WorkflowAction): void {
    if (textPrompts[action] === undefined) {
      this.act.emit({ action, text: null });
      return;
    }
    this.pending.set(action);
    this.text.set('');
  }

  protected confirm(): void {
    const action = this.pending();
    if (action !== null && this.text().trim() !== '') {
      this.act.emit({ action, text: this.text() });
      this.pending.set(null);
    }
  }

  protected cancel(): void {
    this.pending.set(null);
  }
}
