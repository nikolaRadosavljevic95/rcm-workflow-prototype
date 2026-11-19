import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { MyWork, Overview, WorkflowAction, WorkItemDetail } from '../models/work-item';

@Injectable({ providedIn: 'root' })
export class WorkItemsApi {
  private readonly http = inject(HttpClient);

  // A null month means the open one, or the latest when none is open.
  async myWork(cycleId: number | null): Promise<MyWork> {
    return firstValueFrom(this.http.get<MyWork>('/api/work-items', { params: listParams('mine', cycleId) }));
  }

  async overview(cycleId: number | null): Promise<Overview> {
    return firstValueFrom(this.http.get<Overview>('/api/work-items', { params: listParams('overview', cycleId) }));
  }

  async get(id: number): Promise<WorkItemDetail> {
    return firstValueFrom(this.http.get<WorkItemDetail>(`/api/work-items/${id}`));
  }

  async transition(id: number, action: WorkflowAction, text: string | null, version: number): Promise<WorkItemDetail> {
    return firstValueFrom(
      this.http.post<WorkItemDetail>(`/api/work-items/${id}/transitions`, { action, text, version }),
    );
  }

  async comment(id: number, text: string): Promise<WorkItemDetail> {
    return firstValueFrom(this.http.post<WorkItemDetail>(`/api/work-items/${id}/comments`, { text }));
  }
}

function listParams(view: 'mine' | 'overview', cycleId: number | null): HttpParams {
  const params = new HttpParams().set('view', view);
  return cycleId === null ? params : params.set('cycleId', cycleId);
}
