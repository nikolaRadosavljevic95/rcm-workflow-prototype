import { PersonRef } from './work-item';

export interface FacilityRow {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
  owner: PersonRef;
}

export interface Facilities {
  facilities: FacilityRow[];
  // Who may become an owner. Empty unless the viewer can move facilities.
  eligibleOwners: PersonRef[];
}

export interface MoveFacilityInput {
  newOwnerId: number | null;
  reason: string;
  moveOpenItem: boolean;
}

export interface MoveFacilityResult {
  facility: FacilityRow;
  movedItemId: number | null;
}
