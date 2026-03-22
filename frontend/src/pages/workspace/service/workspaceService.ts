import type { IWorkspaceImportsResponse } from '../utils/types'

import { apiClient, HttpMethod } from '@/api/client'
import { API_ENDPOINTS } from '@/api/endpoints'

export const workspaceService = {
  async getCurrentImports(): Promise<IWorkspaceImportsResponse> {
    return apiClient.request<IWorkspaceImportsResponse>(
      API_ENDPOINTS.WORKSPACES.CURRENT_IMPORTS,
      {
        method: HttpMethod.GET,
        timeoutMs: 20_000,
      },
    )
  },
}
