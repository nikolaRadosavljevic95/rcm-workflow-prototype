import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CloseMonthDialog } from '../../components/close-month-dialog/close-month-dialog';
import { ItemTable } from '../../components/item-table/item-table';
import { MonthPicker, cycleIdFrom } from '../../components/month-picker/month-picker';
import { MoveFacilityDialog } from '../../components/move-facility-dialog/move-facility-dialog';
import { OpenMonthDialog } from '../../components/open-month-dialog/open-month-dialog';
import { Cycle } from '../../models/cycle';
import { flagLabels, statusLabels } from '../../models/labels';
import { FacilityRef, ItemFlag, Overview, PersonRef, WorkItemStatus } from '../../models/work-item';
import { problemMessage } from '../../services/problem';
import { WorkItemsApi } from '../../services/work-items-api';

const allFlags: readonly ItemFlag[] = ['Overdue', 'NeedsCover', 'Covered', 'MissedLastMonth'];

@Component({
  selector: 'app-overview',
  imports: [CloseMonthDialog, DatePipe, ItemTable, MonthPicker, MoveFacilityDialog, OpenMonthDialog],
  templateUrl: './overview.html',
  styleUrl: './overview.css',
})
export class OverviewPage {
  private readonly api = inject(WorkItemsApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Bound from the "cycle" query parameter.
  readonly cycle = input<string>();

  protected readonly overview = signal<Overview | null>(null);
  protected readonly message = signal<string | null>(null);
  protected readonly statusLabels = statusLabels;
  protected readonly flagLabels = flagLabels;
  protected readonly allFlags = allFlags;
  protected readonly monthDialog = signal<'close' | 'open' | null>(null);
  protected readonly movingFacility = signal<FacilityRef | null>(null);

  // The All items filters only narrow what the API already sent.
  protected readonly statusFilter = signal<WorkItemStatus | null>(null);
  protected readonly ownerFilter = signal<number | null>(null);
  protected readonly flagFilter = signal<ItemFlag | null>(null);

  protected readonly statuses = computed(() => distinct((this.overview()?.items ?? []).map((row) => row.status)));
  protected readonly owners = computed(() => {
    const byId = new Map<number, PersonRef>((this.overview()?.items ?? []).map((row) => [row.owner.id, row.owner]));
    return [...byId.values()].sort((a, b) => a.name.localeCompare(b.name));
  });
  protected readonly filteredItems = computed(() =>
    (this.overview()?.items ?? []).filter(
      (row) =>
        (this.statusFilter() === null || row.status === this.statusFilter()) &&
        (this.ownerFilter() === null || row.owner.id === this.ownerFilter()) &&
        (this.flagFilter() === null || row.flags.some((flag) => flag === this.flagFilter())),
    ),
  );

  constructor() {
    effect(() => {
      const cycleId = cycleIdFrom(this.cycle());
      untracked(() => {
        // Filters belong to one month. Its statuses and owners may not exist in the next.
        this.statusFilter.set(null);
        this.ownerFilter.set(null);
        this.flagFilter.set(null);
        void this.load(cycleId);
      });
    });
  }

  protected filterStatus(value: string): void {
    this.statusFilter.set(this.statuses().find((status) => status === value) ?? null);
  }

  protected filterOwner(value: string): void {
    this.ownerFilter.set(this.owners().find((owner) => String(owner.id) === value)?.id ?? null);
  }

  protected filterFlag(value: string): void {
    this.flagFilter.set(allFlags.find((flag) => flag === value) ?? null);
  }

  protected async monthClosed(cycle: Cycle): Promise<void> {
    this.monthDialog.set(null);
    await this.load(cycle.id);
  }

  // The new month goes into the URL, which reloads the page through the cycle input.
  protected async monthOpened(cycle: Cycle): Promise<void> {
    this.monthDialog.set(null);
    await this.router.navigate([], { relativeTo: this.route, queryParams: { cycle: cycle.id } });
  }

  protected async facilityMoved(): Promise<void> {
    this.movingFacility.set(null);
    const shown = this.overview();
    await this.load(shown === null ? null : shown.cycle.id);
  }

  private async load(cycleId: number | null): Promise<void> {
    try {
      this.overview.set(await this.api.overview(cycleId));
      this.message.set(null);
    } catch (error) {
      this.message.set(problemMessage(error, 'Could not load the overview.'));
    }
  }
}

function distinct<T>(values: readonly T[]): T[] {
  return [...new Set(values)];
}
