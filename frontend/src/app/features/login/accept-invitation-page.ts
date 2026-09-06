import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-accept-invitation-page',
  imports: [ReactiveFormsModule],
  templateUrl: './accept-invitation-page.html',
})
export class AcceptInvitationPage {
  private readonly auth = inject(AuthService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly router = inject(Router);
  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');
  readonly success = signal(false);
  readonly form = this.formBuilder.nonNullable.group({
    invitationCode: ['', [Validators.required, Validators.minLength(9), Validators.maxLength(9)]],
    email: ['', [Validators.required, Validators.email]],
    firstName: ['', Validators.maxLength(100)],
    lastName: ['', Validators.maxLength(100)],
    nickname: ['', Validators.maxLength(50)],
    password: [
      '',
      [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/)],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.isSubmitting.set(true);
    this.errorMessage.set('');
    const { invitationCode, email, password, firstName, lastName, nickname } =
      this.form.getRawValue();
    this.auth
      .registerInvitedAdult({
        invitationCode,
        email,
        password,
        firstName: firstName || undefined,
        lastName: lastName || undefined,
        nickname: nickname || undefined,
      })
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => this.success.set(true),
        error: (error: HttpErrorResponse) =>
          this.errorMessage.set(
            error.status === 401
              ? 'Koden är felaktig, utgången eller redan använd.'
              : 'Kontot kunde inte skapas. Kontrollera uppgifterna.',
          ),
      });
  }

  goToLogin(): void {
    this.router.navigateByUrl('/login');
  }
}
