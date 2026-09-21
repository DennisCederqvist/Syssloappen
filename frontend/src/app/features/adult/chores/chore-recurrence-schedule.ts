import { WEEKDAY_BITS, RecurrenceSchedule } from './chore-recurrence-label';

// Calendar dates travel as local "YYYY-MM-DD" strings, matching the backend's DateOnly.

export function toIsoDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

export function parseIsoDate(iso: string): Date {
  const [year, month, day] = iso.split('-').map(Number);
  return new Date(year, month - 1, day);
}

/** Mirrors ChoreRecurrenceGenerator.MatchesSchedule on the backend. */
export function recurrenceMatchesDate(recurrence: RecurrenceSchedule, date: Date): boolean {
  switch (recurrence.frequency) {
    case 'Daily':
      return true;
    case 'Weekly':
    case 'Custom': {
      const mondayFirstIndex = (date.getDay() + 6) % 7;
      return ((recurrence.daysOfWeekMask ?? 0) & WEEKDAY_BITS[mondayFirstIndex]) !== 0;
    }
    case 'Monthly': {
      if (recurrence.dayOfMonth === null) return false;
      const daysInMonth = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
      return date.getDate() === Math.min(recurrence.dayOfMonth, daysInMonth);
    }
  }
}

/**
 * The first date after `today` (and not before the recurrence's start date) on which the
 * recurrence produces a new occurrence, or null if none within a year.
 */
export function nextOccurrenceDate(
  recurrence: RecurrenceSchedule & { startDate: string },
  today: string,
): string | null {
  const candidate = parseIsoDate(today);
  candidate.setDate(candidate.getDate() + 1);
  const start = parseIsoDate(recurrence.startDate);
  if (start > candidate) candidate.setTime(start.getTime());

  for (let i = 0; i < 366; i++) {
    if (recurrenceMatchesDate(recurrence, candidate)) return toIsoDate(candidate);
    candidate.setDate(candidate.getDate() + 1);
  }
  return null;
}
