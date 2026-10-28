import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * The shortest password the API accepts.
 *
 * The server is the authority; this mirrors its rule so the reader is told
 * before the request is sent. Both must change together.
 */
export const PASSWORD_MIN_LENGTH = 10;

/**
 * At least the minimum length, with an upper-case letter, a lower-case letter
 * and a digit. Deliberately no symbol: a passphrase is stronger than a short
 * password with punctuation in it, and harder to forget.
 */
export const passwordStrength: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const value = typeof control.value === 'string' ? control.value : '';

  const strong =
    value.length >= PASSWORD_MIN_LENGTH &&
    /[a-z]/.test(value) &&
    /[A-Z]/.test(value) &&
    /[0-9]/.test(value);

  return strong ? null : { passwordStrength: true };
};

/** The two passwords are the same. */
export const passwordsMatch: ValidatorFn = (
  group: AbstractControl,
): ValidationErrors | null => {
  const password = group.get('password');
  const confirmation = group.get('confirmPassword');

  if (password === null || confirmation === null) {
    return null;
  }

  return password.value === confirmation.value ? null : { passwordsMatch: true };
};
