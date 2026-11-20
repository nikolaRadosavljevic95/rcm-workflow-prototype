import { FacilityRef, PersonRef, WorkItemStatus } from './work-item';

export type CycleStatus = 'Open' | 'Closed';

export interface Cycle {
  id: number;
  year: number;
  month: number;
  dueDate: string;
  status: CycleStatus;
}

// Which month buttons the API allows a manager on the month shown.
export interface MonthActions {
  canClose: boolean;
  openNext: NextMonth | null;
}

export interface NextMonth {
  year: number;
  month: number;
  defaultDueDate: string;
}

export interface ClosePreviewRow {
  id: number;
  facility: FacilityRef;
  status: WorkItemStatus;
  owner: PersonRef;
}

// The items that become Incomplete when the month closes.
export interface ClosePreview {
  cycle: Cycle;
  items: ClosePreviewRow[];
}
