import { Component, computed, input, signal } from '@angular/core';
import { actionLabels } from '../../models/labels';
import { WorkflowAction } from '../../models/work-item';

export interface ActionRequest {
  action: WorkflowAction;
  text: string | null;
}

interface TextPrompt {
  label: string;
  maxLength: number;
}

// Block, Submit and Return ask for text before they are sent (docs/04, Item page). Limits match the API.
const textPrompts: Partial<Record<WorkflowAction, TextPrompt>> = {
  Block: { label: 'Reason', maxLength: 500 },
  Submit: { label: 'Summary', maxLength: 2000 },
  Return: { label: 'Comment for the owner', maxLength: 2000 },
};

@Component({
  selector: 'app-action-panel',
  templateUrl: './action-panel.html',
  styleUrl: './action-panel.css',
})
export class ActionPanel {
  readonly actions = input.required<readonly WorkflowAction[]>();
  readonly busy = input(false);
  // Resolves to true when the API accepted the action. The prompt and its text stay until then.
  readonly send = input.required<(request: ActionRequest) => Promise<boolean>>();

  protected readonly labels = actionLabels;
  protected readonly pending = signal<WorkflowAction | null>(null);
  protected readonly text = signal('');
  protected readonly prompt = computed(() => {
    // After a reload the action may no longer be allowed, so the prompt hides.
    const action = this.pending();
    const entry = action === null || !this.actions().includes(action) ? undefined : textPrompts[action];
    return action === null || entry === undefined ? null : { action, ...entry };
  });

  protected async choose(action: WorkflowAction): Promise<void> {
    if (textPrompts[action] === undefined) {
      await this.send()({ action, text: null });
      return;
    }
    this.pending.set(action);
    this.text.set('');
  }

  protected async confirm(): Promise<void> {
    const action = this.pending();
    if (action === null || this.text().trim() === '') {
      return;
    }
    if (await this.send()({ action, text: this.text() })) {
      this.pending.set(null);
    }
  }

  protected cancel(): void {
    this.pending.set(null);
  }
}
