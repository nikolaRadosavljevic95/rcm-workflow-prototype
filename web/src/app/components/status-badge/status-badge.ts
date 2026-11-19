import { Component, computed, input } from '@angular/core';
import { statusLabels } from '../../models/labels';
import { WorkItemStatus } from '../../models/work-item';

@Component({
  selector: 'app-status-badge',
  template: '<span class="badge" [class]="status()">{{ label() }}</span>',
  styleUrl: './status-badge.css',
})
export class StatusBadge {
  readonly status = input.required<WorkItemStatus>();
  protected readonly label = computed(() => statusLabels[this.status()]);
}
