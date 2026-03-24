import { useEffect, useRef, useState, type PointerEvent as ReactPointerEvent } from 'react'
import { BotMessageSquare, ChevronDown, X } from 'lucide-react'

import type {
  IAiActionProposal,
  IAiFollowUpPrompt,
  IAiResponse,
} from '@/entities/assistant/model/types'
import type { IAssistantMessage } from '@/features/ask-assistant/model/useAssistantConversation'
import { Button } from '@/shared/ui/button'
import { AssistantActionProposal } from '@/widgets/assistant-action-proposal/ui/AssistantActionProposal'
import { AssistantChat } from '@/widgets/assistant-chat/ui/AssistantChat'
import { AssistantResultSummary } from '@/widgets/assistant-result-summary/ui/AssistantResultSummary'

interface AssistantDrawerProps {
  drawerHeight: number
  errorMessage?: string | null
  followUps?: IAiFollowUpPrompt[]
  isExecuting?: boolean
  isLoading?: boolean
  isOpen: boolean
  messages: IAssistantMessage[]
  onCancelProposal: () => void
  onClose: () => void
  onConfirmProposal: () => void
  onResize: (height: number) => void
  onSelectPrompt: (prompt: string) => Promise<void> | void
  onSubmit: (prompt: string) => Promise<void> | void
  proposal: IAiActionProposal | null
  response: IAiResponse | null
}

interface DragState {
  initialHeight: number
  pointerId: number
  startY: number
}

type DetailPanel = 'proposal' | 'result'

const MIN_DRAWER_HEIGHT = 360

function getMaxDrawerHeight(): number {
  if (typeof window === 'undefined') {
    return 780
  }

  return Math.max(MIN_DRAWER_HEIGHT + 120, Math.round(window.innerHeight * 0.82))
}

function clampDrawerHeight(height: number): number {
  return Math.min(getMaxDrawerHeight(), Math.max(MIN_DRAWER_HEIGHT, Math.round(height)))
}

