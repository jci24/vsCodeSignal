import type { IWaveformRequest, IWaveformResponse } from '../utils/types'

import { apiClient, HttpMethod } from '@/api/client'
import { API_ENDPOINTS } from '@/api/endpoints'

export const waveformService = {
  async getWaveform(request: IWaveformRequest): Promise<IWaveformResponse> {
    return apiClient.request<IWaveformResponse>(API_ENDPOINTS.ANALYSIS.WAVEFORM, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 20_000,
    })
  },
}
