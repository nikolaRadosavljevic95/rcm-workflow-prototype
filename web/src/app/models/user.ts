import { PersonRef } from './work-item';

export type Role = 'Biller' | 'Supervisor' | 'Manager';

export interface User {
  id: number;
  name: string;
  role: Role;
  reportsToId: number | null;
}

// The signed-in user from /api/me, with the people they cover today.
export interface Me extends User {
  coveringFor: PersonRef[];
}
