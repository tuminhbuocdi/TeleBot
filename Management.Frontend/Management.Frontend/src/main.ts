import { createApp } from "vue"
import { createPinia } from "pinia"
import App from "./App.vue"

import "@/infrastructure/http/interceptor"

import router from "@/app/router"   // <-- SỬA DÒNG NÀY

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount("#app")
