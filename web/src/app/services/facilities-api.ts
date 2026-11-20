import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Facilities, MoveFacilityInput, MoveFacilityResult } from '../models/facility';

@Injectable({ providedIn: 'root' })
export class FacilitiesApi {
  private readonly http = inject(HttpClient);

  async list(): Promise<Facilities> {
    return firstValueFrom(this.http.get<Facilities>('/api/facilities'));
  }

  async move(facilityId: number, input: MoveFacilityInput): Promise<MoveFacilityResult> {
    return firstValueFrom(this.http.put<MoveFacilityResult>(`/api/facilities/${facilityId}/owner`, input));
  }
}
