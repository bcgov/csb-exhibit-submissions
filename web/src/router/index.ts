import { createRouter, createWebHistory } from 'vue-router'
import DevDashboard from '@/views/DevDashboard.vue'

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
