import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { provideTranslocoTesting } from '../../../core/i18n/testing';
import { ChildrenService } from '../children/children.service';
import { AdultChoresPage } from './adult-chores-page';
import { CreateAssignmentRequest, CreateChoreRequest, UpdateChoreRequest } from './chores.models';
import { ChoresService } from './chores.service';

class FakeChoresService {
  choreCalls: CreateChoreRequest[] = [];
  updateCalls: { choreId: number; request: UpdateChoreRequest }[] = [];
  deactivateCalls: number[] = [];
  assignmentCalls: CreateAssignmentRequest[] = [];
  cancelAssignmentCalls: number[] = [];
  getChores() {
    return of([{ id: 1, title: 'Mata katten', description: null, points: 10, createdAt: '' }]);
  }
  getAssignments() {
    return of([]);
  }
  createChore(request: CreateChoreRequest) {
    this.choreCalls.push(request);
    return of({ id: 2, ...request, points: 5 as const, createdAt: '' });
  }
  createAssignment(request: CreateAssignmentRequest) {
    this.assignmentCalls.push(request);
    return of({ id: 8, ...request, points: 10, assignedAt: '2026-08-25T10:00:00Z' });
  }
  updateChore(choreId: number, request: UpdateChoreRequest) {
    this.updateCalls.push({ choreId, request });
    return of({
      id: choreId,
      ...request,
      points: request.points,
      createdAt: '',
    });
  }
  deactivateChore(choreId: number) {
    this.deactivateCalls.push(choreId);
    return of(undefined);
  }
  cancelAssignment(assignmentId: number) {
    this.cancelAssignmentCalls.push(assignmentId);
    return of(undefined);
  }
  getRecurrences() {
    return of([]);
  }
}

class FakeChildrenService {
  getActiveChildren() {
    return of([{ id: 7, name: 'Maja' }]);
  }
}

describe('AdultChoresPage', () => {
  let component: AdultChoresPage;
  let fixture: ComponentFixture<AdultChoresPage>;
  let service: FakeChoresService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdultChoresPage],
      providers: [
        provideRouter([]),
        provideTranslocoTesting(),
        { provide: ChoresService, useClass: FakeChoresService },
        { provide: ChildrenService, useClass: FakeChildrenService },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdultChoresPage);
    component = fixture.componentInstance;
    service = TestBed.inject(ChoresService) as unknown as FakeChoresService;
    fixture.detectChanges();
  });

  it('creates a trimmed chore with the selected points', () => {
    component.choreForm.setValue({ title: '  Bädda sängen  ', description: '', points: 5 });
    component.createChore();
    expect(service.choreCalls).toEqual([{ title: 'Bädda sängen', description: null, points: 5 }]);
    expect(component.chores()).toHaveLength(2);
  });

  it('creates an assignment with the selected chore, child and date', () => {
    component.openAssignmentForm(1);
    component.assignmentForm.setValue({ choreId: 1, childId: 7, dueDate: '2026-08-27' });
    component.createAssignment();
    expect(service.assignmentCalls).toEqual([{ choreId: 1, childId: 7, dueDate: '2026-08-27' }]);
    expect(component.assignments()[0].childName).toBe('Maja');
  });

  it('updates editable fields and refreshes the chore immediately', () => {
    const chore = component.chores()[0];
    component.openEditChore(chore);
    component.editChoreForm.setValue({
      title: '  Mata katterna  ',
      description: '  På morgonen  ',
      points: 15,
    });
    component.updateChore();

    expect(service.updateCalls).toEqual([
      {
        choreId: 1,
        request: { title: 'Mata katterna', description: 'På morgonen', points: 15 },
      },
    ]);
    expect(component.chores()[0].title).toBe('Mata katterna');
    expect(component.editingChore()).toBeNull();
  });

  it('requires confirmation before deactivation and removes the active card after success', () => {
    const chore = component.chores()[0];
    component.deactivateChore(chore);
    expect(service.deactivateCalls).toEqual([]);

    component.requestDeactivation(chore.id);
    component.deactivateChore(chore);

    expect(service.deactivateCalls).toEqual([1]);
    expect(component.chores()).toEqual([]);
  });

  it('gives the card edit button an accessible name and opens the deactivate flow from the edit sheet', () => {
    fixture.detectChanges();
    const editButton = fixture.nativeElement.querySelector(
      'button[aria-label="Redigera Mata katten"]',
    ) as HTMLButtonElement | null;
    expect(editButton).not.toBeNull();

    editButton?.click();
    fixture.detectChanges();

    const deactivateButton = fixture.nativeElement.querySelector(
      `#deactivate-chore-${component.editingChore()?.id}`,
    ) as HTMLButtonElement | null;
    expect(deactivateButton).not.toBeNull();
  });
});
