import { useCallback, useEffect, useMemo, useRef, useState } from 'react'

import { ApiError } from '@/api/client'
import { assistantService } from '@/entities/assistant/api/assistantService'
import type {
  IAiActionProposal,
  IAiConversationTurn,
  IAiResponse,
  IAiSummaryCard,
  IAssistantWorkspaceContextRequest,
  IWorkspaceStatePatch,
} from '@/entities/assistant/model/types'
import { serializeTransformRecipe } from '@/features/transforms/utils/types'

export interface IAssistantMessage {
  content: string
  id: string
  role: 'assistant' | 'user'
  status?: IAiResponse['status']
}

interface UseAssistantConversationParams {
  context: IAssistantWorkspaceContextRequest | null
  onApplyWorkspacePatch: (patch: IWorkspaceStatePatch) => void
}

function createId(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

function buildContextKey(context: IAssistantWorkspaceContextRequest | null): string {
  if (!context) {
    return 'none'
  }

  return [
    context.workspaceId,
    context.fileId,
    context.activeView,
    [...context.compareFileIds].sort().join(','),
    context.selection?.startSeconds ?? '',
    context.selection?.endSeconds ?? '',
    serializeTransformRecipe(context.transforms),
  ].join(':')
}

function toConversationTurns(messages: IAssistantMessage[]): IAiConversationTurn[] {
  return messages.slice(-8).map((message) => ({
    content: message.content,
    role: message.role,
  }))
}

export function useAssistantConversation({
  context,
  onApplyWorkspacePatch,
}: UseAssistantConversationParams) {
  const [messages, setMessages] = useState<IAssistantMessage[]>([])
  const [workspaceSummaryCard, setWorkspaceSummaryCard] = useState<IAiSummaryCard | null>(null)
  const [proposal, setProposal] = useState<IAiActionProposal | null>(null)
  const [latestResult, setLatestResult] = useState<IAiResponse | null>(null)
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(false)
  const [isSummaryLoading, setIsSummaryLoading] = useState(false)
  const contextKey = useMemo(() => buildContextKey(context), [context])
  const contextRef = useRef<IAssistantWorkspaceContextRequest | null>(context)
  const fileScopeKey = context
    ? [
        context.workspaceId,
        context.fileId,
        [...context.compareFileIds].sort().join(','),
        context.selection?.startSeconds ?? '',
        context.selection?.endSeconds ?? '',
      ].join(':')
    : 'none'
  const previousFileScopeKeyRef = useRef(fileScopeKey)

  useEffect(() => {
    contextRef.current = context
  }, [context])

  useEffect(() => {
    if (previousFileScopeKeyRef.current !== fileScopeKey) {
      setMessages([])
      setProposal(null)
      setLatestResult(null)
      setErrorMessage(null)
      previousFileScopeKeyRef.current = fileScopeKey
    }

    const activeContext = contextRef.current

    if (!activeContext) {
      setWorkspaceSummaryCard(null)
      setIsSummaryLoading(false)
      return
    }

    let isCancelled = false
    setIsSummaryLoading(true)

    void assistantService
      .getSummary(activeContext)
      .then((result) => {
        if (!isCancelled) {
          setWorkspaceSummaryCard(result)
        }
      })
      .catch((error) => {
        if (isCancelled) {
          return
        }

        if (error instanceof ApiError) {
          setErrorMessage(error.message)
        } else {
          setErrorMessage('Assistant summary unavailable.')
        }
      })
      .finally(() => {
        if (!isCancelled) {
          setIsSummaryLoading(false)
        }
      })

    return () => {
      isCancelled = true
    }
  }, [contextKey, fileScopeKey])

  const sendPrompt = useCallback(async (prompt: string): Promise<IAiResponse | null> => {
    if (!context) {
      return null
    }

    const trimmed = prompt.trim()

    if (!trimmed) {
      return null
    }

    const userMessage: IAssistantMessage = {
      content: trimmed,
      id: createId(),
      role: 'user',
    }
    const nextMessages = [...messages, userMessage]
    setMessages(nextMessages)
    setIsLoading(true)
    setErrorMessage(null)

    try {
      const response = await assistantService.ask({
        ...context,
        history: toConversationTurns(messages),
        prompt: trimmed,
      })

      setMessages((current) => [
        ...current,
        {
          content: response.message,
          id: createId(),
          role: 'assistant',
          status: response.status,
        },
      ])
      setProposal(response.actionProposal ?? null)
      setLatestResult(response)

      if (response.workspacePatch) {
        onApplyWorkspacePatch(response.workspacePatch)
      }

      return response
    } catch (error) {
      if (error instanceof ApiError) {
        setErrorMessage(error.message)
      } else {
        setErrorMessage('Assistant request failed.')
      }
      return null
    } finally {
      setIsLoading(false)
    }
  }, [context, messages, onApplyWorkspacePatch])

  return {
    errorMessage,
    isLoading,
    isSummaryLoading,
    latestResult,
    messages,
    proposal,
    sendPrompt,
    setProposal,
    summaryCard: workspaceSummaryCard,
  }
}
