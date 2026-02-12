import { createRouter, createWebHistory } from "vue-router"
import { clearAuthSession, isTokenExpired } from "@/shared/auth/jwt"

const routes = [
  {
    path: "/login",
    component: () => import("@/modules/auth/pages/LoginPage.vue"),
  },
  {
    path: "/register",
    component: () => import("@/modules/auth/pages/RegisterPage.vue"),
  },
  {
    path: "/",
    component: () => import("@/app/layouts/MainLayout.vue"),
    meta: { requiresAuth: true },
    children: [
      {
        path: "dashboard",
        component: () =>
          import("@/modules/dashboard/pages/DashboardPage.vue"),
      },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  if (to.meta.requiresAuth) {
    const token = localStorage.getItem("token")
    if (!token || isTokenExpired(token)) {
      clearAuthSession()
      return "/login"
    }
  }
})

export default router