export function AssistantDrawer({
  drawerHeight,
  errorMessage,
  followUps = [],
  isExecuting = false,
  isLoading = false,
  isOpen,
  messages,
  onCancelProposal,
  onClose,
  onConfirmProposal,
  onResize,
  onSelectPrompt,
  onSubmit,
  proposal,
  response,
}: AssistantDrawerProps) {
  const dragStateRef = useRef<DragState | null>(null)
  const [activeDetailPanel, setActiveDetailPanel] = useState<DetailPanel>('proposal')
  const isCompact = drawerHeight < 560
  const hasProposal = Boolean(proposal)
  const hasResult = Boolean(response?.executionResult)
  const showDetailPanel = hasProposal || hasResult

  useEffect(() => {
    if (proposal) {
      setActiveDetailPanel('proposal')
      return
    }

    if (response?.executionResult) {
      setActiveDetailPanel('result')
      return
    }

    setActiveDetailPanel('proposal')
  }, [proposal, response?.executionResult])

  useEffect(() => {
    function handlePointerMove(event: PointerEvent): void {
      const dragState = dragStateRef.current

      if (!dragState || event.pointerId !== dragState.pointerId) {
        return
      }

      const nextHeight = dragState.initialHeight + (dragState.startY - event.clientY)
      onResize(clampDrawerHeight(nextHeight))
    }

    function handlePointerUp(event: PointerEvent): void {
      if (dragStateRef.current?.pointerId === event.pointerId) {
        dragStateRef.current = null
      }
    }

    function handleWindowResize(): void {
      onResize(clampDrawerHeight(drawerHeight))
    }

    window.addEventListener('pointermove', handlePointerMove)
    window.addEventListener('pointerup', handlePointerUp)
    window.addEventListener('pointercancel', handlePointerUp)
    window.addEventListener('resize', handleWindowResize)

    return () => {
      window.removeEventListener('pointermove', handlePointerMove)
      window.removeEventListener('pointerup', handlePointerUp)
      window.removeEventListener('pointercancel', handlePointerUp)
      window.removeEventListener('resize', handleWindowResize)
    }
  }, [drawerHeight, onResize])

  function handleResizeStart(event: ReactPointerEvent<HTMLButtonElement>): void {
    dragStateRef.current = {
      initialHeight: drawerHeight,
      pointerId: event.pointerId,
      startY: event.clientY,
    }
  }

  if (!isOpen) {
    return null
  }

  return (
    <div className="pointer-events-none fixed inset-x-4 bottom-4 z-50 md:left-28 md:right-6">
      <section className="pointer-events-auto overflow-hidden rounded-[2rem] border border-border/70 bg-background/95 shadow-[0_32px_80px_-28px_rgba(15,23,42,0.42)] backdrop-blur-xl">
        <div className="flex justify-center px-5 pt-3">
          <button
            aria-label="Resize assistant drawer"
            className="group flex items-center gap-2 rounded-full px-3 py-2 text-[11px] font-semibold uppercase tracking-[0.22em] text-muted-foreground transition-colors hover:text-foreground"
            onDoubleClick={() => onResize(clampDrawerHeight(window.innerHeight * 0.5))}
            onPointerDown={handleResizeStart}
            type="button"
          >
            <div className="h-1.5 w-16 rounded-full bg-border/80 transition-colors group-hover:bg-foreground/40" />
            <span className="hidden md:inline">Drag to resize</span>
          </button>
        </div>

        <header className="flex items-center justify-between gap-4 border-b border-border/60 px-5 py-4">
          <div className="flex min-w-0 items-center gap-3">
            <div className="flex size-10 shrink-0 items-center justify-center rounded-2xl bg-foreground text-background">
              <BotMessageSquare className="size-5" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
                Assistant drawer
              </p>
              <p className="truncate text-sm text-foreground">
                Full conversation, action workflow, and evidence-backed summaries.
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <Button
              className="rounded-full"
              onClick={onClose}
              size="sm"
              type="button"
              variant="outline"
            >
              <ChevronDown className="mr-2 size-4" />
              Minimize
            </Button>
            <Button
              className="rounded-full"
              onClick={onClose}
              size="icon"
              type="button"
              variant="ghost"
            >
              <X className="size-4" />
            </Button>
          </div>
        </header>

        <div
          className={
            showDetailPanel
              ? 'grid min-h-0 gap-4 p-4 lg:grid-cols-[minmax(0,1.5fr)_minmax(18rem,24rem)]'
              : 'grid min-h-0 gap-4 p-4'
          }
          style={{
            height: `${clampDrawerHeight(drawerHeight)}px`,
            maxHeight: '82vh',
          }}
        >
          <div className="flex min-h-0 flex-col">
            <AssistantChat
              compact={isCompact}
              errorMessage={errorMessage}
              followUps={followUps}
              isLoading={isLoading || isExecuting}
              messages={messages}
              onSelectPrompt={onSelectPrompt}
              onSubmit={onSubmit}
            />
          </div>

          {showDetailPanel ? (
            <div className="flex min-h-0 flex-col gap-3 overflow-auto pr-1">
              {isCompact && hasProposal && hasResult ? (
                <div className="flex flex-wrap gap-2">
                  <button
                    className={
                      activeDetailPanel === 'proposal'
                        ? 'rounded-full bg-foreground px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-background'
                        : 'rounded-full border border-border/70 bg-background px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground'
                    }
                    onClick={() => setActiveDetailPanel('proposal')}
                    type="button"
                  >
                    Action
                  </button>
                  <button
                    className={
                      activeDetailPanel === 'result'
                        ? 'rounded-full bg-foreground px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-background'
                        : 'rounded-full border border-border/70 bg-background px-3 py-1.5 text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground'
                    }
                    onClick={() => setActiveDetailPanel('result')}
                    type="button"
                  >
                    Result
                  </button>
                </div>
              ) : null}

              {isCompact ? (
              <div className="flex min-h-0 flex-1 flex-col gap-3">
                <div className="min-h-0 flex-1 overflow-auto">
                  {activeDetailPanel === 'proposal' && hasProposal ? (
                    <AssistantActionProposal
                      isExecuting={isExecuting}
                      onCancel={onCancelProposal}
                      onConfirm={onConfirmProposal}
                      proposal={proposal}
                    />
                  ) : null}

                  {activeDetailPanel === 'result' && hasResult ? (
                    <AssistantResultSummary response={response} />
                  ) : null}
                </div>
              </div>
            ) : (
              <>
                <AssistantActionProposal
                  isExecuting={isExecuting}
                  onCancel={onCancelProposal}
                  onConfirm={onConfirmProposal}
                  proposal={proposal}
                />

                <AssistantResultSummary response={response} />
              </>
              )}
            </div>
          ) : null}
        </div>
      </section>
    </div>
  )
}
