import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { finalize } from 'rxjs';
import { AppBottomNav, NavItem } from '../../../shared/app-bottom-nav';
import { UserHeader } from '../../../shared/user-header';
import { ChildSummary, CreatedChild } from './children.models';
import { ChildrenService } from './children.service';

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  return control.get('password')?.value === control.get('confirmPassword')?.value
    ? null
    : { passwordMismatch: true };
}

@Component({
  selector: 'app-adult-children-page',
  imports: [AppBottomNav, ReactiveFormsModule, UserHeader],
  templateUrl: './adult-children-page.html',
})
export class AdultChildrenPage implements OnInit {
  private readonly childrenService = inject(ChildrenService);
  private readonly formBuilder = inject(FormBuilder);

  readonly children = signal<ChildSummary[]>([]);
  readonly isLoading = signal(true);
  readonly isSubmitting = signal(false);
  readonly showForm = signal(false);
  readonly loadError = signal('');
  readonly formError = signal('');
  readonly createdChild = signal<CreatedChild | null>(null);

  readonly navItems: NavItem[] = [
    { label: 'Hem', icon: '⌂', route: '/vuxen' },
    { label: 'Sysslor', icon: '☷' },
    { label: 'Barn', icon: '♧', active: true, route: '/vuxen/barn' },
    { label: 'Granska', icon: '✓' },
  ];

  readonly childForm = this.formBuilder.nonNullable.group(
    {
      name: ['', [Validators.required, Validators.maxLength(100)]],
      userName: ['', [Validators.required, Validators.maxLength(50)]],
      password: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
          Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/),
        ],
      ],
      confirmPassword: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  ngOnInit(): void {
    this.loadChildren();
  }

  loadChildren(): void {
    this.isLoading.set(true);
    this.loadError.set('');
    this.childrenService
      .getActiveChildren()
      .pipe(finalize(() => this.isLoading.set(false)))
      .subscribe({
        next: (children) => this.children.set(children),
        error: () => this.loadError.set('Barnen kunde inte hämtas. Försök igen.'),
      });
  }

  openForm(): void {
    this.createdChild.set(null);
    this.formError.set('');
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
    this.formError.set('');
    this.childForm.reset();
  }

  submitChild(): void {
    if (this.childForm.invalid) {
      this.childForm.markAllAsTouched();
      return;
    }

    const { name, userName, password } = this.childForm.getRawValue();
    this.isSubmitting.set(true);
    this.formError.set('');
    this.childrenService
      .createChild({ name, userName, password })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: (child) => {
          this.children.update((children) =>
            [...children, { id: child.id, name: child.name }].sort((a, b) =>
              a.name.localeCompare(b.name, 'sv'),
            ),
          );
          this.createdChild.set(child);
          this.showForm.set(false);
          this.childForm.reset();
        },
        error: (error: HttpErrorResponse) => {
          if (error.status === 409) {
            this.formError.set('Användarnamnet används redan av ett barn i familjen.');
          } else if (error.status === 400) {
            this.formError.set('Kontrollera namn, användarnamn och lösenord.');
          } else {
            this.formError.set('Barnkontot kunde inte skapas. Försök igen om en liten stund.');
          }
        },
      });
  }
}
