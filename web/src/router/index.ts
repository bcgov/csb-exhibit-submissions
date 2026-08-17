import { createRouter, createWebHistory } from 'vue-router';
import DevDashboard from '@/views/DevDashboardView.vue';
import { useAuthStore } from '@/stores/authStore';
import { ROLE_ADMIN, ROLE_USER, ROLE_CLERK } from '@/constants/roles';
import { isDevAuthBypass } from '@/constants/auth';
import { buildKeycloakLoginUrl } from '@/helpers/keycloakLogin';

/** Routes that complete a login and so must never trigger one. */
const AUTH_ROUTE_PREFIX = '/auth/';

//TODO: Future auth guard when using keycloak
//Copied from project: bcgov-jasper
// async function authGuard(to: any, from: any, next: any) {
//   const commonStore = useCommonStore();
//   const results = await SessionManager.getSettings();

//   if (
//     !isPositiveInteger(commonStore?.userInfo?.roles?.length) ||
//     commonStore?.userInfo?.isActive === false ||
//     !commonStore?.userInfo?.judgeId
//   ) {
//     if (to.name === 'RequestAccess') {
//       next();
//     } else {
//       next({ path: '/request-access' });
//     }
//   } else if (results) {
//     if (
//       isPositiveInteger(commonStore?.userInfo?.roles?.length) &&
//       commonStore?.userInfo?.isActive === true &&
//       commonStore?.userInfo?.judgeId &&
//       to.name === 'RequestAccess'
//     ) {
//       next({ path: '/' });
//     } else {
//       next();
//     }
//   }
// }

// To go with authGuard at top
// router.beforeEach((to, from, next) => {
//   if (to.path === '/') {
//     next();
//   } else {
//     authGuard(to, from, next);
//   }
// });

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      name: 'DevDashboard',
      component: DevDashboard,
    },
    {
      path: '/dev',
      name: 'DevDashboard',
      component: DevDashboard,
    },
    {
      path: '/officer/court-list',
      name: 'OfficerCourtList',
      component: () => import('@/views/officer/CourtListView.vue'),
      meta: { requiresAuth: true, roles: [ROLE_USER] },
    },
    {
      path: '/officer/submission/',
      name: 'OfficerSubmissions',
      component: () => import('@/views/officer/SubmissionsView.vue'),
      meta: { requiresAuth: true, roles: [ROLE_USER] },
    },
    {
      path: '/admin/exhibit-search',
      name: 'AdminExhibitSearch',
      component: () => import('@/views/admin/ExhibitSearchView.vue'),
      meta: { requiresAuth: true, roles: [ROLE_ADMIN] },
    },
    {
      path: '/admin/list',
      name: 'AdminSubmissionList',
      component: () => import('@/views/admin/ListingView.vue'),
      meta: { requiresAuth: true, roles: [ROLE_ADMIN, ROLE_CLERK] },
    },
    {
      path: '/admin/view/:id',
      name: 'AdminViewSubmission',
      component: () => import('@/views/admin/SubmissionReviewView.vue'),
      meta: { requiresAuth: true, roles: [ROLE_ADMIN, ROLE_CLERK] },
    },
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/LoginView.vue'),
    },
    {
      path: '/forbidden',
      name: 'Forbidden',
      component: () => import('@/views/ForbiddenView.vue'),
    },
    // Registered unconditionally and deliberately unguarded — guarding the callback
    // would deadlock the login it exists to complete. Harmless in dev-bypass mode,
    // where nothing ever navigates here.
    {
      path: '/auth/callback',
      name: 'AuthCallback',
      component: () => import('@/views/AuthCallbackView.vue'),
    },
    {
      path: '/auth/error',
      name: 'AuthError',
      component: () => import('@/views/AuthErrorView.vue'),
    },
  ],
});

// router.beforeEach((to, from, next) => {
//   const authStore = useAuthStore();
//   const requiresAuth = to.meta.requiresAuth;

//   if (requiresAuth && !authStore.isAuthenticated) {
//     next({ name: 'Login' }); // Or wherever your temporary login component is
//   } else {
//     next();
//   }
// });
/**
 * On the Keycloak path the access token lives in memory, so a hard reload starts with no
 * token even when the session is live. One bootstrap `POST /api/auth/refresh` re-mints it
 * from the HttpOnly cookie before the first guarded navigation resolves, so a reload does
 * not flash the login screen.
 *
 * Run once per page load, and never on the auth routes — there is legitimately no session
 * cookie yet at `/auth/callback`, where a 401 is the expected answer rather than a failure.
 */
let bootstrapPromise: Promise<unknown> | null = null;

async function ensureSessionBootstrapped(path: string): Promise<void> {
  if (isDevAuthBypass() || path.startsWith(AUTH_ROUTE_PREFIX)) return;

  // Imported lazily: sessionService reaches apiClient, which reaches AuthService, which
  // imports this module.
  bootstrapPromise ??= import('@/services/sessionService').then((module) => module.bootstrap());

  await bootstrapPromise;
}

router.beforeEach(async (to, from, next) => {
  await ensureSessionBootstrapped(to.path);

  const authStore = useAuthStore();

  const requiresAuth = to.meta.requiresAuth as boolean | undefined;
  const requiredRoles = to.meta.roles as string[] | undefined;

  // The mock login form only exists on the bypass path; otherwise Keycloak owns the
  // login screen and the browser has to leave the SPA to reach it.
  if (!isDevAuthBypass() && to.name === 'Login') {
    window.location.assign(buildKeycloakLoginUrl(to.query.redirect as string | undefined));
    return next(false);
  }

  // Not logged in
  if (requiresAuth && !authStore.isAuthenticated) {
    if (!isDevAuthBypass()) {
      window.location.assign(buildKeycloakLoginUrl(to.fullPath));
      return next(false);
    }

    return next({ name: 'Login' });
  }

  // The officer number is not a token claim, so it has to be fetched. Done here rather than
  // at each login path because this is the one point every route into the app passes through
  // — including a dev-bypass reload, which restores its token from storage and mints nothing.
  // Not awaited: navigation must not wait on it, and loadProfile is a no-op once loaded.
  if (authStore.isAuthenticated) {
    void authStore.loadProfile();
  }

  // Logged in but missing role
  if (requiredRoles && requiredRoles.length > 0) {
    const hasRole = requiredRoles.some((role) => authStore.roles.includes(role));

    if (!hasRole) {
      return next({ name: 'Forbidden' }); // create this route
    }
  }

  next();
});
export default router;
