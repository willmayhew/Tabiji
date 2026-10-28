import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from './core/auth/session.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly session = inject(SessionService);

  /** Who is signed in, or null. Null until the API has answered once. */
  protected readonly user = this.session.user;

  /** Whether that first answer has arrived. */
  protected readonly isReady = this.session.isReady;

  constructor() {
    // Asked once, when the shell is created, so a page reload keeps the
    // reader signed in without a spinner in the header.
    this.session.restore();
  }

  protected signOut(): void {
    this.session.signOut().subscribe();
  }
}
