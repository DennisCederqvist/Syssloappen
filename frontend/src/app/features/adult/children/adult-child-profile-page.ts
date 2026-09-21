import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { finalize, forkJoin } from 'rxjs';
import { AdultApprovalCard } from '../ui/approval-card';
import { AdultBadge } from '../ui/badge';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultDangerOutlineButton, AdultPrimaryButton } from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { AdultSheet } from '../ui/sheet';
import { ChoreRecurrence } from '../chores/chore-recurrence.models';
import { recurrenceScheduleLabel } from '../chores/chore-recurrence-label';
import {
  nextOccurrenceDate,
  parseIsoDate,
  toIsoDate,
} from '../chores/chore-recurrence-schedule';
import { ScheduleEditor, ScheduleValue } from '../chores/schedule-editor';
import { AdultAssignment, Chore } from '../chores/chores.models';
import { ChoresService } from '../chores/chores.service';
import { AdultRewardRedemption, RewardRedemptionsService } from '../rewards/reward-redemptions.service';
import { ChildSummary } from './children.models';
import { ChildrenService } from './children.service';

/** Something the adult has handed out that is not due yet: a dated one-off, or a schedule's next turn. */
interface FutureItem {
  key: string;
  title: string;
  date: string;
  assignment: AdultAssignment | null;
  recurrence: ChoreRecurrence | null;
}

@Component({
  selector: 'app-adult-child-profile-page',
  imports: [
    AdultApprovalCard,
    AdultBadge,
    AdultBottomNav,
    AdultDangerOutlineButton,
    AdultPrimaryButton,
    AdultPageHeader,
    AdultSheet,
    ScheduleEditor,
    TranslocoPipe,
  ],
  templateUrl: './adult-child-profile-page.html',
})
export class AdultChildProfilePage {
  private readonly route = inject(ActivatedRoute);
  private readonly childrenService = inject(ChildrenService);
  private readonly choresService = inject(ChoresService);
  private readonly rewardRedemptionsService = inject(RewardRedemptionsService);
  private readonly transloco = inject(TranslocoService);
  private readonly childId = Number(this.route.snapshot.paramMap.get('childId'));
  readonly child = signal<ChildSummary | null>(null);
  readonly assignments = signal<AdultAssignment[]>([]);
  readonly chores = signal<Chore[]>([]);
  readonly recurrences = signal<ChoreRecurrence[]>([]);
  readonly rewardRedemptions = signal<AdultRewardRedemption[]>([]);
  readonly error = signal('');
  readonly showAssignmentPicker = signal(false);
  readonly busyAssignmentId = signal<number | null>(null);
  readonly busyRewardRedemptionId = signal<number | null>(null);
  readonly assigningChoreId = signal<number | null>(null);
  readonly rejectingAssignmentId = signal<number | null>(null);
  readonly rejectComments = signal<Readonly<Record<number, string>>>({});
  readonly selectedAssignment = signal<AdultAssignment | null>(null);
  readonly confirmingAssignmentRemovalId = signal<number | null>(null);
  readonly removingAssignmentId = signal<number | null>(null);
  readonly assignmentRemovalError = signal('');
  readonly selectedRecurrence = signal<ChoreRecurrence | null>(null);
  readonly editorInitial = signal<ScheduleValue | null>(null);
  readonly savingSchedule = signal(false);
  readonly scheduleError = signal('');
  readonly activeAssignments = computed(() => {
    const today = toIsoDate(new Date());
    return this.assignments().filter(
      (item) =>
        (item.status === 'Assigned' || item.status === 'NeedsRedo') && item.dueDate <= today,
    );
  });
  readonly futureItems = computed<FutureItem[]>(() => {
    const today = toIsoDate(new Date());
    const assignments = this.assignments();

    const dated = assignments
      .filter((item) => item.status === 'Assigned' && item.dueDate > today)
      .map((item) => ({
        key: `assignment-${item.assignmentId}`,
        title: item.choreTitle,
        date: item.dueDate,
        assignment: item,
        recurrence: null,
      }));

    // A schedule that already has an open occurrence is shown through that occurrence instead.
    const withOpenOccurrence = new Set(
      assignments
        .filter(
          (item) =>
            (item.status === 'Assigned' || item.status === 'NeedsRedo') &&
            item.generatedFromRecurrenceId !== null,
        )
        .map((item) => item.generatedFromRecurrenceId),
    );
    const upcoming = this.recurrences()
      .filter((item) => item.childId === this.childId && !withOpenOccurrence.has(item.id))
      .flatMap((item) => {
        const date = nextOccurrenceDate(item, today);
        return date
          ? [{ key: `recurrence-${item.id}`, title: item.choreTitle, date, assignment: null, recurrence: item }]
          : [];
      });

    return [...dated, ...upcoming].sort((a, b) => a.date.localeCompare(b.date));
  });
  readonly pendingAssignments = computed(() =>
    this.assignments().filter((item) => item.status === 'PendingApproval'),
  );
  readonly recentApprovedAssignments = computed(() =>
    this.assignments()
      .filter((item) => item.status === 'Approved')
      .slice(0, 10),
  );
  readonly activeRewardRedemptions = computed(() =>
    this.rewardRedemptions().filter(
      (item) => item.status === 'Requested' || item.status === 'Approved',
    ),
  );

