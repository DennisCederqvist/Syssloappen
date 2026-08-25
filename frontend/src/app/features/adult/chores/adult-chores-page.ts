import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
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
  readonly isCreatingChore = signal(false);
  readonly isAssigning = signal(false);
  readonly choreError = signal('');
  readonly assignmentError = signal('');
  readonly successMessage = signal('');

  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷', active: true, route: '/vuxen/sysslor' },
    { label: 'Barn', icon: '♧', route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓' },
  ];

  readonly choreForm = this.formBuilder.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(100)]],
    description: ['', Validators.maxLength(500)],
    points: [5, [Validators.required, Validators.pattern(/^(5|10|15|20)$/)]],
  });

  readonly assignmentForm = this.formBuilder.nonNullable.group({
    choreId: [0, Validators.min(1)],
    childId: [0, Validators.min(1)],
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
  }

  closeChoreForm(): void {
    this.showChoreForm.set(false);
    this.choreError.set('');
    this.choreForm.reset({ title: '', description: '', points: 5 });
  }

  createChore(): void {
    if (this.choreForm.invalid) {
      this.choreForm.markAllAsTouched();
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

  openAssignmentForm(choreId = 0): void {
    this.assignmentForm.setValue({ choreId, childId: 0 });
    this.assignmentError.set('');
    this.showAssignmentForm.set(true);
  }

  closeAssignmentForm(): void {
    this.showAssignmentForm.set(false);
    this.assignmentError.set('');
    this.assignmentForm.reset({ choreId: 0, childId: 0 });
  }

  createAssignment(): void {
    if (this.assignmentForm.invalid) {
      this.assignmentForm.markAllAsTouched();
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
              status: 'Assigned',
              submittedAt: null,
              reviewedByUserId: null,
              reviewedAt: null,
              reviewComment: null,
            },
            ...assignments,
          ]);
          this.closeAssignmentForm();
          this.successMessage.set(`${chore.title} är tilldelad till ${child.name}.`);
        },
        error: (error: HttpErrorResponse) =>
          this.assignmentError.set(
            error.status === 404
              ? 'Sysslan eller barnet finns inte längre i din familj.'
              : 'Sysslan kunde inte tilldelas. Försök igen.',
          ),
      });
  }
}
