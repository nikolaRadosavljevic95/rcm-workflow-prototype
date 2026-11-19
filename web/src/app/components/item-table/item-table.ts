import { Component, computed, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { WorkItemRow } from '../../models/work-item';
import { FlagList } from '../flag-list/flag-list';
import { MoneyPipe } from '../money.pipe';
import { StatusBadge } from '../status-badge/status-badge';

type SortKey = 'arTotalCents' | 'arOver90Cents';

interface Sort {
  key: SortKey;
  descending: boolean;
}

// Rows keep the API's order until someone sorts by a balance column. Empty balances always sort last.
@Component({
  selector: 'app-item-table',
  imports: [RouterLink, StatusBadge, FlagList, MoneyPipe],
  templateUrl: './item-table.html',
  styleUrl: './item-table.css',
})
export class ItemTable {
  private readonly router = inject(Router);

  readonly rows = input.required<readonly WorkItemRow[]>();
  readonly showPeople = input(false);
  readonly showTotal = input(false);

  protected readonly sort = signal<Sort | null>(null);
  protected readonly sortedRows = computed(() => {
    const sort = this.sort();
    const rows = [...this.rows()];
    if (sort === null) {
      return rows;
    }
    return rows.sort((a, b) => compareCents(a[sort.key], b[sort.key], sort.descending));
  });

  protected toggleSort(key: SortKey): void {
    const current = this.sort();
    this.sort.set({ key, descending: current?.key === key ? !current.descending : true });
  }

  protected ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
    const current = this.sort();
    if (current?.key !== key) {
      return 'none';
    }
    return current.descending ? 'descending' : 'ascending';
  }

  protected async open(row: WorkItemRow): Promise<void> {
    await this.router.navigate(['/items', row.id]);
  }
}

function compareCents(a: number | null, b: number | null, descending: boolean): number {
  if (a === null || b === null) {
    return a === b ? 0 : a === null ? 1 : -1;
  }
  return descending ? b - a : a - b;
}