  constructor() {
    this.load();
  }

  // "Tue 22 Sep", for compact cards.
  formatShortDate(dueDate: string): string {
    return parseIsoDate(dueDate).toLocaleDateString(this.transloco.getActiveLang(), {
      weekday: 'short',
      day: 'numeric',
      month: 'short',
    });
  }

  futureItemLabel(item: FutureItem): string | null {
    if (item.recurrence) return recurrenceScheduleLabel(item.recurrence, this.transloco);
    return item.assignment ? this.recurrenceLabel(item.assignment) : null;
  }

  openFutureItem(item: FutureItem): void {
    if (item.assignment) {
      this.openAssignmentDetail(item.assignment);
    } else if (item.recurrence) {
      this.closeAssignmentDetail();
      this.resetScheduleEditing();
      this.selectedRecurrence.set(item.recurrence);
    }
  }

  closeRecurrenceDetail(): void {
    this.selectedRecurrence.set(null);
    this.resetScheduleEditing();
    this.confirmingAssignmentRemovalId.set(null);
    this.assignmentRemovalError.set('');
  }

  startEditingAssignment(assignment: AdultAssignment): void {
    const recurrence = this.recurrenceOf(assignment);
    this.scheduleError.set('');
    this.editorInitial.set(
      recurrence
        ? {
            repeat: recurrence.frequency,
            date: assignment.dueDate,
            daysOfWeekMask: recurrence.daysOfWeekMask,
            dayOfMonth: recurrence.dayOfMonth,
          }
        : {
            repeat: 'Once',
            date: assignment.dueDate < toIsoDate(new Date()) ? toIsoDate(new Date()) : assignment.dueDate,
            daysOfWeekMask: null,
            dayOfMonth: null,
          },
    );
  }

  startEditingRecurrence(recurrence: ChoreRecurrence): void {
    this.scheduleError.set('');
    this.editorInitial.set({
      repeat: recurrence.frequency,
      date: recurrence.startDate,
      daysOfWeekMask: recurrence.daysOfWeekMask,
      dayOfMonth: recurrence.dayOfMonth,
    });
  }

  cancelEditingSchedule(): void {
    this.resetScheduleEditing();
  }

  saveAssignmentSchedule(assignment: AdultAssignment, value: ScheduleValue): void {
    if (this.savingSchedule()) return;
    const recurring = value.repeat !== 'Once';
    this.savingSchedule.set(true);
    this.scheduleError.set('');
    this.choresService
      .updateAssignmentSchedule(assignment.assignmentId, {
        frequency: value.repeat === 'Once' ? null : value.repeat,
        daysOfWeekMask: recurring ? value.daysOfWeekMask : null,
        dayOfMonth: recurring ? value.dayOfMonth : null,
        dueDate: value.date || null,
      })
      .pipe(finalize(() => this.savingSchedule.set(false)))
      .subscribe({
        next: () => {
          this.closeAssignmentDetail();
          this.load();
        },
        error: (error: HttpErrorResponse) => this.scheduleError.set(this.scheduleErrorText(error)),
      });
  }

  saveRecurrenceSchedule(recurrence: ChoreRecurrence, value: ScheduleValue): void {
    if (this.savingSchedule() || value.repeat === 'Once') return;
    this.savingSchedule.set(true);
    this.scheduleError.set('');
    this.choresService
      .updateRecurrence(recurrence.id, {
        frequency: value.repeat,
        daysOfWeekMask: value.daysOfWeekMask,
        dayOfMonth: value.dayOfMonth,
      })
      .pipe(finalize(() => this.savingSchedule.set(false)))
      .subscribe({
        next: () => {
          this.closeRecurrenceDetail();
          this.load();
        },
        error: (error: HttpErrorResponse) => this.scheduleError.set(this.scheduleErrorText(error)),
      });
  }

