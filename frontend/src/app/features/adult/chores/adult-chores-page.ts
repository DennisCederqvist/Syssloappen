import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
import { focusAfterRender } from '../../../shared/focus';
import { ChildSummary } from '../children/children.models';
import { ChildrenService } from '../children/children.service';
import { AdultAssignment, Chore } from './chores.models';
import { ChoresService } from './chores.service';

@Component({
  selector: 'app-adult-chores-page',
  imports: [AppBottomNav, DatePipe, ReactiveFormsModule, UserHeader],
  templateUrl: './adult-chores-page.html',
})
export class AdultChoresPage implements OnInit {
  private readonly choresService = inject(ChoresService);
  private readonly childrenService = inject(ChildrenService);
  private readonly formBuilder = inject(FormBuilder);

  readonly chores = signal<Chore[]>([]);
  readonly children = signal<ChildSummary[]>([]);
  readonly assignments = signal<AdultAssignment[]>([]);
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
  readonly confirmingAssignmentCancellationId = signal<number | null>(null);
  readonly cancellingAssignmentId = signal<number | null>(null);
  readonly choreError = signal('');
  readonly editChoreError = signal('');
  readonly deactivationError = signal('');
  readonly assignmentError = signal('');
  readonly assignmentCancellationError = signal('');
  readonly successMessage = signal('');
  private assignmentReturnFocusId = 'open-assignment-trigger';
  private editChoreReturnFocusId = '';

  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', active: true, route: '/vuxen/sysslor' },
    { label: 'Barn', icon: '♧', route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓', route: '/vuxen/granska' },
  ];

