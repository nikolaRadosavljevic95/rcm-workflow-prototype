# Questions and assumptions

Status: accepted as working assumptions for v1 on 2026-11-19. Not yet confirmed by the business.

Ordered by impact. Questions 1 to 5 change the design the most.

1. **Unit of work.** Is a work item one facility for one month, one facility and payer for one month, or one resident account with a balance?
   - Assumption: one facility for one month. Account follow-up stays in the billing system.
   - Why it matters: it sets the main table, the data volume and whether the app holds PHI (protected health information).

2. **Supervision.** There are 5 billers and 4 supervisors, close to one supervisor per biller. Do supervisors also own facilities? Is supervision by person, by facility group or by payer? Can a biller have two supervisors? Is a supervisor role vacant or about to change?
   - Assumption: everyone reports to exactly one person. Supervisors can own facilities. A review goes to the person the owner reports to. Nobody approves their own work.
   - Why it matters: it decides who approves and who sees what, and supervision by facility group or payer would tie the reviewer to the facility instead of the person.

3. **Outside users.** Will anyone outside the RCM team use the app, such as facility administrators or business office managers?
   - Assumption: no. Internal RCM staff only.
   - Why it matters: outside users need a different sign-in model and data split by facility.

4. **Done.** What must be true for a facility review to count as done? Is there a standard checklist? Does a supervisor sign off?
   - Assumption: the owner submits with a short summary and the reviewer approves. No checklist.
   - Why it matters: it decides whether we need a review step, a checklist or both.

5. **Balances.** Where do balances come from: which billing system, which report, how often? Does v1 need them at all?
   - Assumption: v1 shows total AR (accounts receivable) and AR over 90 days per facility per month, from seed data. Production imports them from the month-end aged trial balance.
   - Why it matters: it decides whether "needs attention" is driven by money or only by dates and status, and whether there is an import to build.

6. **Month-end.** What happens to reviews not finished by month-end? Can two months be open at once?
   - Assumption: they close as Incomplete and stay in the history. One month is open at a time, and a manager opens and closes it.
   - Why it matters: it decides whether an item can span months, which changes reporting and every list query.

7. **Covers.** Does a cover take over the work or act on behalf of the owner? Does the same apply to approvers?
   - Assumption: the cover acts on behalf of the owner. The owner stays the owner and the cover ends on its own. Approvers work the same way, and any manager can approve.
   - Why it matters: it decides whether an absence changes data (a reassignment) or only who may act on which dates.

8. **Permissions.** Who records absences and picks covers? Who moves a facility to another owner for good?
   - Assumption: supervisors record absences for themselves and their team. Managers record them for anyone. Only managers move facilities.
   - Why it matters: it sets the permission rules for both ways of moving work.

9. **Resident data.** Will the app hold resident-level data, even by accident in comments?
   - Assumption: not by design. The comment box warns against it. Production still treats the app as possibly holding PHI.
   - Why it matters: PHI changes logging, access control and hosting, and free text is how it gets in.

10. **New app or module.** Is this a new app or a module inside the existing internal app?
    - Assumption: a standalone prototype, built so it can move into the existing app as one feature.
    - Why it matters: a module reuses the existing sign-in, UI shell and database, and must follow their conventions.
