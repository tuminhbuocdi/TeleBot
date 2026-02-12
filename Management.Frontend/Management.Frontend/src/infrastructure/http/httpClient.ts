import axios from "axios"

const http = axios.create({
  baseURL: (import.meta as any).env?.VITE_API_BASE_URL ?? "https://localhost:7179/api",
  timeout: 20000,
})

export default http
