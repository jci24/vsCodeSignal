import { useCallback, useState } from 'react'

import { ApiError } from '@/api/client'
import { assistantService } from '@/entities/assistant/api/assistantService'
import type {
  IAiActionProposal,
  IAssistantWorkspaceContextRequest,
} from '@/entities/assistant/model/types'

export function useAiActionPlanning(context: IAssistantWorkspaceContextRequest | null) {
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isPlanning, setIsPlanning] = useState(false)

  const planAction = useCallback(async (prompt: string): Promise<IAiActionProposal | null> => {
    if (!context) {
      return null
    }

    setIsPlanning(true)
    setErrorMessage(null)

    try {
      return await assistantService.planAction({
        ...context,
        prompt,
      })
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message)
      } else {
        setErrorMessage('Assistant action planning failed.')
      }

      return null
    } finally {
      setIsPlanning(false)
    }
  }, [context])

  return {
    errorMessage,
    isPlanning,
    planAction,
  }
}
