import { useState, type JSX } from 'react'

import type {
  AssistantAnalysisView,
  IAiActionProposal,
  IAiResponse,
  ISelectionRange,
  IWorkspaceStatePatch,
} from '@/entities/assistant/model/types'
import { useAssistantConversation } from '@/features/ask-assistant/model/useAssistantConversation'
import { useExecuteAiAction } from '@/features/execute-ai-action/model/useExecuteAiAction'
import { useAiActionPlanning } from '@/features/plan-ai-action/model/useAiActionPlanning'
import type { ITransformRecipe } from '@/features/transforms/utils/types'

import { AssistantBriefing } from './AssistantBriefing'
import { AssistantDrawer } from './AssistantDrawer'

interface AssistantPanelProps {
  activeView: AssistantAnalysisView
  compareFileIds: string[]
  fileId: string
  isBriefingCollapsed?: boolean
  onApplyWorkspacePatch: (patch: IWorkspaceStatePatch) => void
  onRequestCollapseRail?: () => void
  selection?: ISelectionRange | null
  transforms: ITransformRecipe
  workspaceId: string
}

export function AssistantPanel({
  activeView,
  compareFileIds,
  fileId,
  isBriefingCollapsed = false,
  onApplyWorkspacePatch,
  onRequestCollapseRail,
  selection,
  transforms,
  workspaceId,
}: AssistantPanelProps): JSX.Element {
  const [executionResponse, setExecutionResponse] = useState<IAiResponse | null>(null)
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)
  const [drawerHeight, setDrawerHeight] = useState(() =>
    typeof window === 'undefined'
      ? 560
      : Math.max(360, Math.round(window.innerHeight * 0.5)),
  )
  const context = {
    activeView,
    compareFileIds,
    fileId,
    selection,
    transforms,
    workspaceId,
  }
  const {
    errorMessage,
    isLoading,
    isSummaryLoading,
    latestResult,
    messages,
    proposal,
    sendPrompt,
    setProposal,
    summaryCard,
  } = useAssistantConversation({
    context,
    onApplyWorkspacePatch,
  })
  const {
    errorMessage: planningError,
    isPlanning,
    planAction,
  } = useAiActionPlanning(context)
  const {
    errorMessage: executionError,
    executeAction,
    isExecuting,
  } = useExecuteAiAction({
    context,
    onApplyWorkspacePatch,
  })

  async function handlePrompt(prompt: string): Promise<void> {
    openDrawer()
    await sendPrompt(prompt)
  }

  async function handlePlanPrompt(prompt: string): Promise<void> {
    openDrawer()
    const nextProposal = await planAction(prompt)

    if (nextProposal) {
      setProposal(nextProposal)
    }
  }

  async function handleConfirmProposal(): Promise<void> {
    if (!proposal) {
      return
    }

    const response = await executeAction(proposal)

    if (response) {
      openDrawer()
      setExecutionResponse(response)
      setProposal(null)
    }
  }

  const visibleProposal: IAiActionProposal | null = proposal
  const combinedError = errorMessage ?? planningError ?? executionError
  const latestAssistantResponse = executionResponse ?? latestResult
  const hasActiveTransforms =
    transforms.normalize ||
    transforms.trimSilence ||
    Math.abs(transforms.gainDb) > 0.01 ||
    transforms.filter.mode !== 'none'
  function openDrawer(): void {
    setIsDrawerOpen(true)
    onRequestCollapseRail?.()
  }

  const drawer = (
    <AssistantDrawer
      drawerHeight={drawerHeight}
      errorMessage={combinedError}
      followUps={latestAssistantResponse?.followUpPrompts}
      isExecuting={isExecuting}
      isLoading={isLoading}
      isOpen={isDrawerOpen}
      messages={messages}
      onCancelProposal={() => setProposal(null)}
      onClose={() => setIsDrawerOpen(false)}
      onConfirmProposal={() => void handleConfirmProposal()}
      onResize={setDrawerHeight}
      onSelectPrompt={async (prompt) => {
        await handlePrompt(prompt)
      }}
      onSubmit={async (prompt) => {
        await handlePrompt(prompt)
      }}
      proposal={visibleProposal}
      response={executionResponse}
    />
  )

  if (isBriefingCollapsed) {
    return <>{drawer}</>
  }

  return (
    <>
      <AssistantBriefing
        isPlanning={isPlanning}
        isSummaryLoading={isSummaryLoading}
        onOpenDrawer={openDrawer}
        onPlanFft={() => void handlePlanPrompt('Switch to FFT view')}
        onPlanHighPassCompare={() => void handlePlanPrompt('Apply a high-pass filter and compare')}
        onQuickCompare={() =>
          void handlePrompt(
            compareFileIds.length > 0
              ? 'What changed between these signals?'
              : hasActiveTransforms
                ? 'What changed after the filter?'
                : 'What changed would be visible if I compared this against another signal?',
          )
        }
        onQuickExplain={() => void handlePrompt('What stands out in this signal?')}
        onQuickRecommend={() => void handlePrompt('What should I inspect next?')}
        summaryCard={summaryCard}
      />

      {drawer}
    </>
  )
}
