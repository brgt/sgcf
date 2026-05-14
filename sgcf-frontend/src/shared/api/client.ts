import axios from 'axios'
import type { AxiosInstance, AxiosRequestConfig } from 'axios'

const baseURL = (import.meta.env['VITE_API_BASE_URL'] as string | undefined) ?? 'http://localhost:5000'

export const apiClient: AxiosInstance = axios.create({
  baseURL,
  timeout: 30_000,
  headers: { 'Content-Type': 'application/json' },
})

// Request interceptor: inject auth token when present (dev mode only)
apiClient.interceptors.request.use((config) => {
  if (import.meta.env.DEV) {
    const token = import.meta.env['VITE_DEV_TOKEN'] as string | undefined
    if (token) {
      config.headers['Authorization'] = `Bearer ${token}`
    }
  }
  return config
})

// Response interceptor: handle 401 by redirecting to login
apiClient.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      window.location.href = '/login'
    }
    // 403: handled by the calling component (show toast)
    return Promise.reject(error)
  },
)

/**
 * POST helper that attaches an Idempotency-Key header automatically.
 * Use for all mutation POSTs (contract create, guarantee add, simulate).
 */
export function postIdempotent<T>(url: string, data: unknown, config?: AxiosRequestConfig): Promise<T> {
  return apiClient.post<T>(url, data, {
    ...config,
    headers: {
      ...config?.headers,
      'Idempotency-Key': crypto.randomUUID(),
    },
  }).then((r) => r.data)
}

/** Generic typed GET */
export function get<T>(url: string, params?: Record<string, unknown>): Promise<T> {
  return apiClient.get<T>(url, { params }).then((r) => r.data)
}

/** Generic typed POST */
export function post<T>(url: string, data?: unknown): Promise<T> {
  return apiClient.post<T>(url, data).then((r) => r.data)
}

/** Generic typed PUT */
export function put<T>(url: string, data?: unknown): Promise<T> {
  return apiClient.put<T>(url, data).then((r) => r.data)
}

/** Generic typed DELETE */
export function del(url: string): Promise<void> {
  return apiClient.delete(url).then(() => undefined)
}

/** Extract a human-readable error message from an API error */
export function extractApiError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as Record<string, unknown> | undefined
    const detail = data?.['detail'] ?? data?.['title'] ?? error.message
    return typeof detail === 'string' ? detail : 'Ocorreu um erro inesperado.'
  }
  if (error instanceof Error) return error.message
  return 'Ocorreu um erro inesperado.'
}
