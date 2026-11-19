import { Component, computed, input } from '@angular/core';
import { flagLabels } from '../../models/labels';
import { ItemFlag } from '../../models/work-item';

@Component({
  selector: 'app-flag-list',
  template: '<span class="flags">{{ text() }}</span>',
  styleUrl: './flag-list.css',
})
export class FlagList {
  readonly flags = input.required<readonly ItemFlag[]>();
  protected readonly text = computed(() => this.flags().map((flag) => flagLabels[flag]).join(', '));
}
