# First version

## In v1
- Sign in as any seeded user from a picker.
- Land on the role's home screen: My work for billers, Overview for supervisors and managers.
- Move items through the statuses and rules in [02](02-workflow.md).
- See each item's history and last month's result.
- Record absences, with or without a cover. Covers act for owners and reviewers.
- Move a facility to a new owner for good, with or without this month's open item.
- Open a month, and close it after a preview.
- See total AR and AR over 90 days on every item, and sort by them.

## Not in v1
| Left out | Why |
|---|---|
| Reopen an approved item | Cut to fit session 2. First in What's next |
| Single-item reassignment | Cut to fit session 2. Covers handle absences. Second in What's next |
| Balance import | Export format unknown (01, Q5). Seeded numbers show where it plugs in |
| Balances for months opened in the app | No import yet. New months show "Not imported" |
| Checklist | Steps unknown (01, Q4). Guessed steps get ticked without being read |
| Email reminders, exports | Not needed to show the workflow |
| Admin screens for users, reporting lines and facilities | Seed data is enough for a demo |
| Account-level data | PHI. See [03](03-data-model.md#no-patient-level-data) |

## Screens
- Navigation: billers see My work. Supervisors and managers see Overview, My work and Absences.
- Decision: actions happen on the item page, not in lists. One extra click per action, but text input stays in one place.
- Decision: one Overview page for supervisors and managers, scoped by role. Less to build; neither role gets a layout of its own.

### Sign-in (demo only)
```
RCM Monthly Review                                      Demo data only
Sign in as
  Managers      Morgan  Quinn
  Supervisors   Sam  Taylor  Jordan  Casey
  Billers       Alex  Blake  Charlie  Dana  Emery
                                                    [Reset demo data]
```

### My work (biller home; others use it for items they own or cover)
```
[November 2026, open v]  Due Nov 18                        Alex, Biller
--------------------------------------------------------------------------
Needs action                                                 AR over 90
  Facility 03  Returned     Overdue                           $212,400
  Facility 02  Blocked      Overdue                            $98,100
  Facility 07  Not started  Overdue, Covering for Blake,
                            Missed last month                 $143,900
  Facility 01  In progress  Overdue                            $77,250
  ...
Waiting or done
  Facility 04  Submitted    Overdue                            $65,000
  Facility 05  Approved                                       $120,700
```
- Needs action: Returned, Blocked, Not started, In progress. Each group sorted by AR over 90, highest first.
- A row opens the item page.

### Overview (supervisor and manager home)
Supervisor:
```
[November 2026, open v]  Due Nov 18                  Jordan, Supervisor
Overview | My work | Absences
--------------------------------------------------------------------------
Waiting for my review (0)
Needs attention (3)
  Facility 14  Dana  Not started  Overdue, Needs cover, Missed last month
  Facility 13  Dana  Blocked      Overdue, Needs cover
  Facility 15  Dana  In progress  Overdue, Needs cover
People
  Dana   3 open  0 submitted  0 approved   Away until Nov 21, no cover
All items                                   [Status v] [Owner v] [Flag v]
  Facility  Owner  Reviewer  Status  Flags  AR total  AR over 90
```
Manager, in addition:
```
Progress   Approved 9  Submitted 3  Returned 1  In progress 3  Blocked 2  Not started 2
Teams      Supervisor  Items  Approved  Submitted  Open  Overdue
           Sam         8      ...
Actions    [Close month] while a month is open, [Open next month] when none is
All items  every row has [Move facility]
```
- Needs attention: Blocked, Returned, Overdue, Needs cover or Missed last month. Sorted by AR over 90.

### Item page
```
Facility 02, November 2026                                       [Back]
Owner Alex  Reviewer Sam  Due Nov 18  Status Blocked  Flags Overdue
AR total $412,300   AR over 90 $98,100
Last month: Approved. "All 90+ day accounts noted. Two escalated."
--------------------------------------------------------------------------
[Unblock]
Comment [__________________________________________________] [Add]
        Do not enter resident names or account numbers.
History
  Nov 17 10:02  Alex    Blocked: waiting for the facility's Medicaid pending list
  Nov 12 09:15  Alex    Started
  Nov 01 08:00  Morgan  Opened the month
```
- Only allowed actions show. Block, Submit and Return open a text box that must be filled.

### Absences (supervisors and managers)
```
Absences                                                   [New absence]
  Person  From    To      Cover  Today
  Blake   Nov 17  Nov 23  Alex   Away, covered                   [Edit]
  Dana    Nov 19  Nov 21  none   Away, needs cover               [Edit]
  Taylor  Nov 18  Nov 22  Sam    Away, covered                   [Edit]
Form: person, from, to, cover (optional), note.   [Save]  [Delete]
Errors show inline, for example "Alex is away on Nov 20".
```

### Manager dialogs
```
Close November 2026
  11 items are not approved and will become Incomplete:
    Facility 04  Submitted    Alex
    Facility 14  Not started  Dana
    ...
                                             [Cancel]  [Close month]

Open December 2026
  Due date [2026-12-15]                      [Cancel]  [Open month]

Move Facility 07
  From Blake to [person v]     Reason [______________________]
  [x] Also move this month's open item
                                             [Cancel]  [Move]
```

## What's next
In this order. Items 1 and 2 go into session 2 only if time is left after the tests pass.
1. **Reopen.** Approved to In progress. Reviewer or any manager, reason required, month open. Clears the approval fields; the event keeps the record.
2. **Single-item reassignment.** Changes one open item's owner for the current month. Supervisor for their team, manager for anyone. Reason required. The facility owner does not change.
3. **Balance import** from the billing system. See [05](05-technical-design.md#production-changes).
4. **Excel export** of a month.
5. **Admin screens** for users, reporting lines and facilities. A user who still owns facilities or open items cannot be deactivated.
6. **Email reminders**: due soon, returned, cover starting.
7. **Checklist**, once the business confirms the steps.
8. **Account-level follow-up**, as a separate decision, because it makes the app a PHI system.
