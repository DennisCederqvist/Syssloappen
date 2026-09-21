import { Injectable, inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import * as signalR from '@microsoft/signalr';
import { Subject, distinctUntilChanged } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { NotificationEvent } from './notification-event.model';

// SignalR's default reconnect policy gives up after about 45 seconds. A backend deploy or
// restart, a phone that slept, or a network switch can easily outlast that, after which the
// page silently stops receiving live updates until it is manually reloaded. Keep retrying.
const RECONNECT_DELAYS_MS = [0, 2000, 10000, 30000];

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
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (context) =>
          RECONNECT_DELAYS_MS[Math.min(context.previousRetryCount, RECONNECT_DELAYS_MS.length - 1)],
      })
      .build();

    connection.on('notification', (evt: NotificationEvent) => this.eventsSubject.next(evt));
    // Events sent while the connection was down are never replayed, so ask open pages to
    // reload once it is back.
    connection.onreconnected(() => {
      this.eventsSubject.next({ type: 'ChoresChanged', data: {} });
      this.eventsSubject.next({ type: 'RewardsChanged', data: {} });
    });

    this.connection = connection;
    // Best-effort — a page that can't connect (e.g. offline) simply falls back to
    // requiring a manual reload to see changes, same as before this feature existed.
    connection
      .start()
      .catch((error) => console.error('Realtime connection failed to start.', error));
  }

  private stop(): void {
    const connection = this.connection;
    this.connection = null;
    void connection?.stop();
  }
}
