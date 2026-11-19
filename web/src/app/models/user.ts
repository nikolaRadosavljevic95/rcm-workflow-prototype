export type Role = 'Biller' | 'Supervisor' | 'Manager';

export interface User {
  id: number;
  name: string;
  role: Role;
  reportsToId: number | null;
}
