# Technical design

## Architecture
- An Angular 22 app calls an ASP.NET Core Minimal API on .NET 10. The API uses EF Core 10 with SQLite in the prototype and SQL Server in production.
- Three projects: `src/Rcm.Api`, `tests/Rcm.Api.Tests`, `web`.
- **One API project with feature folders, no layers.** Small and easy to follow. Nothing enforces boundaries; if it grows, split it into projects.
- **Handlers called directly, no MediatR.** One concept less. Without pipeline behaviors, endpoint filters cover cross-cutting needs.
- **Rules in pure classes.** Transitions, permissions, cover resolution and flags live in `Domain/` with no EF and no I/O, so they are cheap to test.
- **The API returns flags and `allowedActions` per item.** The UI shows only allowed actions and holds no rules.
- **Lists computed in memory.** A month has one row per facility, so the API loads it and computes flags, actions and order in C#. No database paging, which is fine up to a few hundred facilities.
- **Result pattern.** Handlers return `Result` and endpoints map it to HTTP. No exceptions for business rules.

## Project structure
```
src/Rcm.Api/
  Program.cs
  Domain/       entities, enums, Result, WorkflowRules, CoverRules, ItemFlags
  Data/         AppDbContext, model configuration, SeedData
  Auth/         DemoAuthHandler, CurrentUser
  Features/
    Demo/       sign-in list and reset (Development only)
    Me/
    Users/
    Cycles/
    WorkItems/
    Absences/
    Facilities/
tests/Rcm.Api.Tests/
  Domain/       rule tests, no database
  Features/     handler tests on SQLite in-memory
web/src/app/
  pages/        sign-in, my-work, overview, item, absences
  components/   item table, status badge, flags, dialogs
  services/     API clients, session
  models/       types for API responses
```

