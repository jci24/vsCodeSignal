import { useCallback, useState } from 'react'

import { ApiError } from '@/api/client'
import { assistantService } from '@/entities/assistant/api/assistantService'
import type {
  IAiActionProposal,
  IAiResponse,
  IAssistantWorkspaceContextRequest,
  IWorkspaceStatePatch,
} from '@/entities/assistant/model/types'

interface UseExecuteAiActionParams {
  context: IAssistantWorkspaceContextRequest | null
  onApplyWorkspacePatch: (patch: IWorkspaceStatePatch) => void
}

export function useExecuteAiAction({
  context,
  onApplyWorkspacePatch,
}: UseExecuteAiActionParams) {
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isExecuting, setIsExecuting] = useState(false)

  const executeAction = useCallback(async (proposal: IAiActionProposal): Promise<IAiResponse | null> => {
    if (!context) {
      return null
    }

    setIsExecuting(true)
    setErrorMessage(null)

    try {
      const response = await assistantService.executeAction({
        ...context,
        confirmed: true,
        proposal,
      })

      if (response.workspacePatch) {
        onApplyWorkspacePatch(response.workspacePatch)
      }

      return response
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message)
      } else {
        setErrorMessage('Assistant action execution failed.')
      }

      return null
    } finally {
      setIsExecuting(false)
    }
  }, [context, onApplyWorkspacePatch])

  return {
    errorMessage,
    executeAction,
    isExecuting,
  }
}
