import { Cycle, MonthActions } from './cycle';

export type WorkItemStatus =
  | 'NotStarted'
  | 'InProgress'
  | 'Blocked'
  | 'Submitted'
  | 'Returned'
  | 'Approved'
  | 'Incomplete';

export type WorkflowAction = 'Start' | 'Block' | 'Unblock' | 'Submit' | 'Approve' | 'Return';

export type ItemFlag = 'Overdue' | 'NeedsCover' | 'Covered' | 'MissedLastMonth';

export type WorkItemEventType = 'Created' | 'StatusChanged' | 'Commented' | 'Reassigned';

export interface PersonRef {
  id: number;
  name: string;
}

export interface FacilityRef {
  id: number;
  code: string;
  name: string;
}

export interface WorkItemRow {
  id: number;
  facility: FacilityRef;
  owner: PersonRef;
  // Null when the owner reports to no one: any other manager reviews.
  reviewer: PersonRef | null;
  team: PersonRef;
  status: WorkItemStatus;
  dueDate: string;
  flags: ItemFlag[];
  // Who covers the owner today, and the owner again when that cover is the viewer.
  coveredBy: PersonRef | null;
  coveringFor: PersonRef | null;
  arTotalCents: number | null;
  arOver90Cents: number | null;
  allowedActions: WorkflowAction[];
  awaitingMyReview: boolean;
  version: number;
}

export interface MyWork {
  cycle: Cycle;
  needsAction: WorkItemRow[];
  waitingOrDone: WorkItemRow[];
}

export interface PersonSummary {
  person: PersonRef;
  open: number;
  submitted: number;
  approved: number;
  // Today's absence, if any: last day away and the cover who counts today.
  awayUntil: string | null;
  cover: PersonRef | null;
}

export interface StatusCount {
  status: WorkItemStatus;
  count: number;
}

export interface TeamSummary {
  supervisor: PersonRef;
  items: number;
  approved: number;
  submitted: number;
  open: number;
  overdue: number;
}

export interface Overview {
  cycle: Cycle;
  waitingForMyReview: WorkItemRow[];
  needsAttention: WorkItemRow[];
  people: PersonSummary[];
  items: WorkItemRow[];
  progress: StatusCount[] | null;
  teams: TeamSummary[] | null;
  monthActions: MonthActions;
  // Shows Move facility on every row of All items.
  canMoveFacilities: boolean;
}

export interface WorkItemEvent {
  id: number;
  type: WorkItemEventType;
  fromStatus: WorkItemStatus | null;
  toStatus: WorkItemStatus | null;
  actor: PersonRef;
  onBehalfOf: PersonRef | null;
  fromUser: PersonRef | null;
  toUser: PersonRef | null;
  text: string | null;
  createdAt: string;
}

export interface LastMonth {
  status: WorkItemStatus;
  summary: string | null;
}

export interface WorkItemDetail {
  item: WorkItemRow;
  cycle: Cycle;
  summary: string | null;
  blockedReason: string | null;
  lastMonth: LastMonth | null;
  // Newest first.
  events: WorkItemEvent[];
}
