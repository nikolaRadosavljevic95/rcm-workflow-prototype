import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { User } from '../models/user';

@Injectable({ providedIn: 'root' })
export class DemoApi {
  private readonly http = inject(HttpClient);

  async getUsers(): Promise<User[]> {
    return firstValueFrom(this.http.get<User[]>('/api/demo/users'));
  }

  async reset(): Promise<void> {
    await firstValueFrom(this.http.post<void>('/api/demo/reset', null));
  }
}
