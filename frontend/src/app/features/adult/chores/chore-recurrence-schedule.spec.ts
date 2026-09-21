import { nextOccurrenceDate, recurrenceMatchesDate } from './chore-recurrence-schedule';

// 2026-09-21 is a Monday.
const daily = { frequency: 'Daily' as const, daysOfWeekMask: null, dayOfMonth: null, startDate: '2026-09-01' };

describe('nextOccurrenceDate', () => {
  it('returns tomorrow for a daily recurrence', () => {
    expect(nextOccurrenceDate(daily, '2026-09-21')).toBe('2026-09-22');
  });

  it('finds the next selected weekday, never today itself', () => {
    const mondays = { ...daily, frequency: 'Weekly' as const, daysOfWeekMask: 1 };
    expect(nextOccurrenceDate(mondays, '2026-09-21')).toBe('2026-09-28');
    const fridays = { ...daily, frequency: 'Weekly' as const, daysOfWeekMask: 16 };
    expect(nextOccurrenceDate(fridays, '2026-09-21')).toBe('2026-09-25');
  });

  it('picks the nearest of several custom days', () => {
    const monWedFri = { ...daily, frequency: 'Custom' as const, daysOfWeekMask: 1 | 4 | 16 };
    expect(nextOccurrenceDate(monWedFri, '2026-09-21')).toBe('2026-09-23');
    expect(nextOccurrenceDate(monWedFri, '2026-09-25')).toBe('2026-09-28');
  });

  it('finds the next day of the month, including across a month boundary', () => {
    const first = { ...daily, frequency: 'Monthly' as const, dayOfMonth: 1 };
    expect(nextOccurrenceDate(first, '2026-09-21')).toBe('2026-10-01');
    expect(nextOccurrenceDate(first, '2026-12-31')).toBe('2027-01-01');
  });

  it('clamps a day of month that does not exist to the last day', () => {
    const thirtyFirst = { ...daily, frequency: 'Monthly' as const, dayOfMonth: 31 };
    expect(nextOccurrenceDate(thirtyFirst, '2026-09-21')).toBe('2026-09-30');
    expect(nextOccurrenceDate(thirtyFirst, '2027-01-31')).toBe('2027-02-28');
  });

  it('does not go earlier than the start date', () => {
    const later = { ...daily, startDate: '2026-10-05' };
    expect(nextOccurrenceDate(later, '2026-09-21')).toBe('2026-10-05');
  });
});

describe('recurrenceMatchesDate', () => {
  it('uses Monday-first weekday bits', () => {
    const sundays = { frequency: 'Weekly' as const, daysOfWeekMask: 64, dayOfMonth: null };
    expect(recurrenceMatchesDate(sundays, new Date(2026, 8, 27))).toBe(true);
    expect(recurrenceMatchesDate(sundays, new Date(2026, 8, 21))).toBe(false);
  });
});
