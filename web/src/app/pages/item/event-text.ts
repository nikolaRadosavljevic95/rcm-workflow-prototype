import { statusLabels } from '../../models/labels';
import { WorkItemEvent } from '../../models/work-item';

export function actorText(event: WorkItemEvent): string {
  return event.onBehalfOf === null ? event.actor.name : `${event.actor.name} for ${event.onBehalfOf.name}`;
}

export function eventText(event: WorkItemEvent): string {
  const what = describe(event);
  return event.text === null ? what : `${what}: ${event.text}`;
}

function describe(event: WorkItemEvent): string {
  switch (event.type) {
    case 'Created':
      return 'Opened the month';
    case 'Commented':
      return 'Commented';
    case 'Reassigned':
      return `Moved from ${event.fromUser?.name ?? 'nobody'} to ${event.toUser?.name ?? 'nobody'}`;
    case 'StatusChanged':
      return statusChange(event);
  }
}

function statusChange(event: WorkItemEvent): string {
  switch (event.toStatus) {
    case 'InProgress':
      return event.fromStatus === 'Blocked' ? 'Unblocked' : 'Started';
    case 'Incomplete':
      return 'Closed as incomplete';
    case null:
      return 'Changed';
    default:
      return statusLabels[event.toStatus];
  }
}
