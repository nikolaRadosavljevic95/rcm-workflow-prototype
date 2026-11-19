using Microsoft.EntityFrameworkCore;
using Rcm.Api.Domain;

namespace Rcm.Api.Data;

// Fake demo data, placed relative to the business date: last month closed, this month open and past due.
public static class SeedData
{
    public const int Morgan = 1, Quinn = 2, Sam = 3, Taylor = 4, Jordan = 5, Casey = 6;
    public const int Alex = 7, Blake = 8, Charlie = 9, Dana = 10, Emery = 11;

    private static readonly (int Id, string Name, Role Role, int? ReportsToId)[] People =
    [
        (Morgan, "Morgan", Role.Manager, null),
        (Quinn, "Quinn", Role.Manager, null),
        (Sam, "Sam", Role.Supervisor, Morgan),
        (Taylor, "Taylor", Role.Supervisor, Morgan),
        (Jordan, "Jordan", Role.Supervisor, Quinn),
        (Casey, "Casey", Role.Supervisor, Quinn),
        (Alex, "Alex", Role.Biller, Sam),
        (Blake, "Blake", Role.Biller, Sam),
        (Charlie, "Charlie", Role.Biller, Taylor),
        (Dana, "Dana", Role.Biller, Jordan),
        (Emery, "Emery", Role.Biller, Casey),
    ];

    // Index 0 is F01. Owner, this month's status and balances in whole dollars.
    private static readonly (int OwnerId, WorkItemStatus Status, long ArTotal, long ArOver90)[] Facilities =
    [
        (Alex, WorkItemStatus.InProgress, 520_000, 77_250),
        (Alex, WorkItemStatus.Blocked, 412_300, 98_100),
        (Alex, WorkItemStatus.Returned, 690_500, 212_400),
        (Alex, WorkItemStatus.Submitted, 310_000, 65_000),
        (Blake, WorkItemStatus.Approved, 405_000, 120_700),
        (Blake, WorkItemStatus.Approved, 236_800, 41_900),
        (Blake, WorkItemStatus.NotStarted, 388_000, 143_900),
        (Blake, WorkItemStatus.Approved, 954_200, 187_300),
        (Charlie, WorkItemStatus.Submitted, 1_180_000, 301_600),
        (Charlie, WorkItemStatus.Approved, 162_400, 24_800),
        (Charlie, WorkItemStatus.Approved, 745_900, 268_000),
        (Charlie, WorkItemStatus.Approved, 298_300, 55_100),
        (Dana, WorkItemStatus.Blocked, 873_600, 342_000),
        (Dana, WorkItemStatus.NotStarted, 451_200, 160_300),
        (Dana, WorkItemStatus.InProgress, 205_700, 36_400),
        (Emery, WorkItemStatus.Approved, 612_000, 88_500),
        (Emery, WorkItemStatus.InProgress, 334_900, 120_600),
        (Emery, WorkItemStatus.Approved, 1_046_500, 214_700),
        (Casey, WorkItemStatus.Submitted, 527_800, 171_900),
        (Casey, WorkItemStatus.Approved, 178_600, 21_400),
    ];

    private static readonly int[] IncompleteLastMonth = [7, 14];

    private static readonly string[] Summaries =
    [
        "All 90+ day accounts noted. Two escalated.",
        "Reviewed all open balances. No new write-offs.",
        "Followed up on aged Medicaid claims. Three resubmitted.",
        "Pending list matches the facility report.",
    ];

    private static readonly string[] BlockReasons =
    [
        "Waiting for the facility's Medicaid pending list",
        "Waiting for remittance files from the payer",
    ];

    private const string ReturnComment = "Add the follow-up dates for the two oldest balances.";

    private static readonly TimeOnly OpenTime = new(8, 0);

