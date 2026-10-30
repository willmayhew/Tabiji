import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * The state a screen shows when nobody is signed in.
 *
 * Trips are private to their owner, so a signed-out reader has nothing to
 * look at. This is the invitation back in, and it carries the page they were
 * trying to reach so that signing in returns them to it. It reuses the
 * editorial empty state rather than introducing a second visual language.
 */
@Component({
  selector: 'app-sign-in-prompt',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './sign-in-prompt.html',
})
export class SignInPrompt {
  /** Where to come back to after signing in. */
  readonly returnUrl = input('/trips');

  /** What the reader was trying to do, in one line. */
  readonly message = input('Your trips are private to your account.');

  /**
   * 1 where the prompt is the whole page, 2 where the page already has a
   * heading above it. Keeps one h1 per route.
   */
  readonly level = input<1 | 2>(2);
}