  removeRecurrence(recurrence: ChoreRecurrence): void {
    if (
      this.confirmingAssignmentRemovalId() !== -recurrence.id ||
      this.removingAssignmentId() !== null
    ) {
      return;
    }
    this.removingAssignmentId.set(-recurrence.id);
    this.assignmentRemovalError.set('');
    this.choresService
      .deleteRecurrence(recurrence.id)
      .pipe(finalize(() => this.removingAssignmentId.set(null)))
      .subscribe({
        next: () => {
          this.closeRecurrenceDetail();
          this.load();
        },
        error: (error: HttpErrorResponse) =>
          this.assignmentRemovalError.set(
            this.transloco.translate(
              error.status === 404
                ? 'adult.childProfile.removeSheet.error.notFound'
                : 'adult.childProfile.removeSheet.error.generic',
            ),
          ),
      });
  }

  // "Tuesday 22 September 2026", in the language the app is currently showing.
  formatDueDate(dueDate: string): string {
    const [year, month, day] = dueDate.split('-').map(Number);
    return new Date(year, month - 1, day).toLocaleDateString(this.transloco.getActiveLang(), {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
    });
  }

  // Null for a one-off assignment, or when the schedule that generated it has since been stopped.
  recurrenceLabel(assignment: AdultAssignment): string | null {
    const recurrence = this.recurrenceOf(assignment);
    return recurrence ? recurrenceScheduleLabel(recurrence, this.transloco) : null;
  }

  recurrenceText(recurrence: ChoreRecurrence): string {
    return recurrenceScheduleLabel(recurrence, this.transloco);
  }

  nextDateOf(recurrence: ChoreRecurrence): string | null {
    return nextOccurrenceDate(recurrence, toIsoDate(new Date()));
  }

  recurrenceOf(assignment: AdultAssignment): ChoreRecurrence | null {
    return (
      this.recurrences().find((item) => item.id === assignment.generatedFromRecurrenceId) ?? null
    );
  }

  private resetScheduleEditing(): void {
    this.editorInitial.set(null);
    this.scheduleError.set('');
  }

  private scheduleErrorText(error: HttpErrorResponse): string {
    return this.transloco.translate(
      error.status === 400
        ? 'adult.childProfile.editSheet.error.invalid'
        : error.status === 404
          ? 'adult.childProfile.editSheet.error.notFound'
          : error.status === 409
            ? 'adult.childProfile.editSheet.error.conflict'
            : 'adult.childProfile.editSheet.error.generic',
    );
  }

  toggleAssignmentPicker(): void {
    this.showAssignmentPicker.update((value) => !value);
  }

  approve(item: AdultAssignment): void {
    this.review(item, 'approve', null);
  }

  requestReject(item: AdultAssignment): void {
    if (this.busyAssignmentId() !== null) return;
    this.rejectingAssignmentId.set(item.assignmentId);
  }

  cancelReject(): void {
    this.rejectingAssignmentId.set(null);
  }

  setRejectComment(assignmentId: number, comment: string): void {
    this.rejectComments.update((comments) => ({ ...comments, [assignmentId]: comment }));
  }

  confirmReject(item: AdultAssignment): void {
    const rawComment = this.rejectComments()[item.assignmentId] ?? '';
    this.review(item, 'reject', rawComment.trim() || null);
  }

  changeRewardRedemption(
    item: AdultRewardRedemption,
    action: 'approve' | 'cancel' | 'deliver',
  ): void {
    if (this.busyRewardRedemptionId() !== null) return;
    this.busyRewardRedemptionId.set(item.id);
    this.rewardRedemptionsService.change(item.id, action, null).subscribe({
      next: (updated) =>
        this.rewardRedemptions.update((items) =>
          items.map((current) => (current.id === updated.id ? updated : current)),
        ),
      error: () => this.error.set(this.transloco.translate('adult.home.rewardChangeError')),
      complete: () => this.busyRewardRedemptionId.set(null),
    });
  }

  assign(chore: Chore): void {
    if (this.assigningChoreId() !== null) return;
    this.assigningChoreId.set(chore.id);
    this.choresService
      .createAssignment({
        choreId: chore.id,
        childId: this.childId,
        dueDate: new Date().toLocaleDateString('sv-SE'),
      })
      .subscribe({
        next: () => {
          this.showAssignmentPicker.set(false);
          this.load();
        },
        error: () => {
          this.error.set(this.transloco.translate('adult.chores.assignError.generic'));
          this.assigningChoreId.set(null);
        },
      });
  }

