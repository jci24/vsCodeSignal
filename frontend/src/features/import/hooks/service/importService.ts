import type { IImportRequest, IImportResponse } from '../../utils/types'

import { apiClient, HttpMethod } from '@/api/client'
import { API_ENDPOINTS } from '@/api/endpoints'

export const importFiles = async (request: IImportRequest): Promise<IImportResponse> => {
  return apiClient.request<IImportResponse>(API_ENDPOINTS.IMPORT.IMPORT, {
    body: request,
    method: HttpMethod.POST,
    timeoutMs: 20_000,
  })
}

export const uploadFiles = async (files: File[]): Promise<IImportResponse> => {
  const formData = new FormData()

  files.forEach((file) => {
    formData.append('files', file)
  })

  return apiClient.request<IImportResponse>(API_ENDPOINTS.IMPORT.IMPORT, {
    body: formData,
    method: HttpMethod.POST,
    timeoutMs: 20_000,
  })
}
