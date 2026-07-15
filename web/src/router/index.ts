import { createRouter, createWebHistory } from 'vue-router';
import DevDashboard from '@/views/DevDashboardView.vue';
import { useAuthStore } from '@/stores/authStore';
import { ROLE_ADMIN, ROLE_USER, ROLE_CLERK } from '@/constants/roles';

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
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore();

  const requiresAuth = to.meta.requiresAuth as boolean | undefined;
  const requiredRoles = to.meta.roles as string[] | undefined;
  console.log(authStore.roles);

  // Not logged in
  if (requiresAuth && !authStore.isAuthenticated) {
    return next({ name: 'Login' });
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
