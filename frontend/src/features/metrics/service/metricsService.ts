import type { IMetricsRequest, IMetricsResponse } from '../utils/types'

import { apiClient, HttpMethod } from '@/api/client'
import { API_ENDPOINTS } from '@/api/endpoints'

export const metricsService = {
  async getMetrics(request: IMetricsRequest): Promise<IMetricsResponse> {
    return apiClient.request<IMetricsResponse>(API_ENDPOINTS.ANALYSIS.METRICS, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 20_000,
    })
  },
}
