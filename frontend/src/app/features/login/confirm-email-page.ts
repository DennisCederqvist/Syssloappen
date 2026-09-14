import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthService } from '../../core/auth/auth.service';

type ConfirmState = 'loading' | 'success' | 'error' | 'missingLink';

@Component({
  selector: 'app-confirm-email-page',
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './confirm-email-page.html',
})
export class ConfirmEmailPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  readonly state = signal<ConfirmState>('loading');
  readonly errorMessage = signal('');

  ngOnInit(): void {
    const userId = this.route.snapshot.queryParamMap.get('userId');
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!userId || !token) {
      this.state.set('missingLink');
      return;
    }

    this.auth.confirmEmail({ userId, token }).subscribe({
      next: () => this.state.set('success'),
      error: (error: HttpErrorResponse) => {
        this.errorMessage.set(
          this.transloco.translate(
            error.status === 400
              ? 'auth.confirmEmail.errorInvalid'
              : 'auth.confirmEmail.errorGeneric',
          ),
        );
        this.state.set('error');
      },
    });
  }

  goToLogin(): void {
    this.router.navigateByUrl('/login');
  }
}
