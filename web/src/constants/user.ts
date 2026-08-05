/**
 * Officer number rules. IDIR exposes no officer-number claim, so the value is typed by the
 * officer once and stored on their CES user row — these mirror the API's `UserConstants`.
 */

/** Ample for any badge/PIN format in use. Matches `UserConstants.OfficerNumberMaxLength`. */
export const OFFICER_NUMBER_MAX_LENGTH = 30;

/**
 * No authoritative schema exists for officer numbers, so this is a defensive character
 * allowlist rather than a format check. Matches `UserConstants.OfficerNumberPattern`.
 */
export const OFFICER_NUMBER_PATTERN = /^[A-Za-z0-9.-]+$/;

/** Everything the allowlist rejects, for stripping as the officer types. */
const DISALLOWED_CHARACTERS = /[^A-Za-z0-9.-]/g;

/**
 * Drops disallowed characters and clamps to the maximum length, so an invalid officer number
 * cannot be typed in the first place and the API's rejection path stays a backstop.
 */
export function sanitizeOfficerNumber(raw: string): string {
  return raw.replace(DISALLOWED_CHARACTERS, '').slice(0, OFFICER_NUMBER_MAX_LENGTH);
}
