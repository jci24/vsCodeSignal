import type { ISpectrogramRequest, ISpectrogramResponse } from '../utils/types'

import { apiClient, HttpMethod } from '@/api/client'
import { API_ENDPOINTS } from '@/api/endpoints'

export const spectrogramService = {
  async getSpectrogram(request: ISpectrogramRequest): Promise<ISpectrogramResponse> {
    return apiClient.request<ISpectrogramResponse>(API_ENDPOINTS.ANALYSIS.SPECTROGRAM, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 20_000,
    })
  },
}
