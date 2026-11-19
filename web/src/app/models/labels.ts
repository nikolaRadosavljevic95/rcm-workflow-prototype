import { Cycle } from './cycle';
import { ItemFlag, WorkflowAction, WorkItemStatus } from './work-item';

export const statusLabels: Record<WorkItemStatus, string> = {
  NotStarted: 'Not started',
  InProgress: 'In progress',
  Blocked: 'Blocked',
  Submitted: 'Submitted',
  Returned: 'Returned',
  Approved: 'Approved',
  Incomplete: 'Incomplete',
};

export const flagLabels: Record<ItemFlag, string> = {
  Overdue: 'Overdue',
  MissedLastMonth: 'Missed last month',
};

export const actionLabels: Record<WorkflowAction, string> = {
  Start: 'Start',
  Block: 'Block',
  Unblock: 'Unblock',
  Submit: 'Submit',
  Approve: 'Approve',
  Return: 'Return',
};

export function monthLabel(cycle: Pick<Cycle, 'year' | 'month'>): string {
  return new Date(cycle.year, cycle.month - 1, 1).toLocaleString('en-US', { month: 'long', year: 'numeric' });
}