## API
| Method | Path | Who | Purpose |
|---|---|---|---|
| GET | `/api/demo/users` | anyone, Development only | Sign-in picker |
| POST | `/api/demo/reset` | anyone, Development only | Drop and re-seed the database |
| GET | `/api/me` | signed in | Current user and who I cover today |
| GET | `/api/users` | signed in | Active users for pickers |
| GET | `/api/cycles` | signed in | Months, newest first |
| POST | `/api/cycles` | manager | Open the month after the latest one. Body: `dueDate` |
| GET | `/api/cycles/{id}/close-preview` | manager | Items that would become Incomplete |
| POST | `/api/cycles/{id}/close` | manager | Close the month |
| GET | `/api/work-items?cycleId=&view=mine` | signed in | My work rows |
| GET | `/api/work-items?cycleId=&view=overview` | supervisor, manager | Overview rows |
| GET | `/api/work-items/{id}` | can see it | Detail, events, last month's item |
| POST | `/api/work-items/{id}/transitions` | per [02](02-workflow.md#transitions) | Body: `action`, `text`, `version` |
| POST | `/api/work-items/{id}/comments` | can see it | Body: `text` |
| GET | `/api/absences` | supervisor, manager | Current and future absences in scope |
| POST | `/api/absences` | supervisor for self and team, manager | Create |
| PUT | `/api/absences/{id}` | same | Change dates, cover or note |
| DELETE | `/api/absences/{id}` | same | Only before it starts |
| GET | `/api/facilities` | supervisor, manager | Facilities with current owner |
| PUT | `/api/facilities/{id}/owner` | manager | Body: `newOwnerId`, `reason`, `moveOpenItem` |

- A row carries owner, reviewer, team (the owner's supervisor, or the owner if a supervisor), status, flags, balances in cents, `allowedActions`, `awaitingMyReview` and `version`.
- `cycleId` defaults to the open month, or the latest month if none is open.
- Errors are ProblemDetails: 400 rule or validation, 401 not signed in, 403 not allowed, 404 missing or not visible to the user, 409 stale `version`.

## Auth

### Prototype
- The sign-in page lists users from `/api/demo/users` and keeps the choice in localStorage.
- An Angular interceptor sends `X-Demo-User: <id>` on every call.
- `DemoAuthHandler` loads that user, returns 401 for unknown or inactive ids, and issues claims: `user_id`, name, role.
- The handler and `/api/demo/*` are registered only in the Development environment. In any other environment the API exits at startup with a message, because without a sign-in every request would fail.
- Authorization is real: role checks on endpoint groups, item checks in the rules. Swapping authentication does not touch them.
- There is no real identity. Anyone can be anyone, which is fine for fake data.

### Production
- Assumed: Windows Authentication on IIS. Confirm with the team that runs the existing app.
- An `IClaimsTransformation` maps the Windows login to a User row through a new `User.LoginName` column and adds the same `user_id` and role claims. Unknown or inactive logins get 403.
- Roles and reporting lines stay in the app's database, because the workflow needs the reporting lines anyway. Until an AD or HR sync exists, roles are maintained in two places.
- The browser sends Windows credentials on its own, so state-changing calls need CSRF protection: ASP.NET Core antiforgery with Angular's built-in XSRF cookie and header.
- If the existing app uses Entra ID instead, only the authentication registration changes.

## Run locally
- Prerequisites: .NET 10 SDK and Node.js 24.15 or later (Angular 22 also accepts 22.22.3 or later). No database server, no Docker.
- API on http://localhost:5080, HTTP only in Development so no dev certificate is needed.
- The Angular dev server on http://localhost:4200 proxies `/api` to the API. No CORS setup.
- SQLite file `rcm.db` in the API project folder. The path comes from the content root, not the working directory.
- On start the API runs `EnsureCreated` and seeds an empty database. To start over, use "Reset demo data" on the sign-in page, or stop the API and delete the file.
- `global.json` pins the .NET 10 SDK and allows newer feature bands.
- Commands are in [CLAUDE.md](../CLAUDE.md).
- Decision: `EnsureCreated` instead of migrations. The prototype has no migration history; production generates SQL Server migrations from the same model.

## Seed data
All fake. First names only, numbered facilities.
- Managers: Morgan and Quinn.
- Supervisors: Sam and Taylor report to Morgan. Jordan and Casey report to Quinn.
- Billers: Alex and Blake report to Sam, Charlie to Taylor, Dana to Jordan, Emery to Casey.
- Facilities F01 to F20, named "Facility 01" to "Facility 20". Alex owns F01 to F04, Blake F05 to F08, Charlie F09 to F12, Dana F13 to F15, Emery F16 to F18. Casey, a supervisor, owns F19 and F20.
- Last month: closed. 18 Approved, 2 Incomplete (F07 and F14).
- This month: open, due yesterday (or the 1st if today is the 1st), so the demo shows the catch-up phase. Every open status and Approved appear at least once. Alex has a Returned and a Blocked item. Charlie and Casey each have a Submitted item.
- Absences, relative to today: Blake from 2 days ago to 4 days ahead, covered by Alex. Dana from today to 2 days ahead, no cover. On the 1st her absence starts a day later: her seeded events all fall on that day, and with no cover she cannot act while away. Taylor from yesterday to 3 days ahead, covered by Sam.
- Balances: fixed per facility, so every reset shows the same numbers. Total AR from $150,000 to $1,200,000. AR over 90 from 10% to 45% of the total.
- Every seeded item has an event trail that matches its status.

## Production changes
- **SQL Server.** Add the SQL Server provider and read the connection string from config. Generate an initial migration and deploy it as an idempotent script. Use an `rcm` schema if the tables share the existing database. SQL Server compares text case-insensitively by default and SQLite does not, so no rule may depend on case. Cents and the int `Version` stay as they are.
- **Sign-in.** See Auth, Production.
- **Hosting on IIS.** The API serves the built Angular app from `wwwroot` with a fallback to `index.html`. One site, same origin, no CORS. The UI cannot be deployed on its own.
- **Balance import.** A job reads the billing system's month-end aged trial balance (file drop or API). It sums per facility in memory, matches on `Facility.Code`, and writes cents to the open month's items. Each run is logged with who, when, file, row count and errors. A re-run replaces the numbers. An unknown facility code fails the run instead of being skipped. Account rows are never stored, but the job still handles PHI in memory, so it runs on the server, logs no row data and deletes the file when done.
- **Logging.** Structured logs. Never log comment or summary text, because it may contain PHI.
- **Users and org chart.** Admin screens first, then AD or HR sync.
- **Operations.** Health check endpoint, database backups, the existing monitoring.

## Test plan
- **Rule tests** (`Domain/`, no database): every row of the transition table, allowed and denied. Reviewer and cover resolution. Flags.
- **Handler tests** (SQLite in-memory): opening and closing months, absences, facility moves, transitions with version and visibility checks.
- **Smoke test by hand** after the build:
  1. Alex: Blake's items show "Covering for Blake". Start one item, block one, submit one.
  2. Sam: approve Alex's submission and return another with a comment. Approve Charlie's item as Taylor's cover.
  3. Jordan: Dana's items show Needs cover. Edit Dana's absence and set a cover.
  4. Casey: the seeded Submitted item has no Approve button. Quinn: approve it from Waiting for my review.
  5. Morgan: move a facility with its open item. The item history shows the move.
  6. Morgan: close the month after checking the preview, then open the next one. Facilities left unapproved show Missed last month.
  7. Sign-in page: Reset demo data. The seeded month is back as it started.
- **Checked by hand only**: demo reset (smoke step 7). It drops and recreates the database file, which the in-memory test database cannot do.
- **Not tested in v1**: the Angular UI (it holds no rules), SQL Server, real sign-in.

### Edge cases
| Case | Expected | Test |
|---|---|---|
| Supervisor covers their own biller and submits | The supervisor cannot approve. Any manager can | Rule |
| Item owned by a supervisor | The reviewer is their manager | Rule |
| Owner reports to no one | Any other manager reviews | Rule |
| Cover is also away that day | No chain. Needs cover | Rule |
| Last day of an absence | Cover still active. Owner alone again the next day | Rule |
| Overdue boundary | Due today: not overdue. Due yesterday: overdue. Business time zone, not server time | Rule |
| Block, Submit or Return without text | 400 | Rule |
| Any action in a closed month | 400 | Rule |
| Supervisor takes an owner action on a team item without covering | 403 | Rule |
| Item the user cannot see | 404 on read and on actions | Handler |
| Owner and cover act at the same time | The second gets 409 | Handler |
| Biller as cover for a supervisor | Rejected | Handler |
| Overlapping absence, self as cover, inactive cover, cover away | Rejected | Handler |
| Delete an absence that has started | Rejected | Handler |
| Open a month while one is open, or one that exists | Rejected | Handler |
| Inactive facility | No item when a month opens | Handler |
| Close with Submitted items | They become Incomplete | Handler |
| Move with "also move open item" off | Old owner finishes this month. Next month goes to the new owner | Handler |
| Move with a Submitted item | The item moves. The new owner's reviewer reviews it | Handler |
| Approved or Incomplete item during a move | Never moves | Handler |

Known risk, not testable: people may type resident names into comments. Mitigated by the UI warning and the logging rule above.
