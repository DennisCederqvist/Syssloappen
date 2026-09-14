import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';
import { focusAfterRender } from '../../../shared/focus';
import { AdultBottomNav } from '../ui/bottom-nav';
import { AdultPrimaryButton } from '../ui/buttons';
import { AdultPageHeader } from '../ui/page-header';
import { FeedbackService } from './feedback.service';

@Component({
  selector: 'app-adult-feedback-page',
  imports: [
    ReactiveFormsModule,
    AdultBottomNav,
    AdultPrimaryButton,
    AdultPageHeader,
    TranslocoPipe,
  ],
  templateUrl: './adult-feedback-page.html',
})
export class AdultFeedbackPage {
  private readonly feedbackService = inject(FeedbackService);
  private readonly formBuilder = inject(FormBuilder);
  private readonly transloco = inject(TranslocoService);

  readonly isSubmitting = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');

  readonly form = this.formBuilder.nonNullable.group({
    message: ['', [Validators.required, Validators.maxLength(2000)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      focusAfterRender('feedback-message');
      return;
    }

    this.isSubmitting.set(true);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.feedbackService
      .sendFeedback(this.form.getRawValue().message.trim())
      .pipe(finalize(() => this.isSubmitting.set(false)))
      .subscribe({
        next: () => {
          this.form.reset({ message: '' });
          this.successMessage.set(this.transloco.translate('adult.feedback.success'));
          focusAfterRender('adult-feedback-success');
        },
        error: (error: HttpErrorResponse) =>
          this.errorMessage.set(
            this.transloco.translate(
              error.status === 400
                ? 'adult.feedback.errorValidation'
                : 'adult.feedback.errorGeneric',
            ),
          ),
      });
  }
}
