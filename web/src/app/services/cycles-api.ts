import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ClosePreview, Cycle } from '../models/cycle';

@Injectable({ providedIn: 'root' })
export class CyclesApi {
  private readonly http = inject(HttpClient);

  // Newest first.
  async list(): Promise<Cycle[]> {
    return firstValueFrom(this.http.get<Cycle[]>('/api/cycles'));
  }

  // Opens the month after the latest one.
  async open(dueDate: string): Promise<Cycle> {
    return firstValueFrom(this.http.post<Cycle>('/api/cycles', { dueDate }));
  }

  async closePreview(cycleId: number): Promise<ClosePreview> {
    return firstValueFrom(this.http.get<ClosePreview>(`/api/cycles/${cycleId}/close-preview`));
  }

  async close(cycleId: number): Promise<Cycle> {
    return firstValueFrom(this.http.post<Cycle>(`/api/cycles/${cycleId}/close`, {}));
  }
}
