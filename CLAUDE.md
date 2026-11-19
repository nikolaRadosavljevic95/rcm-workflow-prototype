# CLAUDE.md

Build brief for session 2 of 3 (design, build, review). The docs are the spec. If code and docs disagree, ask.

## What we are building
- An internal app for the monthly review of outstanding accounts across about 20 nursing home facilities.
- One work item per facility per month, with a review step, covers for absences, permanent facility moves, and month open and close.
- Assumptions: docs/01. Rules: docs/02. Model: docs/03. Scope and screens: docs/04. API, auth, seed and tests: docs/05.

## Decisions already made
- Scope is docs/04 "In v1". Do not add to it.
- Fake data only, as listed in docs/05. No patient-level data, not even fake.
- SQLite through EF Core, `EnsureCreated` and seed on start, no migrations.
- Amounts are cents in `long`. Timestamps are UTC `DateTime`. Dates are `DateOnly`. No `decimal` or `DateTimeOffset` in entities.
- The reviewer comes from `ReportsToId` at the time of the action. Never store it.
- Demo sign-in through the `X-Demo-User` header, registered only in Development.
- The API computes flags and `allowedActions`. The UI shows only allowed actions and holds no rules.

## Build order
Stop after each step so it can be reviewed and committed. If time runs short, cut steps from the end. Tests are never cut: a step ships with its tests or not at all.
1. **Skeleton, seed and demo auth.** Projects as in Setup, entities, `AppDbContext`, `SeedData`, demo sign-in, `/api/me`, `/api/users`, the sign-in page, a README with run steps.
2. **Work items with transitions and tests.** Rule classes and their tests, list, detail, transition and comment endpoints, My work, Overview and item pages.
3. **Covers.** Absence endpoints and page, cover resolution in visibility, actions and flags.
4. **Closing and opening a month.** Close preview, close, open next, manager dialogs.
5. **Facility move.** Owner change with assignment history and the open-item option, move dialog.
6. **Only if time is left:** Reopen, then single-item reassignment (docs/04, What's next).

## Conventions
API, `src/Rcm.Api`:
- `Features/<Name>/` holds `<Name>Endpoints.cs` (routes only), the handlers, and their request and response records.
- Endpoints call handlers directly. Handlers use `AppDbContext` directly, without repositories.
- `Domain/` holds entities, enums, `Result` and the pure rule classes. No EF, no I/O.
- `Data/` holds `AppDbContext`, model configuration and `SeedData`.
- Business rules return a `Result` and never throw. An endpoint maps it to ProblemDetails: Invalid 400, Forbidden 403, NotFound 404, Conflict 409.
- async/await for all I/O.
- Inject `TimeProvider`. "Today" uses `Business:TimeZone`, default `America/New_York`.
- Store enums as strings. Bump `WorkItem.Version` on every change.
- Build the SQLite path from the content root.
- Never log comment or summary text.

Web, `web`:
- Angular 22, standalone components, signals, strict TypeScript with no `any`.
- Wrap `HttpClient` calls in `firstValueFrom` and await them. No `subscribe` callbacks.
- `src/app/pages/`, `components/`, `services/`, `models/`.
- Plain CSS in component files and `styles.css`. No inline styles, no CSS framework, no UI library.
- `proxy.conf.json` sends `/api` to http://localhost:5080.

Tests, `tests/Rcm.Api.Tests`:
- Every business rule has a unit test, including every transition row in docs/02 and every edge case in docs/05.
- xUnit with plain `Assert`.
- Rule tests use no database. Handler tests use SQLite in-memory on an open connection.
- Name tests `Method_Condition_Expected`.

Docs:
- Plain English, short sentences, no emojis, no marketing words, no em dashes. A decision says what we chose and what it costs.

## Packages
- API: the `webapi` template's packages and `Microsoft.EntityFrameworkCore.Sqlite`.
- Tests: the `xunit` template's packages. EF Core SQLite comes through the project reference.
- Web: what `ng new` installs.
- Nothing else without approval.

## Setup, once
- Solution at the root with `src/Rcm.Api` (`dotnet new webapi`) and `tests/Rcm.Api.Tests` (`dotnet new xunit`, referencing the API).
- `global.json`: SDK 10.0.100, `rollForward` `latestFeature`.
- `.gitignore` from `dotnet new gitignore`, plus `node_modules/`, `.angular/` and `*.db`.
- Angular app in `web` from `npx @angular/cli@22 new`: CSS, no SSR, no generated tests, no git init.
- `launchSettings.json`: API on http://localhost:5080.

## Commands
From the repo root unless noted.
- Build: `dotnet build`
- Test: `dotnet test`
- API: `dotnet run --project src/Rcm.Api`
- Web, in `web`: `npm install`, then `npm start`
- Reset data: "Reset demo data" on the sign-in page, or stop the API and delete `src/Rcm.Api/rcm.db`.

## Done for session 2
- `dotnet build` has no warnings and `dotnet test` passes.
- A fresh clone runs with the commands above and only the prerequisites in docs/05.
- The smoke test in docs/05 passes by hand for every step that was built.
- README.md covers what it is, prerequisites, how to run, demo users and what v1 leaves out.
- No real names and no PHI anywhere in the repo.
