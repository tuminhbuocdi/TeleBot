import type {
  AxiosInstance,
  AxiosRequestConfig,
  AxiosResponse,
  InternalAxiosRequestConfig,
} from "axios"
import axios from "axios"
import { clearAuthSession, isTokenExpired } from "@/shared/auth/jwt"

export type ApiError = {
  status?: number
  message: string
  details?: unknown
}

export class ApiClient {
  private readonly http: AxiosInstance

  constructor(baseURL: string, timeout = 20000) {
    this.http = axios.create({
      baseURL,
      timeout,
    })

    this.http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
      const token = localStorage.getItem("token")
      if (token && !isTokenExpired(token)) {
        config.headers.Authorization = `Bearer ${token}`
      } else if (token) {
        clearAuthSession()
      }
      return config
    })

    this.http.interceptors.response.use(
      (res) => res,
      (err) => {
        const status = err?.response?.status
        if (status === 401) {
          clearAuthSession()
        }
        return Promise.reject(err)
      },
    )
  }

  async request<T>(config: AxiosRequestConfig): Promise<T> {
    try {
      const res: AxiosResponse<T> = await this.http.request<T>(config)
      return res.data
    } catch (e: any) {
      throw this.normalizeError(e)
    }
  }

  get<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({
      ...config,
      method: "GET",
      url,
    })
  }

  post<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({
      ...config,
      method: "POST",
      url,
      data,
    })
  }

  put<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({
      ...config,
      method: "PUT",
      url,
      data,
    })
  }

  delete<T>(url: string, config?: AxiosRequestConfig): Promise<T> {
    return this.request<T>({
      ...config,
      method: "DELETE",
      url,
    })
  }

  private normalizeError(e: any): ApiError {
    const status: number | undefined = e?.response?.status
    const data = e?.response?.data

    if (typeof data === "string" && data.trim().length > 0) {
      return { status, message: data, details: data }
    }

    if (data && typeof data === "object") {
      const message = (data as any).message ?? e?.message ?? "Request failed"
      return { status, message, details: data }
    }

    return { status, message: e?.message ?? "Request failed", details: data }
  }
}

const apiBaseUrl = (import.meta as any).env?.VITE_API_BASE_URL ??
  "https://localhost:7179/api"

const api = new ApiClient(apiBaseUrl)

export default api
