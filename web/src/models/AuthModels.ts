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
