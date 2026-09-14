import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class FeedbackService {
  private readonly http = inject(HttpClient);

  sendFeedback(message: string): Observable<void> {
    return this.http.post<void>('/api/support/contact', { message });
  }
}
