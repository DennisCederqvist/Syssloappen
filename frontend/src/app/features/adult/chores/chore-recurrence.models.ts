export type ChoreRecurrenceFrequency = 'Daily' | 'Weekly' | 'Monthly' | 'Custom';

export interface CreateChoreRecurrenceRequest {
  choreId: number;
  childId: number;
  frequency: ChoreRecurrenceFrequency;
  daysOfWeekMask?: number | null;
  dayOfMonth?: number | null;
  startDate?: string | null;
}

export interface UpdateChoreRecurrenceRequest {
  frequency: ChoreRecurrenceFrequency;
  daysOfWeekMask: number | null;
  dayOfMonth: number | null;
}

/** A null frequency makes the assignment a one-off chore on `dueDate`. */
export interface UpdateAssignmentScheduleRequest {
  frequency: ChoreRecurrenceFrequency | null;
  daysOfWeekMask: number | null;
  dayOfMonth: number | null;
  dueDate: string | null;
}

export interface ChoreRecurrence {
  id: number;
  choreId: number;
  choreTitle: string;
  childId: number;
  childName: string;
  frequency: ChoreRecurrenceFrequency;
  daysOfWeekMask: number | null;
  dayOfMonth: number | null;
  startDate: string;
}
