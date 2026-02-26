import { createRouter, createWebHistory } from 'vue-router'
import DevDashboard from '@/views/DevDashboardView.vue'
import Submissions from '@/components/officer/Submission.vue'

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

export default router
