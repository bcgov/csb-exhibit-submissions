export interface JwtPayload {
  sub: string;
  email: string;
  exp: number;
  iss?: string;
  /** Dev-bypass mock token: a single CES role name. */
  role?: string;
  /** Keycloak token: raw client role names, mapped through KEYCLOAK_ROLE_MAP. */
  roles?: string[];
  name?: string;
  preferred_username?: string;
}

export interface User {
  id: string;
  email: string;
  roles: string[];
  displayName?: string;
}

/**
 * Response from `POST /api/auth/callback`. Carries no refresh token by design — that
 * stays in the encrypted HttpOnly cookie the API issues and is unreachable from JS.
 */
export interface AuthCallbackResponse {
  accessToken: string;
  /** Access-token lifetime in seconds. */
  expiresIn: number;
  /** Validated server-side; safe to navigate to. */
  returnUrl: string;
}

/** Response from `POST /api/auth/refresh`. Same no-refresh-token rule as the callback. */
export interface AuthRefreshResponse {
  accessToken: string;
  expiresIn: number;
}

/** Response from `POST /api/auth/logout`. */
export interface AuthLogoutResponse {
  endSessionUrl: string;
}
