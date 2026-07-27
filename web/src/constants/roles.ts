export const ROLE_ADMIN = 'Admin';
export const ROLE_USER = 'User';
export const ROLE_CLERK = 'Clerk';

/**
 * Keycloak client role → CES application role. The API performs the same mapping to
 * authorize requests; this copy exists only so the SPA can drive nav visibility and
 * route guards from the access token it already decodes.
 *
 * Roles absent from this map grant nothing, matching the backend.
 */
export const KEYCLOAK_ROLE_MAP: Readonly<Record<string, string>> = {
  'ces-judicial': ROLE_ADMIN,
  'ces-user': ROLE_USER,
  'ces-clerk': ROLE_CLERK,
};
