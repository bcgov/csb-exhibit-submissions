/**
 * The signed-in user's CES-local row (`GET /api/users/me`). Identity fields are echoed for
 * display only — the token stays authoritative for who the user is and what they may do.
 */
export interface UserProfileModel {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  /** Null until the officer supplies it; never set for Admin/Clerk users. */
  officerNumber: string | null;
}
