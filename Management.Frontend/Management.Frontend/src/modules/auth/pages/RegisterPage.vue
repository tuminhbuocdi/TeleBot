<template>
  <div>
    <h2>Register</h2>
    <div>
      <div>
        <input v-model="username" placeholder="Username" />
      </div>
      <div>
        <input v-model="email" placeholder="Email (optional)" />
      </div>
      <div>
        <input v-model="phone" placeholder="Phone (optional)" />
      </div>
      <div>
        <input v-model="fullName" placeholder="Full name (optional)" />
      </div>
      <div>
        <input v-model="password" type="password" placeholder="Password" />
      </div>
      <div>
        <button @click="onRegister" :disabled="loading">Create account</button>
        <a href="/login">Back to login</a>
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

const username = ref("")
const email = ref("")
const phone = ref("")
const fullName = ref("")
const password = ref("")
const loading = ref(false)
const error = ref<string | null>(null)

const onRegister = async () => {
  error.value = null
  loading.value = true
  try {
    const res = await api.post<{ token: string }>("/auth/register", {
      username: username.value,
      email: email.value || null,
      phone: phone.value || null,
      fullName: fullName.value || null,
      password: password.value,
    })

    localStorage.setItem("token", res.token)
    await router.push("/dashboard")
  } catch (e: any) {
    const err = e as ApiError
    error.value = err.message ?? "Register failed"
  } finally {
    loading.value = false
  }
}
</script>
