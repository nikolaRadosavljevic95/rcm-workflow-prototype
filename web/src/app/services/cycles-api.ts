import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Cycle } from '../models/cycle';

@Injectable({ providedIn: 'root' })
export class CyclesApi {
  private readonly http = inject(HttpClient);

  // Newest first.
  async list(): Promise<Cycle[]> {
    return firstValueFrom(this.http.get<Cycle[]>('/api/cycles'));
  }
}