  openAssignmentDetail(item: AdultAssignment): void {
    this.selectedRecurrence.set(null);
    this.resetScheduleEditing();
    this.selectedAssignment.set(item);
    this.confirmingAssignmentRemovalId.set(null);
    this.assignmentRemovalError.set('');
  }

  closeAssignmentDetail(): void {
    this.selectedAssignment.set(null);
    this.resetScheduleEditing();
    this.confirmingAssignmentRemovalId.set(null);
    this.assignmentRemovalError.set('');
  }

  // A bare recurrence is confirmed under the negated id, so it can share the confirmation UI
  // without ever colliding with an assignment id.
  requestRecurrenceRemoval(recurrence: ChoreRecurrence): void {
    this.confirmingAssignmentRemovalId.set(-recurrence.id);
    this.assignmentRemovalError.set('');
  }

  requestAssignmentRemoval(assignmentId: number): void {
    this.confirmingAssignmentRemovalId.set(assignmentId);
    this.assignmentRemovalError.set('');
  }

  cancelAssignmentRemoval(): void {
    this.confirmingAssignmentRemovalId.set(null);
    this.assignmentRemovalError.set('');
  }

  removeAssignment(item: AdultAssignment): void {
    if (
      this.confirmingAssignmentRemovalId() !== item.assignmentId ||
      this.removingAssignmentId() !== null
    ) {
      return;
    }

    this.removingAssignmentId.set(item.assignmentId);
    this.assignmentRemovalError.set('');
    this.choresService
      .cancelAssignment(item.assignmentId)
      .pipe(finalize(() => this.removingAssignmentId.set(null)))
      .subscribe({
        next: () => {
          this.assignments.update((assignments) =>
            assignments.filter((current) => current.assignmentId !== item.assignmentId),
          );
          this.confirmingAssignmentRemovalId.set(null);
          this.closeAssignmentDetail();
        },
        error: (error: HttpErrorResponse) =>
          this.assignmentRemovalError.set(
            this.transloco.translate(
              error.status === 404
                ? 'adult.childProfile.removeSheet.error.notFound'
                : error.status === 409
                  ? 'adult.childProfile.removeSheet.error.conflict'
                  : 'adult.childProfile.removeSheet.error.generic',
            ),
          ),
      });
  }

  private load(): void {
    forkJoin({
      children: this.childrenService.getActiveChildren(),
      assignments: this.choresService.getAssignments(),
      chores: this.choresService.getChores(),
      recurrences: this.choresService.getRecurrences(),
      rewardRedemptions: this.rewardRedemptionsService.get(),
    }).subscribe({
      next: ({ children, assignments, chores, recurrences, rewardRedemptions }) => {
        const child = children.find((item) => item.id === this.childId) ?? null;
        this.child.set(child);
        this.assignments.set(assignments.filter((item) => item.childId === this.childId));
        this.chores.set(chores);
        this.recurrences.set(recurrences);
        this.rewardRedemptions.set(
          rewardRedemptions.filter((item) => item.childId === this.childId),
        );
        this.assigningChoreId.set(null);
        if (!child) this.error.set(this.transloco.translate('adult.childProfile.notFound'));
      },
      error: () => this.error.set(this.transloco.translate('adult.childProfile.loadError')),
    });
  }

  private review(item: AdultAssignment, decision: 'approve' | 'reject', comment: string | null): void {
    if (this.busyAssignmentId() !== null) return;
    this.busyAssignmentId.set(item.assignmentId);
    const request =
      decision === 'approve'
        ? this.choresService.approveAssignment(item.assignmentId, { comment })
        : this.choresService.rejectAssignment(item.assignmentId, { comment });
    request.subscribe({
      next: (reviewed) => {
        this.assignments.update((items) =>
          items.map((current) =>
            current.assignmentId === reviewed.assignmentId
              ? { ...current, status: reviewed.status, reviewedAt: reviewed.reviewedAt }
              : current,
          ),
        );
        if (decision === 'reject') {
          this.rejectingAssignmentId.set(null);
          this.rejectComments.update(({ [item.assignmentId]: _, ...rest }) => rest);
        }
      },
      error: () => this.error.set(this.transloco.translate('adult.home.reviewError')),
      complete: () => this.busyAssignmentId.set(null),
    });
  }
}