  readonly choreForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
    points: [5, [Validators.required, Validators.pattern(/^(5|10|15|20)$/)]],
  });

  readonly assignmentForm = this.formBuilder.nonNullable.group({
    choreId: [0, Validators.min(1)],
    childId: [0, Validators.min(1)],
    dueDate: [this.todayInputValue(), Validators.required],
  });

  readonly editChoreForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
    points: [5, [Validators.required, Validators.pattern(/^(5|10|15|20)$/)]],
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
    })
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: ({ chores, children, assignments }) => {
          this.chores.set(chores);
          this.children.set(children);
          this.assignments.set(assignments);
        },
        error: () => this.loadError.set('Sysslorna kunde inte hämtas. Försök igen.'),
      });
  }

  openChoreForm(): void {
    this.choreError.set('');
    this.successMessage.set('');
    this.showChoreForm.set(true);
    focusAfterRender('new-chore-panel');
  }

  closeChoreForm(): void {
    this.showChoreForm.set(false);
    this.choreError.set('');
    this.choreForm.reset({ title: '', description: '', points: 5 });
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
      .pipe(finalize(() => this.isCreatingChore.set(false)))
      .subscribe({
        next: (chore) => {
          this.chores.update((chores) =>
            [...chores, chore].sort((a, b) => a.title.localeCompare(b.title, 'sv')),
          );
          this.closeChoreForm();
          this.successMessage.set(`${chore.title} är skapad och kan nu tilldelas.`);
          this.openAssignmentForm(chore.id);
        },
        error: (error: HttpErrorResponse) =>
          this.choreError.set(
            error.status === 400
              ? 'Kontrollera titel, beskrivning och poäng.'
              : 'Sysslan kunde inte skapas. Försök igen.',
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
    this.editChoreError.set('');
    this.deactivationError.set('');
    this.confirmingDeactivationId.set(null);
    focusAfterRender('edit-chore-panel');
  }

  closeEditChore(): void {
    this.editingChore.set(null);
    this.editChoreForm.reset({ title: '', description: '', points: 5 });
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
      .pipe(finalize(() => this.isUpdatingChore.set(false)))
      .subscribe({
        next: (updated) => {
          this.chores.update((chores) =>
            chores
              .map((item) => (item.id === updated.id ? updated : item))
              .sort((a, b) => a.title.localeCompare(b.title, 'sv')),
          );
          this.closeEditChore();
          this.successMessage.set(`${updated.title} är uppdaterad.`);
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.editChoreError.set(
            error.status === 404
              ? 'Sysslan är inte längre aktiv eller finns inte i din familj.'
              : error.status === 400
                ? 'Kontrollera titel, beskrivning och poäng.'
                : 'Sysslan kunde inte uppdateras. Försök igen.',
          ),
      });
  }

  requestDeactivation(choreId: number): void {
    this.confirmingDeactivationId.set(choreId);
    this.deactivationError.set('');
    this.successMessage.set('');
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
          this.successMessage.set(`${chore.title} är bortplockad från uppgiftsbanken.`);
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.deactivationError.set(
            error.status === 404
              ? 'Sysslan är redan bortplockad eller finns inte i din familj.'
              : 'Sysslan kunde inte plockas bort. Försök igen.',
          ),
      });
  }

  openAssignmentForm(choreId = 0, returnFocusId?: string): void {
    this.assignmentReturnFocusId =
      returnFocusId ?? (choreId ? `assign-chore-${choreId}` : 'open-assignment-trigger');
    this.assignmentForm.setValue({ choreId, childId: 0, dueDate: this.todayInputValue() });
    this.assignmentError.set('');
    this.showAssignmentForm.set(true);
    focusAfterRender('assignment-panel');
  }

  closeAssignmentForm(): void {
    this.showAssignmentForm.set(false);
    this.assignmentError.set('');
    this.assignmentForm.reset({ choreId: 0, childId: 0, dueDate: this.todayInputValue() });
    focusAfterRender(this.assignmentReturnFocusId);
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
      this.assignmentError.set('Välj en syssla och ett aktivt barn.');
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
            },
            ...assignments,
          ]);
          this.closeAssignmentForm();
          this.successMessage.set(`${chore.title} är tilldelad till ${child.name}.`);
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.assignmentError.set(
            error.status === 404
              ? 'Sysslan eller barnet finns inte längre i din familj.'
              : 'Sysslan kunde inte tilldelas. Försök igen.',
          ),
      });
  }

  requestAssignmentCancellation(assignmentId: number): void {
    this.confirmingAssignmentCancellationId.set(assignmentId);
    this.assignmentCancellationError.set('');
    this.successMessage.set('');
    focusAfterRender(`cancel-assignment-cancellation-${assignmentId}`);
  }

  cancelAssignmentCancellation(): void {
    const assignmentId = this.confirmingAssignmentCancellationId();
    this.confirmingAssignmentCancellationId.set(null);
    this.assignmentCancellationError.set('');
    if (assignmentId) focusAfterRender(`request-assignment-cancellation-${assignmentId}`);
  }

  cancelAssignment(assignment: AdultAssignment): void {
    if (
      this.confirmingAssignmentCancellationId() !== assignment.assignmentId ||
      this.cancellingAssignmentId() !== null
    ) {
      return;
    }

    this.cancellingAssignmentId.set(assignment.assignmentId);
    this.assignmentCancellationError.set('');
    this.choresService
      .cancelAssignment(assignment.assignmentId)
      .pipe(finalize(() => this.cancellingAssignmentId.set(null)))
      .subscribe({
        next: () => {
          this.assignments.update((assignments) =>
            assignments.filter((item) => item.assignmentId !== assignment.assignmentId),
          );
          this.confirmingAssignmentCancellationId.set(null);
          this.successMessage.set(
            `${assignment.choreTitle} är borttagen från ${assignment.childName}.`,
          );
          focusAfterRender('adult-chores-success');
        },
        error: (error: HttpErrorResponse) =>
          this.assignmentCancellationError.set(
            error.status === 404
              ? 'Tilldelningen finns inte längre i din familj.'
              : error.status === 409
                ? 'Tilldelningen har redan godkänts eller ändrats och kan inte tas bort.'
                : 'Tilldelningen kunde inte tas bort. Försök igen.',
          ),
      });
  }

  assignmentStatusLabel(status: AdultAssignment['status']): string {
    switch (status) {
      case 'Assigned':
        return 'Tilldelad';
      case 'PendingApproval':
        return 'Väntar på granskning';
      case 'NeedsRedo':
        return 'Behöver göras om';
      case 'Approved':
        return 'Godkänd';
      case 'Cancelled':
        return 'Borttagen';
    }
  }

  private todayInputValue(): string {
    const now = new Date();
    const offset = now.getTimezoneOffset() * 60_000;
    return new Date(now.getTime() - offset).toISOString().slice(0, 10);
  }
}
