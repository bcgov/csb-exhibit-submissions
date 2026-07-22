export interface JwtPayload {
  sub: string;
  email: string;
  exp: number;
  iss?: string;
  role: string;
}

export interface User {
  id: string;
  email: string;
  roles: string[];
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
