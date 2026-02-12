<template>
  <div>
    <h2>Login</h2>
    <div>
      <div>
        <input v-model="login" placeholder="Username / Email / Phone" />
      </div>
      <div>
        <input v-model="password" type="password" placeholder="Password" />
      </div>
      <div>
        <button @click="onLogin" :disabled="loading">Login</button>
        <a href="/register">Register</a>
      </div>
      <div v-if="error" style="color: red">{{ error }}</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue"
import { useRouter } from "vue-router"
import api, { type ApiError } from "@/infrastructure/http/apiClient"

const router = useRouter()

const login = ref("")
const password = ref("")
const loading = ref(false)
const error = ref<string | null>(null)

const onLogin = async () => {
  error.value = null
  loading.value = true
  try {
    const res = await api.post<{ token: string }>("/auth/login", {
      login: login.value,
      password: password.value,
    })

    localStorage.setItem("token", res.token)
    await router.push("/dashboard")
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Login failed"
  } finally {
    loading.value = false
  }
}
</script>
