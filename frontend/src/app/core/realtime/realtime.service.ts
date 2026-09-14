import { Injectable, inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import * as signalR from '@microsoft/signalr';
import { Subject, distinctUntilChanged } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { NotificationEvent } from './notification-event.model';

/**
 * Keeps one persistent SignalR connection to /hubs/notifications for as long as the
 * user is signed in, and republishes every server-pushed event on {@link events$}.
 * Any page can subscribe and filter by `type` to refresh itself live instead of
 * requiring a manual reload.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private connection: signalR.HubConnection | null = null;
  private readonly eventsSubject = new Subject<NotificationEvent>();

  readonly events$ = this.eventsSubject.asObservable();

  constructor() {
    toObservable(this.auth.isAuthenticated)
      .pipe(distinctUntilChanged())
      .subscribe((isAuthenticated) => {
        if (isAuthenticated) {
          this.start();
        } else {
          this.stop();
        }
      });
  }

  private start(): void {
    if (this.connection) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', { withCredentials: true })
      .withAutomaticReconnect()
      .build();

    connection.on('notification', (evt: NotificationEvent) => this.eventsSubject.next(evt));

    this.connection = connection;
    // Best-effort — a page that can't connect (e.g. offline) simply falls back to
    // requiring a manual reload to see changes, same as before this feature existed.
    connection.start().catch(() => undefined);
  }

  private stop(): void {
    const connection = this.connection;
    this.connection = null;
    void connection?.stop();
  }
}
