# RCM Monthly Review

An internal app for the monthly review of outstanding accounts across 20 nursing home facilities. Each facility gets one work item per month. The owner reviews it in the billing system, records the result here, and the reviewer approves it or sends it back. Covers handle absences, and a manager opens and closes each month.

This is a prototype with fake data only. There is no patient-level data, not even fake.

Design notes are in [docs/](docs/): assumptions (01), workflow rules (02), data model (03), scope and screens (04), and technical design (05).

## Prerequisites
- .NET 10 SDK
- Node.js 24.15 or later (22.22.3 or later also works)

No database server and no Docker. The API uses a SQLite file.

## Run
From the repo root, start the API on http://localhost:5080:
```
dotnet run --project src/Rcm.Api
```

In a second terminal, start the web app on http://localhost:4200:
```
cd web
npm install
npm start
```

Open http://localhost:4200 and pick a user. The dev server forwards `/api` to the API.

On first start the API creates `src/Rcm.Api/rcm.db` and seeds it. To start over, click "Reset demo data" on the sign-in page, or stop the API and delete the `rcm.db` files.

## Test
```
dotnet build
dotnet test
```

## Demo users
Sign-in is a picker with no password. It exists only in the Development environment.

| User | Role | Reports to | Owns |
|---|---|---|---|
| Morgan | Manager | | |
| Quinn | Manager | | |
| Sam | Supervisor | Morgan | |
| Taylor | Supervisor | Morgan | |
| Jordan | Supervisor | Quinn | |
| Casey | Supervisor | Quinn | Facility 19, 20 |
| Alex | Biller | Sam | Facility 01 to 04 |
| Blake | Biller | Sam | Facility 05 to 08 |
| Charlie | Biller | Taylor | Facility 09 to 12 |
| Dana | Biller | Jordan | Facility 13 to 15 |
| Emery | Biller | Casey | Facility 16 to 18 |

The seed is relative to today. Last month is closed with two facilities Incomplete. This month is open and was due yesterday, so the demo starts in the catch-up phase. Blake and Taylor are away with covers, and Dana is away with no cover.

## Left out of v1
- Reopening an approved item and reassigning a single item. These come next.
- Balance import. Seeded balances show where it plugs in, and months opened in the app show "Not imported".
- A review checklist, email reminders and exports.
- Admin screens for users, reporting lines and facilities.
- Account-level data, because it would make the app hold PHI.

Details and reasons are in [docs/04](docs/04-first-version.md).
