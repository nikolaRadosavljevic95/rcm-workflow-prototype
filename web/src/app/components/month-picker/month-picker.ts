import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Cycle } from '../../models/cycle';
import { monthLabel } from '../../models/labels';
import { CyclesApi } from '../../services/cycles-api';

// The chosen month lives in the "cycle" query parameter, so a reload or a shared link keeps it.
export function cycleIdFrom(queryValue: string | undefined): number | null {
  const id = Number(queryValue);
  return queryValue !== undefined && Number.isInteger(id) && id > 0 ? id : null;
}

@Component({
  selector: 'app-month-picker',
  imports: [DatePipe],
  templateUrl: './month-picker.html',
  styleUrl: './month-picker.css',
})
export class MonthPicker {
  private readonly cyclesApi = inject(CyclesApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly selected = input.required<Cycle>();
  protected readonly cycles = signal<Cycle[]>([]);

  constructor() {
    void this.loadCycles();
  }

  protected label(cycle: Cycle): string {
    return `${monthLabel(cycle)}, ${cycle.status === 'Open' ? 'open' : 'closed'}`;
  }

  protected async pick(value: string): Promise<void> {
    await this.router.navigate([], { relativeTo: this.route, queryParams: { cycle: value } });
  }

  private async loadCycles(): Promise<void> {
    try {
      this.cycles.set(await this.cyclesApi.list());
    } catch {
      this.cycles.set([this.selected()]);
    }
  }
}