    public static async Task SeedAsync(AppDbContext db, BusinessCalendar calendar, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var today = calendar.Today;
        var now = calendar.UtcNow;
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        var users = People
            .Select(p => new User
            {
                Id = p.Id,
                Name = p.Name,
                Email = $"{p.Name.ToLowerInvariant()}@example.com",
                Role = p.Role,
                ReportsToId = p.ReportsToId,
            })
            .ToList();
        var reviewerOf = People.ToDictionary(p => p.Id, p => p.ReportsToId);

        var facilities = Facilities
            .Select((f, index) => new Facility
            {
                Id = index + 1,
                Code = $"F{index + 1:00}",
                Name = $"Facility {index + 1:00}",
                OwnerId = f.OwnerId,
            })
            .ToList();

        var assignedAt = calendar.ToUtc(lastMonth.AddMonths(-1), OpenTime);
        var assignments = facilities
            .Select(f => new FacilityAssignment
            {
                FacilityId = f.Id,
                UserId = f.OwnerId,
                StartedAt = assignedAt,
                Reason = "Initial assignment",
                ChangedById = Morgan,
            })
            .ToList();

        // Dana has no cover, so she must not be away when her seeded events happen. On the 1st they all
        // happen that day, after the month opens, so her absence starts on the 2nd instead.
        var danaAwayFrom = today.Day == 1 ? today.AddDays(1) : today;
        var absences = new List<Absence>
        {
            NewAbsence(calendar, Blake, today.AddDays(-2), today.AddDays(4), Alex, "Annual leave", Sam, today.AddDays(-9)),
            NewAbsence(calendar, Dana, danaAwayFrom, danaAwayFrom.AddDays(2), null, "Personal leave", Jordan, today.AddDays(-3)),
            NewAbsence(calendar, Taylor, today.AddDays(-1), today.AddDays(3), Sam, "Training", Morgan, today.AddDays(-6)),
        };

        // This month opens at least an hour before now, so its trails fit even early on the 1st.
        var thisOpenedAt = Min(calendar.ToUtc(thisMonth, OpenTime), now.AddHours(-1));
        var lastClosedAt = thisOpenedAt.AddMinutes(-30);

        var lastCycle = new ReviewCycle
        {
            Year = lastMonth.Year,
            Month = lastMonth.Month,
            DueDate = new DateOnly(lastMonth.Year, lastMonth.Month, 15),
            Status = CycleStatus.Closed,
            OpenedById = Morgan,
            OpenedAt = calendar.ToUtc(lastMonth, OpenTime),
            ClosedById = Morgan,
            ClosedAt = lastClosedAt,
        };
        var thisCycle = new ReviewCycle
        {
            Year = thisMonth.Year,
            Month = thisMonth.Month,
            DueDate = today.Day == 1 ? today : today.AddDays(-1),
            Status = CycleStatus.Open,
            OpenedById = Morgan,
            OpenedAt = thisOpenedAt,
        };

        var trails = new TrailWriter(calendar, absences);
        var items = new List<WorkItem>();
        foreach (var facility in facilities)
        {
            var number = facility.Id;
            var plan = Facilities[number - 1];
            var reviewerId = reviewerOf[plan.OwnerId] ?? Morgan;
            var summary = Summaries[number % Summaries.Length];

            var last = new WorkItem
            {
                Cycle = lastCycle,
                FacilityId = facility.Id,
                AssigneeId = plan.OwnerId,
                DueDate = lastCycle.DueDate,
                ArTotalCents = plan.ArTotal * 96,
                ArOver90Cents = plan.ArOver90 * 104,
            };
            DateTime OnDay(int day, int hour, int minute) =>
                calendar.ToUtc(lastMonth.AddDays(day - 1), new TimeOnly(hour, minute)).AddMinutes(number);
            List<Step> lastSteps = IncompleteLastMonth.Contains(number)
                ?
                [
                    new(WorkItemStatus.InProgress, plan.OwnerId, OnDay(4, 9, 30)),
                    new(WorkItemStatus.Incomplete, Morgan, lastClosedAt),
                ]
                :
                [
                    new(WorkItemStatus.InProgress, plan.OwnerId, OnDay(3, 9, 0)),
                    new(WorkItemStatus.Submitted, plan.OwnerId, OnDay(10, 15, 0), summary),
                    new(WorkItemStatus.Approved, reviewerId, OnDay(12, 10, 0)),
                ];
            trails.Write(last, lastCycle.OpenedAt, lastClosedAt, lastSteps);

            var current = new WorkItem
            {
                Cycle = thisCycle,
                FacilityId = facility.Id,
                AssigneeId = plan.OwnerId,
                DueDate = thisCycle.DueDate,
                PreviousItem = last,
                ArTotalCents = plan.ArTotal * 100,
                ArOver90Cents = plan.ArOver90 * 100,
            };
            DateTime DaysAgo(int days, int hour, int minute) =>
                calendar.ToUtc(today.AddDays(-days), new TimeOnly(hour, minute)).AddMinutes(number);
            List<Step> currentSteps = plan.Status switch
            {
                WorkItemStatus.InProgress =>
                [
                    new(WorkItemStatus.InProgress, plan.OwnerId, DaysAgo(5, 10, 0)),
                ],
                WorkItemStatus.Blocked =>
                [
                    new(WorkItemStatus.InProgress, plan.OwnerId, DaysAgo(7, 9, 15)),
                    new(WorkItemStatus.Blocked, plan.OwnerId, DaysAgo(2, 10, 2), BlockReasons[number % BlockReasons.Length]),
                ],
                WorkItemStatus.Submitted =>
                [
                    new(WorkItemStatus.InProgress, plan.OwnerId, DaysAgo(8, 9, 30)),
                    new(WorkItemStatus.Submitted, plan.OwnerId, DaysAgo(1, 15, 10), summary),
                ],
                WorkItemStatus.Returned =>
                [
                    new(WorkItemStatus.InProgress, plan.OwnerId, DaysAgo(9, 9, 0)),
                    new(WorkItemStatus.Submitted, plan.OwnerId, DaysAgo(4, 14, 0), summary),
                    new(WorkItemStatus.Returned, reviewerId, DaysAgo(3, 11, 20), ReturnComment),
                ],
                WorkItemStatus.Approved =>
                [
                    new(WorkItemStatus.InProgress, plan.OwnerId, DaysAgo(12, 9, 0)),
                    new(WorkItemStatus.Submitted, plan.OwnerId, DaysAgo(8, 16, 0), summary),
                    new(WorkItemStatus.Approved, reviewerId, DaysAgo(6, 10, 30)),
                ],
                _ => [],
            };
            trails.Write(current, thisOpenedAt, now, currentSteps);

            items.Add(last);
            items.Add(current);
        }

        db.Users.AddRange(users);
        db.Facilities.AddRange(facilities);
        db.FacilityAssignments.AddRange(assignments);
        db.Absences.AddRange(absences);
        db.ReviewCycles.AddRange(lastCycle, thisCycle);
        db.WorkItems.AddRange(items);
        await db.SaveChangesAsync(ct);
    }

