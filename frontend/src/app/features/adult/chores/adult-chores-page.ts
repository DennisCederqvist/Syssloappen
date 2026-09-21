import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { catchError, finalize, forkJoin, Observable, of, switchMap } from 'rxjs';
import { focusAfterRender } from '../../../shared/focus';
import { SuccessMessage } from '../../../shared/success-message';
import { ChildSummary } from '../children/children.models';
import { ChildrenService } from '../children/children.service';
import { AdultBadge } from '../ui/badge';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultImagePicker } from '../ui/image-picker';
import {
  AdultDangerOutlineButton,
  AdultPrimaryButton,
  AdultSecondaryTintButton,
} from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { AdultSheet } from '../ui/sheet';
import { AdultTile } from '../ui/tile';
import {
  ChoreRecurrence,
  ChoreRecurrenceFrequency,
  CreateChoreRecurrenceRequest,
} from './chore-recurrence.models';
import { WEEKDAY_BITS, recurrenceScheduleLabel } from './chore-recurrence-label';
import { AdultAssignment, Chore } from './chores.models';
import { ChoresService } from './chores.service';

@Component({
  selector: 'app-adult-chores-page',
  imports: [
    ReactiveFormsModule,
    FormsModule,
    AdultBadge,
    AdultBottomNav,
    AdultImagePicker,
    AdultDangerOutlineButton,
    AdultPrimaryButton,
    AdultSecondaryTintButton,
    AdultPageHeader,
    AdultSheet,
    AdultTile,
    TranslocoPipe,
  ],
  templateUrl: './adult-chores-page.html',
})
export class AdultChoresPage implements OnInit {
  private readonly choresService = inject(ChoresService);
  private readonly transloco = inject(TranslocoService);
  private readonly success = new SuccessMessage();
  private readonly childrenService = inject(ChildrenService);
  private readonly formBuilder = inject(FormBuilder);

  readonly chores = signal<Chore[]>([]);
  readonly children = signal<ChildSummary[]>([]);
  readonly assignments = signal<AdultAssignment[]>([]);
  readonly recurrences = signal<ChoreRecurrence[]>([]);
  readonly isRecurring = signal(false);
  readonly recurrenceFrequency = signal<ChoreRecurrenceFrequency>('Daily');
  readonly recurrenceWeekday = signal(0);
  readonly recurrenceCustomDays = signal<ReadonlySet<number>>(new Set());
  readonly recurrenceDayOfMonth = signal(1);
  readonly stoppingRecurrenceId = signal<number | null>(null);
  readonly recurrenceStopError = signal('');
  readonly isLoading = signal(true);
  readonly loadError = signal('');
  readonly showChoreForm = signal(false);
  readonly showAssignmentForm = signal(false);
  readonly editingChore = signal<Chore | null>(null);
  readonly isCreatingChore = signal(false);
  readonly isUpdatingChore = signal(false);
  readonly deactivatingChoreId = signal<number | null>(null);
  readonly confirmingDeactivationId = signal<number | null>(null);
  readonly isAssigning = signal(false);
  readonly choreError = signal('');
  readonly imageError = signal('');
  readonly pendingImageFile = signal<File | null>(null);
  readonly imagePreviewUrl = signal<string | null>(null);
  readonly editChoreError = signal('');
  readonly deactivationError = signal('');
  readonly assignmentError = signal('');
  readonly successMessage = this.success.message;
  readonly successFading = this.success.fading;
  private assignmentReturnFocusId = 'open-assignment-trigger';
  private editChoreReturnFocusId = '';

