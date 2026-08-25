import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AdultChildrenPage } from './adult-children-page';
import { CreateChildRequest } from './children.models';
import { ChildrenService } from './children.service';

class FakeChildrenService {
  createCalls = 0;

  getActiveChildren() {
    return of([{ id: 1, name: 'Leo' }]);
  }

  createChild(request: CreateChildRequest) {
    this.createCalls += 1;
    return of({ id: 2, name: request.name, userName: request.userName, role: 'Child' as const });
  }
}

describe('AdultChildrenPage', () => {
  let component: AdultChildrenPage;
  let service: FakeChildrenService;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdultChildrenPage],
      providers: [{ provide: ChildrenService, useClass: FakeChildrenService }],
    }).compileComponents();

    const fixture = TestBed.createComponent(AdultChildrenPage);
    component = fixture.componentInstance;
    service = TestBed.inject(ChildrenService) as unknown as FakeChildrenService;
    component.ngOnInit();
  });

  it('does not submit when the passwords differ', () => {
    component.childForm.setValue({
      name: 'Maja',
      userName: 'maja',
      password: 'Secret12',
      confirmPassword: 'Different12',
    });

    component.submitChild();

    expect(component.childForm.hasError('passwordMismatch')).toBe(true);
    expect(service.createCalls).toBe(0);
  });

  it('adds a successfully created child to the visible list', () => {
    component.childForm.setValue({
      name: 'Maja',
      userName: 'maja',
      password: 'Secret12',
      confirmPassword: 'Secret12',
    });

    component.submitChild();

    expect(service.createCalls).toBe(1);
    expect(component.children()).toEqual([
      { id: 1, name: 'Leo' },
      { id: 2, name: 'Maja' },
    ]);
    expect(component.createdChild()?.userName).toBe('maja');
  });
});