    private static Absence NewAbsence(
        BusinessCalendar calendar, int userId, DateOnly start, DateOnly end, int? coverId, string note, int createdById, DateOnly createdOn) =>
        new()
        {
            UserId = userId,
            StartDate = start,
            EndDate = end,
            CoverUserId = coverId,
            Note = note,
            CreatedById = createdById,
            CreatedAt = calendar.ToUtc(createdOn, new TimeOnly(11, 0)),
        };

    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;

    private sealed record Step(WorkItemStatus To, int ActorId, DateTime DesiredAt, string? Text = null);

    private sealed class TrailWriter(BusinessCalendar calendar, IReadOnlyList<Absence> absences)
    {
        // Writes Created plus one StatusChanged per step and sets the item fields to match.
        // Times keep their order and stay within [from, to], one minute apart at the edges.
        public void Write(WorkItem item, DateTime from, DateTime to, IReadOnlyList<Step> steps)
        {
            item.Status = WorkItemStatus.NotStarted;
            item.Events.Add(new WorkItemEvent
            {
                Type = WorkItemEventType.Created,
                ToStatus = WorkItemStatus.NotStarted,
                ToUserId = item.AssigneeId,
                ActorId = Morgan,
                CreatedAt = from,
            });

            var previous = from;
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var earliest = from.AddMinutes(i + 1);
                var latest = to.AddMinutes(-(steps.Count - 1 - i));
                var at = step.DesiredAt < earliest ? earliest : step.DesiredAt > latest ? latest : step.DesiredAt;
                if (at <= previous)
                {
                    at = previous.AddMinutes(1);
                }

                var (actorId, onBehalfOfId) = ResolveActor(step.ActorId, at);
                item.Events.Add(new WorkItemEvent
                {
                    Type = WorkItemEventType.StatusChanged,
                    FromStatus = item.Status,
                    ToStatus = step.To,
                    ActorId = actorId,
                    OnBehalfOfId = onBehalfOfId,
                    Text = step.Text,
                    CreatedAt = at,
                });

                switch (step.To)
                {
                    case WorkItemStatus.Blocked:
                        item.BlockedReason = step.Text;
                        break;
                    case WorkItemStatus.Submitted:
                        item.Summary = step.Text;
                        item.SubmittedById = actorId;
                        item.SubmittedAt = at;
                        break;
                    case WorkItemStatus.Approved:
                        item.ApprovedById = actorId;
                        item.ApprovedAt = at;
                        break;
                }

                item.Status = step.To;
                item.Version++;
                previous = at;
            }
        }

        // Someone away with a cover acts through the cover on that date.
        private (int ActorId, int? OnBehalfOfId) ResolveActor(int userId, DateTime at)
        {
            var date = calendar.DateOf(at);
            var cover = absences
                .Where(a => a.UserId == userId && a.StartDate <= date && date <= a.EndDate)
                .Select(a => a.CoverUserId)
                .FirstOrDefault();
            return cover is { } coverId ? (coverId, userId) : (userId, null);
        }
    }
}
