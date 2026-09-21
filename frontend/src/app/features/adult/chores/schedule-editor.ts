import { Component, OnInit, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AdultPrimaryButton } from '../ui/buttons';
import { ChoreRecurrenceFrequency } from './chore-recurrence.models';
import { WEEKDAY_BITS } from './chore-recurrence-label';
import { toIsoDate } from './chore-recurrence-schedule';

export type ScheduleRepeat = 'Once' | ChoreRecurrenceFrequency;

export interface ScheduleValue {
  repeat: ScheduleRepeat;
  /** Due date of a one-off chore, or the start date of a new recurrence. "YYYY-MM-DD". */
  date: string;
  daysOfWeekMask: number | null;
  dayOfMonth: number | null;
}

const DAYS = [0, 1, 2, 3, 4, 5, 6];
const DAYS_OF_MONTH = Array.from({ length: 31 }, (_, index) => index + 1);

/**
 * Form for choosing how a chore repeats: once on a date, daily, weekly, monthly or on chosen
 * weekdays. Emits the choice; the host decides which endpoint to call.
 */
@Component({
  selector: 'app-schedule-editor',
  imports: [TranslocoPipe, AdultPrimaryButton],
  templateUrl: './schedule-editor.html',
})
export class ScheduleEditor implements OnInit {
  readonly initial = input.required<ScheduleValue>();
  /** False when editing a bare recurrence: it has no single occurrence to turn into a one-off. */
  readonly allowOnce = input(true);
  /** True when turning a one-off into a recurrence, which needs a start date. */
  readonly askStartDate = input(false);
  readonly busy = input(false);
  readonly error = input('');

  readonly save = output<ScheduleValue>();
  readonly cancelled = output<void>();

  protected readonly days = DAYS;
  protected readonly daysOfMonth = DAYS_OF_MONTH;
  protected readonly minDate = toIsoDate(new Date());

  protected readonly repeat = signal<ScheduleRepeat>('Once');
  protected readonly date = signal('');
  protected readonly weekday = signal(0);
  protected readonly customDays = signal<ReadonlySet<number>>(new Set());
  protected readonly dayOfMonth = signal(1);
  protected readonly validationError = signal<'date' | 'days' | null>(null);

  ngOnInit(): void {
    const initial = this.initial();
    this.repeat.set(initial.repeat);
    this.date.set(initial.date);
    const mask = initial.daysOfWeekMask ?? 0;
    const selected = DAYS.filter((day) => (mask & WEEKDAY_BITS[day]) !== 0);
    this.weekday.set(selected[0] ?? 0);
    this.customDays.set(new Set(selected));
    this.dayOfMonth.set(initial.dayOfMonth ?? 1);
  }

  protected showDate(): boolean {
    return this.repeat() === 'Once' || (this.askStartDate() && this.repeat() !== 'Once');
  }

  protected toggleDay(day: number): void {
    this.customDays.update((days) => {
      const next = new Set(days);
      if (next.has(day)) next.delete(day);
      else next.add(day);
      return next;
    });
  }

  protected submit(event: Event): void {
    event.preventDefault();
    if (this.busy()) return;

    const repeat = this.repeat();
    if (this.showDate() && (!this.date() || this.date() < this.minDate)) {
      this.validationError.set('date');
      return;
    }
    if (repeat === 'Custom' && this.customDays().size === 0) {
      this.validationError.set('days');
      return;
    }
    this.validationError.set(null);

    const daysOfWeekMask =
      repeat === 'Weekly'
        ? WEEKDAY_BITS[this.weekday()]
        : repeat === 'Custom'
          ? [...this.customDays()].reduce((mask, day) => mask | WEEKDAY_BITS[day], 0)
          : null;

    this.save.emit({
      repeat,
      date: this.showDate() ? this.date() : this.initial().date,
      daysOfWeekMask,
      dayOfMonth: repeat === 'Monthly' ? this.dayOfMonth() : null,
    });
  }
}
