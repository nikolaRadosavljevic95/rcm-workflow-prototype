import { Component, computed, input } from '@angular/core';
import { flagLabels } from '../../models/labels';
import { ItemFlag, WorkItemRow } from '../../models/work-item';

type FlaggedRow = Pick<WorkItemRow, 'flags' | 'coveredBy' | 'coveringFor'>;

@Component({
  selector: 'app-flag-list',
  template: '<span class="flags">{{ text() }}</span>',
  styleUrl: './flag-list.css',
})
export class FlagList {
  readonly row = input.required<FlaggedRow>();
  protected readonly text = computed(() => {
    const row = this.row();
    return row.flags.map((flag) => label(flag, row)).join(', ');
  });
}

// The viewer who is the cover reads "Covering for Blake"; everyone else reads "Covered by Alex".
function label(flag: ItemFlag, row: FlaggedRow): string {
  if (flag !== 'Covered') {
    return flagLabels[flag];
  }
  if (row.coveringFor !== null) {
    return `Covering for ${row.coveringFor.name}`;
  }
  return row.coveredBy === null ? flagLabels.Covered : `Covered by ${row.coveredBy.name}`;
}
