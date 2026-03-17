---
id: examples-scheduling
title: Scheduling Examples
slug: /docs/examples/scheduling
description: DateRange, TimeSlot, and RecurrencePattern value objects for scheduling domains.
sidebar_position: 20
---

# Scheduling Domain Examples

## `DateRange` — bounded temporal interval

```csharp
using ZeroAlloc.ValueObjects;

[ValueObject]
public partial class DateRange
{
    public DateOnly Start { get; }
    public DateOnly End { get; }

    public DateRange(DateOnly start, DateOnly end)
    {
        if (end < start) throw new ArgumentException("End must be on or after Start");
        Start = start;
        End = end;
    }

    public int Days => End.DayNumber - Start.DayNumber + 1;

    public bool Overlaps(DateRange other) =>
        Start <= other.End && End >= other.Start;

    public bool Contains(DateOnly date) =>
        date >= Start && date <= End;

    public static DateRange ForMonth(int year, int month) =>
        new(new DateOnly(year, month, 1),
            new DateOnly(year, month, DateTime.DaysInMonth(year, month)));

    public static DateRange ForYear(int year) =>
        new(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
}
```

```csharp
var q1 = new DateRange(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
var q2 = new DateRange(new DateOnly(2024, 4, 1), new DateOnly(2024, 6, 30));

q1.Overlaps(q2)   // false — consecutive but not overlapping
q1 == q2          // false — different dates

// Deduplicate booking windows
var booked = new HashSet<DateRange>();
booked.Add(q1);
booked.Add(q1);   // duplicate, not added
// booked.Count == 1

// Find holidays that fall in a range
var holidays = GetHolidays();
var inQ1 = holidays.Where(h => q1.Contains(h.Date)).ToList();
```

---

## `TimeSlot` — meeting or appointment slot (struct)

```csharp
[ValueObject]
public readonly partial struct TimeSlot
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public TimeSlot(TimeOnly start, TimeOnly end)
    {
        if (end <= start) throw new ArgumentException("End must be after Start");
        Start = start;
        End = end;
    }

    public int DurationMinutes => (int)(End - Start).TotalMinutes;

    public bool Overlaps(TimeSlot other) =>
        Start < other.End && End > other.Start;

    public static TimeSlot Hour(int h) =>
        new(new TimeOnly(h, 0), new TimeOnly(h + 1, 0));

    public static TimeSlot HalfHour(int h, int halfHour) =>
        new(new TimeOnly(h, halfHour), new TimeOnly(h, halfHour + 30));
}
```

```csharp
var morningMeeting = TimeSlot.Hour(9);    // 09:00–10:00
var sameMeeting    = TimeSlot.Hour(9);
var afternoonSlot  = TimeSlot.Hour(14);   // 14:00–15:00

morningMeeting == sameMeeting    // true
morningMeeting == afternoonSlot  // false

morningMeeting.Overlaps(afternoonSlot)  // false
morningMeeting.Overlaps(TimeSlot.Hour(9))  // true — same slot

// Availability calendar keyed by time slot
var availability = new Dictionary<TimeSlot, List<string>>();
availability[morningMeeting] = new List<string> { "Alice" };
availability[sameMeeting].Add("Bob");  // same key — adds to the same list
```

---

## `RecurringSchedule` — combining DateRange and TimeSlot

```csharp
[ValueObject]
public partial class RecurringSchedule
{
    [EqualityMember] public DayOfWeek DayOfWeek { get; }
    [EqualityMember] public TimeSlot Slot { get; }

    // Metadata — not part of identity
    public string? RoomId { get; }
    public string? Notes { get; }

    public RecurringSchedule(DayOfWeek day, TimeSlot slot, string? roomId = null, string? notes = null)
    {
        DayOfWeek = day; Slot = slot; RoomId = roomId; Notes = notes;
    }
}
```

```csharp
var mondayMorning = new RecurringSchedule(
    DayOfWeek.Monday,
    TimeSlot.Hour(9),
    roomId: "A101");

var conflict = new RecurringSchedule(
    DayOfWeek.Monday,
    TimeSlot.Hour(9),
    roomId: "B204");  // different room — but same schedule slot

mondayMorning == conflict  // true — DayOfWeek and Slot match; RoomId excluded

// Detect schedule conflicts
var schedules = new HashSet<RecurringSchedule>();
bool hasConflict = !schedules.Add(mondayMorning);  // false — first add
hasConflict = !schedules.Add(conflict);             // true — same slot already present
```
