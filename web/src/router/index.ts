import { createRouter, createWebHistory } from 'vue-router'
import DevDashboard from '@/views/DevDashboardView.vue'
import Submissions from '@/components/officer/Submission.vue'
import { useAuthStore } from '@/stores/authStore';


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
    component: DevDashboard
  },
  {
    path: '/officer/submission/:id',
    name: 'OfficerSubmissions',
    component: Submissions
  },
  {
    path: '/officer/court-list',
    name: 'OfficerCourtList',
    component: () => import('@/views/officer/CourtListView.vue')
  },
  {
    path: '/admin/list',
    name: 'AdminSubmissionList',
    component: () => import('@/views/admin/ListingView.vue')
  },
  {
    path: '/admin/view/:id',
    name: 'AdminViewSubmission',
    component: () => import('@/views/admin/SubmissionReviewView.vue')
  },
  {
    path: '/login',
    name: 'Login',
    component: () => import('@/views/LoginView.vue')
  }
    // {
    //   path: '/about',
    //   name: 'about',
    //   // route level code-splitting
    //   // this generates a separate chunk (About.[hash].js) for this route
    //   // which is lazy-loaded when the route is visited.
    //   component: () => import('../views/AboutView.vue'),
    // },
  ],
})

// To go with authGuard at top
// router.beforeEach((to, from, next) => {
//   if (to.path === '/') {
//     next();
//   } else {
//     authGuard(to, from, next);
//   }
// });
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore();
  const requiresAuth = to.meta.requiresAuth;

  if (requiresAuth && !authStore.isAuthenticated) {
    next({ name: 'Login' }); // Or wherever your temporary login component is
  } else {
    next();
  }
});
export default router
