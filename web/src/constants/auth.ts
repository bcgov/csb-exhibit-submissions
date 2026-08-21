/**
 * Renew this far ahead of `exp`. Covers clock skew between the browser and Keycloak
 * plus the round-trip, so a request is never sent with a token about to lapse.
 */
export const TOKEN_REFRESH_LEAD_MS = 60_000;

/**
 * Floor on the timer — a token issued with less than the lead time left refreshes almost
 * immediately rather than scheduling a negative delay.
 */
export const MIN_REFRESH_DELAY_MS = 5_000;

/** `exp` is in seconds; `Date.now()` is in milliseconds. */
export const SECONDS_TO_MS = 1000;

/** localStorage key for the dev-bypass token. Unused on the Keycloak path. */
export const BYPASS_TOKEN_STORAGE_KEY = 'jwt_token';

function normalizeBooleanString(value?: string): string | undefined {
  return value === 'true' || value === 'false' ? value : undefined;
}

/**
 * Dev-bypass mode: the mock username/password login, default-on so routine work needs
 * no Keycloak client and no secret. Release containers read the value from runtime
 * configuration injected into index.html at startup; Vite env remains the fallback for
 * the dev server and tests.
 *
 * Exposed as a function, read at call time, so it is not frozen at module load — that keeps
 * it stubbable per-test under the non-isolated Vitest pool.
 */
export function isDevAuthBypass(): boolean {
  const runtimeValue =
    typeof window === 'undefined' ? undefined : window.__CES_CONFIG__?.VITE_DEV_AUTH_BYPASS;
  return (normalizeBooleanString(runtimeValue) ?? import.meta.env.VITE_DEV_AUTH_BYPASS) !== 'false';
}
