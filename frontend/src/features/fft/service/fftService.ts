import type { IFftRequest, IFftResponse } from '../utils/types'

import { apiClient, HttpMethod } from '@/api/client'
import { API_ENDPOINTS } from '@/api/endpoints'

export const fftService = {
  async getFft(request: IFftRequest): Promise<IFftResponse> {
    return apiClient.request<IFftResponse>(API_ENDPOINTS.ANALYSIS.FFT, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 20_000,
    })
  },
}
