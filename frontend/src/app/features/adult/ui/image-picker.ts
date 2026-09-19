import { Component, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

/** Preview + "choose photo" / "take photo" buttons. Emits the picked file; the parent decides
 * when to upload it. Two separate hidden inputs, not one shared `capture` toggle: on mobile,
 * `capture` forces the OS straight into the camera with no way to pick an existing photo. */
@Component({
  selector: 'app-adult-image-picker',
  imports: [TranslocoPipe],
  template: `
    <div class="mt-1.5 flex flex-wrap items-center gap-3">
      @if (previewUrl(); as url) {
        <img
          [src]="url"
          alt=""
          class="size-16 shrink-0 rounded-lg border border-adult-border object-cover"
        />
      }
      <div class="flex flex-wrap gap-2">
        <button
          type="button"
          (click)="galleryInput.click()"
          class="min-h-11 rounded-lg bg-adult-accent-soft px-4 text-sm font-semibold text-adult-accent-dark"
        >
          {{ 'common.imagePicker.choose' | transloco }}
        </button>
        <button
          type="button"
          (click)="cameraInput.click()"
          class="min-h-11 rounded-lg border border-adult-border px-4 text-sm font-semibold text-adult-text"
        >
          {{ 'common.imagePicker.take' | transloco }}
        </button>
        @if (previewUrl()) {
          <button
            type="button"
            (click)="removed.emit()"
            class="min-h-11 rounded-lg border border-adult-border px-4 text-sm font-medium text-adult-text-secondary"
          >
            {{ 'common.imagePicker.remove' | transloco }}
          </button>
        }
      </div>
      <input #galleryInput type="file" accept="image/*" (change)="pick($event)" class="hidden" />
      <input
        #cameraInput
        type="file"
        accept="image/*"
        capture="environment"
        (change)="pick($event)"
        class="hidden"
      />
    </div>
  `,
})
export class AdultImagePicker {
  readonly previewUrl = input<string | null>(null);
  readonly selected = output<File>();
  readonly removed = output<void>();

  pick(event: Event): void {
    const target = event.target as HTMLInputElement;
    const file = target.files?.[0];
    // Reset so picking the same file again after removing it still fires `change`.
    if (file) this.selected.emit(file);
    target.value = '';
  }
}
