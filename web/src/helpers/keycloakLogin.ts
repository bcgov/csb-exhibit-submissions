/**
 * Builds the URL that starts the server-mediated Keycloak flow.
 *
 * Lives here rather than in AuthService so the router guard can use it without importing
 * AuthService, which imports the router.
 */
export function buildKeycloakLoginUrl(returnUrl?: string): string {
  const target =
    returnUrl && returnUrl !== '/' ? `?returnUrl=${encodeURIComponent(returnUrl)}` : '';
  return `/api/auth/login${target}`;
}
