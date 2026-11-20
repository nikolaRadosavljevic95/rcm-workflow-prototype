import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AbsenceInput, AbsenceRow, Absences } from '../models/absence';
import { User } from '../models/user';

@Injectable({ providedIn: 'root' })
export class AbsencesApi {
  private readonly http = inject(HttpClient);

  async list(): Promise<Absences> {
    return firstValueFrom(this.http.get<Absences>('/api/absences'));
  }

  // Everyone active, for the cover picker. The API decides who may cover whom.
  async activeUsers(): Promise<User[]> {
    return firstValueFrom(this.http.get<User[]>('/api/users'));
  }

  async create(userId: number, input: AbsenceInput): Promise<AbsenceRow> {
    return firstValueFrom(this.http.post<AbsenceRow>('/api/absences', { userId, ...input }));
  }

  async update(id: number, input: AbsenceInput): Promise<AbsenceRow> {
    return firstValueFrom(this.http.put<AbsenceRow>(`/api/absences/${id}`, input));
  }

  async delete(id: number): Promise<void> {
    await firstValueFrom(this.http.delete<void>(`/api/absences/${id}`));
  }
}
