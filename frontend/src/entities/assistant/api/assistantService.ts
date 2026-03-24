import { apiClient, HttpMethod } from '@/api/client'
import { API_ENDPOINTS } from '@/api/endpoints'

import type {
  IAiActionProposal,
  IAiRequest,
  IAiResponse,
  IAiSummaryCard,
  IAssistantWorkspaceContext,
  IAssistantWorkspaceContextRequest,
  IExecuteActionRequest,
  IPlanActionRequest,
} from '../model/types'

export const assistantService = {
  async ask(request: IAiRequest): Promise<IAiResponse> {
    return apiClient.request<IAiResponse>(API_ENDPOINTS.AI.ASK, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 45_000,
    })
  },

  async executeAction(request: IExecuteActionRequest): Promise<IAiResponse> {
    return apiClient.request<IAiResponse>(API_ENDPOINTS.AI.EXECUTE_ACTION, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 45_000,
    })
  },

  async getContext(request: IAssistantWorkspaceContextRequest): Promise<IAssistantWorkspaceContext> {
    const query = new URLSearchParams({
      activeView: request.activeView,
      fileId: request.fileId,
    })

    if (request.compareFileIds.length > 0) {
      query.set('compareFileIds', request.compareFileIds.join(','))
    }

    return apiClient.request<IAssistantWorkspaceContext>(
      `${API_ENDPOINTS.AI.CONTEXT(request.workspaceId)}?${query.toString()}`,
      {
        method: HttpMethod.GET,
        timeoutMs: 20_000,
      },
    )
  },

  async getSummary(request: IAssistantWorkspaceContextRequest): Promise<IAiSummaryCard> {
    return apiClient.request<IAiSummaryCard>(API_ENDPOINTS.AI.SUMMARY, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 45_000,
    })
  },

  async planAction(request: IPlanActionRequest): Promise<IAiActionProposal> {
    return apiClient.request<IAiActionProposal>(API_ENDPOINTS.AI.PLAN_ACTION, {
      body: request,
      method: HttpMethod.POST,
      timeoutMs: 45_000,
    })
  },
}
