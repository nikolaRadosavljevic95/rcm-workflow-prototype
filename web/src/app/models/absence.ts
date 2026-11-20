import { PersonRef } from './work-item';

export type AbsenceToday = 'Upcoming' | 'AwayCovered' | 'AwayNeedsCover' | 'Ended';

export interface AbsenceRow {
  id: number;
  person: PersonRef;
  startDate: string;
  endDate: string;
  cover: PersonRef | null;
  note: string | null;
  today: AbsenceToday;
  canEdit: boolean;
  canDelete: boolean;
}

export interface Absences {
  // Who the viewer may record an absence for.
  people: PersonRef[];
  absences: AbsenceRow[];
}

export interface AbsenceInput {
  startDate: string;
  endDate: string;
  coverUserId: number | null;
  note: string | null;
}
