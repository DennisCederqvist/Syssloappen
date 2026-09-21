import { TranslocoService } from '@jsverse/transloco';
import { ChoreRecurrence } from './chore-recurrence.models';

// Mon=0..Sun=6 in the UI, converted to the backend's Mon=1..Sun=64 bitmask on submit.
export const WEEKDAY_BITS = [1, 2, 4, 8, 16, 32, 64];

export type RecurrenceSchedule = Pick<ChoreRecurrence, 'frequency' | 'daysOfWeekMask' | 'dayOfMonth'>;

export function recurrenceScheduleLabel(
  recurrence: RecurrenceSchedule,
  transloco: TranslocoService,
): string {
  switch (recurrence.frequency) {
    case 'Daily':
      return transloco.translate('adult.chores.recurrence.scheduleDaily');
    case 'Weekly': {
      const dayIndex = WEEKDAY_BITS.indexOf(recurrence.daysOfWeekMask ?? 0);
      return transloco.translate('adult.chores.recurrence.scheduleWeekly', {
        weekday: transloco.translate(`adult.chores.recurrence.weekday.${dayIndex}`),
      });
    }
    case 'Monthly':
      return transloco.translate('adult.chores.recurrence.scheduleMonthly', {
        day: recurrence.dayOfMonth,
      });
    case 'Custom': {
      const mask = recurrence.daysOfWeekMask ?? 0;
      const days = WEEKDAY_BITS.map((bit, index) => (mask & bit ? index : null))
        .filter((index): index is number => index !== null)
        .map((index) => transloco.translate(`adult.chores.recurrence.weekdayShort.${index}`))
        .join(', ');
      return transloco.translate('adult.chores.recurrence.scheduleCustom', { days });
    }
  }
}
