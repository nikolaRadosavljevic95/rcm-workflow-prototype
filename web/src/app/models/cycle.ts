export type CycleStatus = 'Open' | 'Closed';

export interface Cycle {
  id: number;
  year: number;
  month: number;
  dueDate: string;
  status: CycleStatus;
}