  readonly choreForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
    points: [5, [Validators.required, Validators.min(1), Validators.pattern(/^[0-9]+$/)]],
  });

  readonly assignmentForm = this.formBuilder.nonNullable.group({
    choreId: [0, Validators.min(1)],
    childId: [0, Validators.min(1)],
    dueDate: [this.todayInputValue(), Validators.required],
  });

  readonly editChoreForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
    points: [5, [Validators.required, Validators.min(1), Validators.pattern(/^[0-9]+$/)]],
  });

  ngOnInit(): void {
    this.loadPage();
  }

  loadPage(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    forkJoin({
      chores: this.choresService.getChores(),
      children: this.childrenService.getActiveChildren(),
      assignments: this.choresService.getAssignments(),
      recurrences: this.choresService.getRecurrences(),
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ chores, children, assignments, recurrences }) => {
          this.chores.set(chores);
          this.children.set(children);
          this.assignments.set(assignments);
          this.recurrences.set(recurrences);
        },
        error: () => this.loadError.set(this.transloco.translate('adult.chores.loadError')),
      });
  }

  openChoreForm(): void {
    this.choreError.set('');
    this.success.clear();
    this.showChoreForm.set(true);
    focusAfterRender('new-chore-panel');
  }

  closeChoreForm(): void {
    this.showChoreForm.set(false);
    this.choreError.set('');
    this.choreForm.reset({ title: '', description: '', points: 5 });
    this.resetImageState(null);
    focusAfterRender('new-chore-trigger');
  }

  createChore(): void {
    if (this.choreForm.invalid) {
      this.choreForm.markAllAsTouched();
      focusAfterRender(
        this.choreForm.controls.title.invalid ? 'new-chore-name' : 'new-chore-description',
      );
      return;
    }
    const value = this.choreForm.getRawValue();
    const title = value.title.trim();
    if (!title) {
      this.choreForm.controls.title.setErrors({ required: true });
      this.choreForm.controls.title.markAsTouched();
      return;
    }
    this.isCreatingChore.set(true);
    this.choreError.set('');
    this.choresService
      .createChore({
        title,
        description: value.description.trim() || null,
        points: value.points,
      })
      .pipe(
        switchMap((chore) => this.uploadPendingImage(chore)),
        finalize(() => this.isCreatingChore.set(false)),
      )
      .subscribe({
        next: (chore) => {
          this.chores.update((chores) =>
            [...chores, chore].sort((a, b) => a.title.localeCompare(b.title, 'sv')),
          );
          this.closeChoreForm();
          this.success.show(
            this.transloco.translate('adult.chores.createSuccess', { title: chore.title }),
          );
          this.openAssignmentForm(chore.id);
        },
        error: (error: HttpErrorResponse) =>
          this.choreError.set(
            this.transloco.translate(
              error.status === 400
                ? 'adult.chores.createError.validation'
                : 'adult.chores.createError.generic',
            ),
          ),
      });
  }

  openEditChore(chore: Chore): void {
    this.editChoreReturnFocusId = `edit-chore-${chore.id}`;
    this.editingChore.set(chore);
    this.editChoreForm.setValue({
      title: chore.title,
      description: chore.description ?? '',
      points: chore.points,
    });
    this.resetImageState(chore.imageUrl);
    this.editChoreError.set('');
    this.deactivationError.set('');
    this.confirmingDeactivationId.set(null);
    focusAfterRender('edit-chore-panel');
  }

  closeEditChore(): void {
    this.editingChore.set(null);
    this.editChoreForm.reset({ title: '', description: '', points: 5 });
    this.resetImageState(null);
    this.editChoreError.set('');
    if (this.editChoreReturnFocusId) focusAfterRender(this.editChoreReturnFocusId);
  }

  updateChore(): void {
    const chore = this.editingChore();
    if (!chore || this.editChoreForm.invalid) {
      this.editChoreForm.markAllAsTouched();
      focusAfterRender(
        this.editChoreForm.controls.title.invalid ? 'edit-chore-name' : 'edit-chore-description',
      );
      return;
    }
    const value = this.editChoreForm.getRawValue();
    const title = value.title.trim();
    if (!title) {
      this.editChoreForm.controls.title.setErrors({ required: true });
      this.editChoreForm.controls.title.markAsTouched();
      return;
    }
    this.isUpdatingChore.set(true);
    this.editChoreError.set('');
    this.choresService
      .updateChore(chore.id, {
        title,
        description: value.description.trim() || null,
        points: value.points,
      })
      .pipe(
        switchMap((updated) => this.uploadPendingImage(updated)),
        finalize(() => this.isUpdatingChore.set(false)),
      )
      .subscribe({
        next: (updated) => {
          this.chores.update((chores) =>
            chores
              .map((item) => (item.id === updated.id ? updated : item))
              .sort((a, b) => a.title.localeCompare(b.title, 'sv')),
          );
          this.closeEditChore();
          this.success.show(
            this.transloco.translate('adult.chores.updateSuccess', { title: updated.title }),
          );
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.editChoreError.set(
            this.transloco.translate(
              error.status === 404
                ? 'adult.chores.updateError.notFound'
                : error.status === 400
                  ? 'adult.chores.updateError.validation'
                  : 'adult.chores.updateError.generic',
            ),
          ),
      });
  }

  selectImage(file: File): void {
    this.resetImageState(null);
    this.pendingImageFile.set(file);
    this.imagePreviewUrl.set(URL.createObjectURL(file));
  }

  /** Clears a picked-but-unsent file, or removes the chore's saved image when editing. */
  removeImage(): void {
    if (this.pendingImageFile()) {
      this.resetImageState(this.editingChore()?.imageUrl ?? null);
      return;
    }
    const chore = this.editingChore();
    if (!chore) return;
    this.choresService.deleteChoreImage(chore.id).subscribe({
      next: (updated) => {
        this.chores.update((chores) => chores.map((item) => (item.id === updated.id ? updated : item)));
        this.editingChore.set(updated);
        this.imagePreviewUrl.set(null);
      },
      error: () => this.editChoreError.set(this.transloco.translate('adult.chores.updateError.generic')),
    });
  }

  private resetImageState(existingImageUrl: string | null): void {
    const preview = this.imagePreviewUrl();
    if (this.pendingImageFile() && preview) URL.revokeObjectURL(preview);
    this.pendingImageFile.set(null);
    this.imagePreviewUrl.set(existingImageUrl);
  }

  // A failed upload must not undo the chore that was just saved, so it is reported
  // separately and the chore is returned as saved (without its image).
  private uploadPendingImage(chore: Chore): Observable<Chore> {
    const file = this.pendingImageFile();
    this.imageError.set('');
    if (!file) return of(chore);
    return this.choresService.uploadChoreImage(chore.id, file).pipe(
      catchError(() => {
        this.imageError.set(this.transloco.translate('adult.chores.createSheet.imageUploadError'));
        return of(chore);
      }),
    );
  }

  requestDeactivation(choreId: number): void {
    this.confirmingDeactivationId.set(choreId);
    this.deactivationError.set('');
    this.success.clear();
    focusAfterRender(`cancel-chore-deactivation-${choreId}`);
  }

  cancelDeactivation(): void {
    const choreId = this.confirmingDeactivationId();
    this.confirmingDeactivationId.set(null);
    this.deactivationError.set('');
    if (choreId) focusAfterRender(`deactivate-chore-${choreId}`);
  }

  deactivateChore(chore: Chore): void {
    if (this.confirmingDeactivationId() !== chore.id || this.deactivatingChoreId() !== null) {
      return;
    }
    this.deactivatingChoreId.set(chore.id);
    this.deactivationError.set('');
    this.choresService
      .deactivateChore(chore.id)
      .pipe(finalize(() => this.deactivatingChoreId.set(null)))
      .subscribe({
        next: () => {
          this.chores.update((chores) => chores.filter((item) => item.id !== chore.id));
          if (this.editingChore()?.id === chore.id) this.closeEditChore();
          if (this.assignmentForm.controls.choreId.value === chore.id) this.closeAssignmentForm();
          this.confirmingDeactivationId.set(null);
          this.success.show(
            this.transloco.translate('adult.chores.deactivateSuccess', { title: chore.title }),
          );
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.deactivationError.set(
            this.transloco.translate(
              error.status === 404
                ? 'adult.chores.deactivateError.notFound'
                : 'adult.chores.deactivateError.generic',
            ),
          ),
      });
  }

  openAssignmentForm(choreId = 0, returnFocusId?: string): void {
    this.assignmentReturnFocusId =
      returnFocusId ?? (choreId ? `assign-chore-${choreId}` : 'open-assignment-trigger');
    this.assignmentForm.setValue({ choreId, childId: 0, dueDate: this.todayInputValue() });
    this.assignmentError.set('');
    this.resetRecurrenceState();
    this.showAssignmentForm.set(true);
    focusAfterRender('assignment-panel');
  }

  closeAssignmentForm(): void {
    this.showAssignmentForm.set(false);
    this.assignmentError.set('');
    this.assignmentForm.reset({ choreId: 0, childId: 0, dueDate: this.todayInputValue() });
    this.resetRecurrenceState();
    focusAfterRender(this.assignmentReturnFocusId);
  }

  private resetRecurrenceState(): void {
    this.isRecurring.set(false);
    this.recurrenceFrequency.set('Daily');
    this.recurrenceWeekday.set(0);
    this.recurrenceCustomDays.set(new Set());
    this.recurrenceDayOfMonth.set(1);
  }

  toggleCustomDay(dayIndex: number): void {
    this.recurrenceCustomDays.update((days) => {
      const next = new Set(days);
      if (next.has(dayIndex)) next.delete(dayIndex);
      else next.add(dayIndex);
      return next;
    });
  }

  isCustomDaySelected(dayIndex: number): boolean {
    return this.recurrenceCustomDays().has(dayIndex);
  }

  createAssignment(): void {
    if (this.assignmentForm.invalid) {
      this.assignmentForm.markAllAsTouched();
      focusAfterRender(
        this.assignmentForm.controls.choreId.invalid ? 'assignment-chore' : 'assignment-child',
      );
      return;
    }
    const request = this.assignmentForm.getRawValue();
    const chore = this.chores().find((item) => item.id === request.choreId);
    const child = this.children().find((item) => item.id === request.childId);
    if (!chore || !child) {
      this.assignmentError.set(this.transloco.translate('adult.chores.assignmentValidation'));
      return;
    }

    if (this.isRecurring()) {
      this.createRecurringAssignment(chore, child, request.dueDate);
      return;
    }

    this.isAssigning.set(true);
    this.assignmentError.set('');
    this.choresService
      .createAssignment(request)
      .pipe(finalize(() => this.isAssigning.set(false)))
      .subscribe({
        next: (created) => {
          this.assignments.update((assignments) => [
            {
              assignmentId: created.id,
              choreId: created.choreId,
              choreTitle: chore.title,
              childId: created.childId,
              childName: child.name,
              points: created.points,
              assignedAt: created.assignedAt,
              dueDate: created.dueDate,
              status: 'Assigned',
              submittedAt: null,
              reviewedByUserId: null,
              reviewedAt: null,
              reviewComment: null,
              cancelledByUserId: null,
              cancelledAt: null,
              adultArchivedAt: null,
              generatedFromRecurrenceId: null,
            },
            ...assignments,
          ]);
          this.closeAssignmentForm();
          this.success.show(
            this.transloco.translate('adult.chores.assignSuccess', {
              choreTitle: chore.title,
              childName: child.name,
            }),
          );
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.assignmentError.set(
            this.transloco.translate(
              error.status === 404
                ? 'adult.chores.assignError.notFound'
                : 'adult.chores.assignError.generic',
            ),
          ),
      });
  }

  private createRecurringAssignment(chore: Chore, child: ChildSummary, startDate: string): void {
    const frequency = this.recurrenceFrequency();
    const request: CreateChoreRecurrenceRequest = {
      choreId: chore.id,
      childId: child.id,
      frequency,
      startDate,
      daysOfWeekMask:
        frequency === 'Weekly'
          ? WEEKDAY_BITS[this.recurrenceWeekday()]
          : frequency === 'Custom'
            ? [...this.recurrenceCustomDays()].reduce((mask, day) => mask | WEEKDAY_BITS[day], 0)
            : null,
      dayOfMonth: frequency === 'Monthly' ? this.recurrenceDayOfMonth() : null,
    };

    if (frequency === 'Custom' && this.recurrenceCustomDays().size === 0) {
      this.assignmentError.set(this.transloco.translate('adult.chores.assign.customDaysRequired'));
      return;
    }

    this.isAssigning.set(true);
    this.assignmentError.set('');
    this.choresService
      .createRecurrence(request)
      .pipe(finalize(() => this.isAssigning.set(false)))
      .subscribe({
        next: (recurrence) => {
          this.recurrences.update((items) => [...items, recurrence]);
          // The recurrence may have just generated today's occurrence server-side;
          // refetch rather than guess at the shape of what was (or wasn't) created.
          this.choresService
            .getAssignments()
            .subscribe((assignments) => this.assignments.set(assignments));
          this.closeAssignmentForm();
          this.success.show(
            this.transloco.translate('adult.chores.assignRecurringSuccess', {
              choreTitle: chore.title,
              childName: child.name,
            }),
          );
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.assignmentError.set(
            this.transloco.translate(
              error.status === 404
                ? 'adult.chores.assignError.notFound'
                : 'adult.chores.assignError.generic',
            ),
          ),
      });
  }

  recurrencesForChore(choreId: number): ChoreRecurrence[] {
    return this.recurrences().filter((recurrence) => recurrence.choreId === choreId);
  }

  recurrenceScheduleLabel(recurrence: ChoreRecurrence): string {
    return recurrenceScheduleLabel(recurrence, this.transloco);
  }

  stopRecurrence(recurrence: ChoreRecurrence): void {
    if (this.stoppingRecurrenceId() !== null) return;
    this.stoppingRecurrenceId.set(recurrence.id);
    this.recurrenceStopError.set('');
    this.choresService
      .deleteRecurrence(recurrence.id)
      .pipe(finalize(() => this.stoppingRecurrenceId.set(null)))
      .subscribe({
        next: () =>
          this.recurrences.update((items) => items.filter((item) => item.id !== recurrence.id)),
        error: () =>
          this.recurrenceStopError.set(
            this.transloco.translate('adult.chores.recurrence.stopError'),
          ),
      });
  }

  private todayInputValue(): string {
    const now = new Date();
    const offset = now.getTimezoneOffset() * 60_000;
    return new Date(now.getTime() - offset).toISOString().slice(0, 10);
  }
}
