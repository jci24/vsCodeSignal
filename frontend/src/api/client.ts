export enum HttpMethod {
  DELETE = 'DELETE',
  GET = 'GET',
  PATCH = 'PATCH',
  POST = 'POST',
  PUT = 'PUT',
}

type RequestBody = BodyInit | object

interface IRequestOptions {
  body?: RequestBody
  headers?: HeadersInit
  method: HttpMethod
  timeoutMs?: number
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

function isFormData(value: unknown): value is FormData {
  return typeof FormData !== 'undefined' && value instanceof FormData
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === 'object' && !Array.isArray(value)
}

function isBodyInit(value: unknown): value is BodyInit {
  return (
    typeof value === 'string' ||
    value instanceof Blob ||
    value instanceof URLSearchParams ||
    value instanceof ReadableStream ||
    value instanceof ArrayBuffer ||
    ArrayBuffer.isView(value)
  )
}

function parseErrorMessage(payload: unknown, fallbackMessage: string) {
  if (!isPlainObject(payload)) {
    return fallbackMessage
  }

  if (typeof payload.message === 'string' && payload.message.trim()) {
    return payload.message
  }

  if (!isPlainObject(payload.errors)) {
    return fallbackMessage
  }

  const messages = Object.values(payload.errors)
    .flatMap((value) => {
      if (Array.isArray(value)) {
        return value.filter((entry): entry is string => typeof entry === 'string')
      }

      return typeof value === 'string' ? [value] : []
    })
    .filter(Boolean)

  return messages.length > 0 ? messages.join(' ') : fallbackMessage
}

export const apiClient = {
  async request<TResponse>(endpoint: string, options: IRequestOptions): Promise<TResponse> {
    const headers = new Headers(options.headers)
    const controller = new AbortController()
    const timeoutMs = options.timeoutMs ?? 20_000
    const timeoutId = window.setTimeout(() => controller.abort(), timeoutMs)
    let body: BodyInit | undefined

    if (isFormData(options.body)) {
      body = options.body as BodyInit
    } else if (isPlainObject(options.body)) {
      headers.set('Content-Type', 'application/json')
      body = JSON.stringify(options.body)
    } else if (isBodyInit(options.body)) {
      body = options.body
    }

    try {
      const response = await fetch(endpoint, {
        body,
        headers,
        method: options.method,
        signal: controller.signal,
      })

      const payload = await response.json().catch(() => null)

      if (!response.ok) {
        throw new ApiError(
          parseErrorMessage(payload, 'Request failed. Please try again.'),
          response.status,
        )
      }

      return payload as TResponse
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') {
        throw new ApiError('The request timed out. Check the backend and try again.', 408)
      }

      throw error
    } finally {
      window.clearTimeout(timeoutId)
    }
  },
}
