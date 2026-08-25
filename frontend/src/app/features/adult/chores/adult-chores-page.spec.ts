import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ChildrenService } from '../children/children.service';
import { AdultChoresPage } from './adult-chores-page';
import { CreateAssignmentRequest, CreateChoreRequest } from './chores.models';
import { ChoresService } from './chores.service';

class FakeChoresService {
  choreCalls: CreateChoreRequest[] = [];
  assignmentCalls: CreateAssignmentRequest[] = [];
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
}

class FakeChildrenService {
  getActiveChildren() {
    return of([{ id: 7, name: 'Maja' }]);
  }
}

describe('AdultChoresPage', () => {
  let component: AdultChoresPage;
  let service: FakeChoresService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdultChoresPage],
      providers: [
        { provide: ChoresService, useClass: FakeChoresService },
        { provide: ChildrenService, useClass: FakeChildrenService },
      ],
    }).compileComponents();
    component = TestBed.createComponent(AdultChoresPage).componentInstance;
    service = TestBed.inject(ChoresService) as unknown as FakeChoresService;
    component.ngOnInit();
  });

  it('creates a trimmed chore with the selected points', () => {
    component.choreForm.setValue({ title: '  Bädda sängen  ', description: '', points: 5 });
    component.createChore();
    expect(service.choreCalls).toEqual([{ title: 'Bädda sängen', description: null, points: 5 }]);
    expect(component.chores()).toHaveLength(2);
  });

  it('creates an assignment using only selected ids', () => {
    component.openAssignmentForm(1);
    component.assignmentForm.setValue({ choreId: 1, childId: 7 });
    component.createAssignment();
    expect(service.assignmentCalls).toEqual([{ choreId: 1, childId: 7 }]);
    expect(component.assignments()[0].childName).toBe('Maja');
  });
});
