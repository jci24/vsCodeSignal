import type { JSX } from 'react'
import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import {
  ArrowLeftRight,
  ArrowUpRight,
  Copy,
  Download,
  RefreshCw,
} from 'lucide-react'
import { Link, useSearchParams } from 'react-router-dom'

import type {
  AssistantAnalysisView,
  IAiFollowUpPrompt,
  IAiResponse,
  IWorkspaceStatePatch,
} from '@/entities/assistant/model/types'
import { useAssistantConversation } from '@/features/ask-assistant/model/useAssistantConversation'
import { useExecuteAiAction } from '@/features/execute-ai-action/model/useExecuteAiAction'
import { FftPanel } from '@/features/fft/components/FftPanel/FftPanel'
import { useAiActionPlanning } from '@/features/plan-ai-action/model/useAiActionPlanning'
import { SpectrogramPanel } from '@/features/spectrogram/components/SpectrogramPanel/SpectrogramPanel'
import { useSignalTransforms } from '@/features/transforms/hooks/useSignalTransforms'
import { WaveformPanel } from '@/features/waveform/components/WaveformPanel/WaveformPanel'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { LoadingSpinner } from '@/shared/ui/loading-spinner'
import { AssistantActionProposal } from '@/widgets/assistant-action-proposal/ui/AssistantActionProposal'
import { AssistantChat } from '@/widgets/assistant-chat/ui/AssistantChat'
import { AssistantResultSummary } from '@/widgets/assistant-result-summary/ui/AssistantResultSummary'

import { useWorkspaceImports } from '@/pages/workspace/hooks/useWorkspaceImports'
import type { IWorkspaceImportedFile } from '@/pages/workspace/utils/types'
import type { IAiSummaryCard } from '@/entities/assistant/model/types'

const VIEW_OPTIONS: Array<{ id: AssistantAnalysisView; label: string }> = [
  { id: 'waveform', label: 'Waveform' },
  { id: 'fft', label: 'FFT' },
  { id: 'spectrogram', label: 'Spectrogram' },
]

export function ComparePage(): JSX.Element {
  const scrollRootRef = useRef<HTMLDivElement | null>(null)
  const followUpSectionRef = useRef<HTMLElement | null>(null)
  const actionSectionRef = useRef<HTMLElement | null>(null)
  const [searchParams, setSearchParams] = useSearchParams()
  const [activeView, setActiveView] = useState<AssistantAnalysisView>('waveform')
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'error'>('idle')
  const [executionResponse, setExecutionResponse] = useState<IAiResponse | null>(null)
  const [hasPinnedView, setHasPinnedView] = useState(false)
  const [isFollowUpExpanded, setIsFollowUpExpanded] = useState(false)
  const [isPairingExpanded, setIsPairingExpanded] = useState(false)
  const {
    batches,
    errorMessage,
    isLoading,
    reloadWorkspace,
    selectedFile,
    workspaceId,
  } = useWorkspaceImports()

  const importedFiles = useMemo(
    () => batches.flatMap((batch) => batch.importedFiles),
    [batches],
  )
  const audioFiles = useMemo(
    () => importedFiles.filter((file) => file.signalKind === 'audio'),
    [importedFiles],
  )
  const sortedAudioFiles = useMemo(
    () =>
      [...audioFiles].sort((left, right) => {
        const leftTime = Date.parse(left.importedAtUtc)
        const rightTime = Date.parse(right.importedAtUtc)

        if (leftTime !== rightTime) {
          return leftTime - rightTime
        }

        return left.sourcePath.localeCompare(right.sourcePath)
      }),
    [audioFiles],
  )
  const baselineFile = selectedFile?.signalKind === 'audio' ? selectedFile : null
  const requestedCandidateId = searchParams.get('candidate')
  const candidateFile = useMemo(
    () =>
      baselineFile
        ? audioFiles.find((file) => file.id === requestedCandidateId && file.id !== baselineFile.id) ?? null
        : null,
    [audioFiles, baselineFile, requestedCandidateId],
  )
  const suggestedBaseline = baselineFile ?? sortedAudioFiles[0] ?? null
  const suggestedCandidate = useMemo(
    () => pickSuggestedCandidate(sortedAudioFiles, suggestedBaseline),
    [sortedAudioFiles, suggestedBaseline],
  )
  const {
    activeTransforms,
    setRecipeForFile,
  } = useSignalTransforms(baselineFile?.id ?? null)
  const context =
    baselineFile && candidateFile
      ? {
          activeView,
          compareFileIds: [candidateFile.id],
          fileId: baselineFile.id,
          transforms: activeTransforms,
          workspaceId,
        }
      : null
  const {
    errorMessage: assistantError,
    isLoading: isAssistantLoading,
    isSummaryLoading,
    latestResult,
    messages,
    proposal,
    sendPrompt,
    setProposal,
    summaryCard,
  } = useAssistantConversation({
    context,
    onApplyWorkspacePatch: applyWorkspacePatch,
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
    onApplyWorkspacePatch: applyWorkspacePatch,
  })

  const combinedError = assistantError ?? planningError ?? executionError
  const followUps = latestResult?.followUpPrompts?.length
    ? latestResult.followUpPrompts
    : buildSummaryFollowUps(summaryCard?.nextSteps ?? [])
  const hasAudioBaseline = Boolean(baselineFile)
  const hasComparePair = Boolean(hasAudioBaseline && candidateFile)
  const recommendedView = summaryCard?.recommendedView ?? null
  const recommendedNextStep =
    summaryCard?.recommendedNextStep || summaryCard?.nextSteps[0] || ''
  const isUsingSuggestedPair =
    baselineFile?.id === suggestedBaseline?.id &&
    candidateFile?.id === suggestedCandidate?.id
  const inspectHref = baselineFile
    ? buildInspectHref(baselineFile, candidateFile, activeView)
    : '/inspect'
  const shareSummary = useMemo(
    () => buildShareSummary(baselineFile, candidateFile, summaryCard, activeView),
    [activeView, baselineFile, candidateFile, summaryCard],
  )

  function applyWorkspacePatch(patch: IWorkspaceStatePatch): void {
    if (patch.activeView) {
      setActiveView(patch.activeView)
      setHasPinnedView(true)
    }

    if (patch.compareFileIds) {
      updateCompareParams({
        baseline: baselineFile,
        candidateId: patch.compareFileIds[0] ?? null,
      })
    }

    if (patch.targetFileId && patch.transforms) {
      setRecipeForFile(patch.targetFileId, patch.transforms)
    }
  }

  useEffect(() => {
    if (!selectedFile && sortedAudioFiles.length > 0) {
      updateCompareParams({
        baseline: sortedAudioFiles[0],
        candidateId: requestedCandidateId,
      })
      return
    }

    if (selectedFile && selectedFile.signalKind !== 'audio' && sortedAudioFiles.length > 0) {
      updateCompareParams({
        baseline: sortedAudioFiles[0],
        candidateId: requestedCandidateId,
      })
    }
  }, [requestedCandidateId, selectedFile, setSearchParams, sortedAudioFiles])

  useEffect(() => {
    if (!baselineFile || candidateFile || !suggestedCandidate) {
      return
    }

    updateCompareParams({
      baseline: baselineFile,
      candidateId: suggestedCandidate.id,
    })
  }, [baselineFile, candidateFile, suggestedCandidate])

  useEffect(() => {
    setExecutionResponse(null)
    setProposal(null)
    setHasPinnedView(false)
    setActiveView('waveform')
  }, [baselineFile?.id, candidateFile?.id, setProposal])

  useEffect(() => {
    if (!hasPinnedView && recommendedView) {
      setActiveView(recommendedView)
    }
  }, [hasPinnedView, recommendedView])

  const handleBaselineChange = (nextFileId: string): void => {
    const nextBaseline = sortedAudioFiles.find((file) => file.id === nextFileId) ?? null

    if (!nextBaseline) {
      return
    }

    const nextCandidateId =
      candidateFile?.id && candidateFile.id !== nextBaseline.id ? candidateFile.id : null

    updateCompareParams({
      baseline: nextBaseline,
      candidateId:
        nextCandidateId ??
        pickSuggestedCandidate(sortedAudioFiles, nextBaseline)?.id ??
        null,
    })
  }

  const handleCandidateChange = (nextFileId: string): void => {
    updateCompareParams({
      baseline: baselineFile,
      candidateId: nextFileId || null,
    })
  }

  const handleChartViewChange = (view: AssistantAnalysisView): void => {
    setHasPinnedView(true)
    setActiveView(view)
  }

  const handlePrompt = async (prompt: string): Promise<void> => {
    setExecutionResponse(null)
    await sendPrompt(prompt)
  }

  const handlePlanPrompt = async (prompt: string): Promise<void> => {
    setExecutionResponse(null)
    const nextProposal = await planAction(prompt)

    if (nextProposal) {
      setProposal(nextProposal)
    }
  }

  const handleGuidedStep = async (prompt: string): Promise<void> => {
    if (!prompt.trim()) {
      return
    }

    setIsFollowUpExpanded(true)
    window.requestAnimationFrame(() => {
      followUpSectionRef.current?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      })
    })

    if (isActionLike(prompt)) {
      await handlePlanPrompt(prompt)
      return
    }

    await handlePrompt(prompt)
  }

  const handleConfirmProposal = async (): Promise<void> => {
    if (!proposal) {
      return
    }

    const response = await executeAction(proposal)

    if (response) {
      setExecutionResponse(response)
      setProposal(null)
    }
  }

  const handleUseSuggestedPair = (): void => {
    if (!suggestedBaseline || !suggestedCandidate) {
      return
    }

    updateCompareParams({
      baseline: suggestedBaseline,
      candidateId: suggestedCandidate.id,
    })
  }

  const handleSwapPair = (): void => {
    if (!baselineFile || !candidateFile) {
      return
    }

    updateCompareParams({
      baseline: candidateFile,
      candidateId: baselineFile.id,
    })
  }

  useEffect(() => {
    if (copyState === 'idle') {
      return
    }

    const timer = window.setTimeout(() => setCopyState('idle'), 2200)
    return () => window.clearTimeout(timer)
  }, [copyState])

  useEffect(() => {
    setIsPairingExpanded(!hasComparePair)
  }, [hasComparePair])

  useEffect(() => {
    if (!hasComparePair) {
      setIsFollowUpExpanded(false)
      return
    }

    if (messages.length > 0 || isAssistantLoading || isPlanning) {
      setIsFollowUpExpanded(true)
    }
  }, [hasComparePair, isAssistantLoading, isPlanning, messages.length])

  useLayoutEffect(() => {
    if (!hasComparePair) {
      return
    }

    const scrollContainers: HTMLElement[] = []
    const root = scrollRootRef.current

    if (root) {
      scrollContainers.push(root)

      let parent = root.parentElement
      while (parent) {
        const overflowY = window.getComputedStyle(parent).overflowY
        const isScrollable =
          (overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay') &&
          parent.scrollHeight > parent.clientHeight

        if (isScrollable) {
          scrollContainers.push(parent)
        }

        parent = parent.parentElement
      }
    }

    const resetToTop = () => {
      scrollContainers.forEach((container) => {
        container.scrollTop = 0
      })

      window.scrollTo(0, 0)
    }

    resetToTop()
    const frameId = window.requestAnimationFrame(() => {
      resetToTop()
    })

    return () => {
      window.cancelAnimationFrame(frameId)
    }
  }, [hasComparePair, summaryCard?.primaryFinding])

  useEffect(() => {
    if (!proposal && !executionResponse) {
      return
    }

    window.requestAnimationFrame(() => {
      actionSectionRef.current?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      })
    })
  }, [executionResponse, proposal])

  if (isLoading) {
    return (
      <div className="flex h-full min-h-0 flex-1 items-center justify-center rounded-[2rem] border border-border/70 bg-background/80">
        <LoadingSpinner label="Loading compare workspace" size="lg" />
      </div>
    )
  }

  if (errorMessage) {
    return (
      <div className="flex h-full min-h-0 flex-1 items-center justify-center">
        <Card className="max-w-xl rounded-[2rem] border border-border/70 bg-background/90">
          <CardHeader>
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
              Compare
            </p>
            <CardTitle>Workspace imports are unavailable</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <p className="text-sm leading-6 text-muted-foreground">{errorMessage}</p>
            <Button onClick={() => void reloadWorkspace()} type="button" variant="outline">
              <RefreshCw className="mr-2 size-4" />
              Retry
            </Button>
          </CardContent>
        </Card>
      </div>
    )
  }

  if (batches.length === 0) {
    return (
      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto pr-2">
        <CompareEmptyState
          description="Import a baseline and a candidate signal to get an AI-guided comparison."
          title="Start with two signals"
        />
      </div>
    )
  }

  if (audioFiles.length === 0) {
    return (
      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto pr-2">
        <CompareEmptyState
          description="This guided compare flow is audio-first right now. Import audio files or open Inspect for the full technical workspace."
          title="Audio compare only in this first slice"
        />
      </div>
    )
  }

  const briefingHeadline =
    summaryCard?.primaryFinding ?? 'Choose a baseline and candidate to generate the guided comparison.'
  const briefingSummary =
    summaryCard?.summary && summaryCard.summary !== summaryCard.primaryFinding
      ? summaryCard.summary
      : null
  const briefingImpact =
    summaryCard?.impactSummary ?? 'The product will explain why the current difference matters.'
  const evidenceFacts = summaryCard?.keyFacts.slice(0, 3) ?? []
  const evidenceObservations = summaryCard?.topObservations.slice(0, 2) ?? []
  const guidancePrompts = followUps.slice(0, 3)
  const groundingLabel =
    latestResult?.context?.selectionScope && latestResult.context.selectionScope !== 'full-file'
      ? latestResult.context.selectionScope
      : 'full-file analysis'
  const analysisBasis =
    latestResult?.context?.analysisBasis ?? 'Grounded in computed decode metrics and backend observations.'
  const showPairingEditor = !hasComparePair || isPairingExpanded
  const hasResolvedSummary = Boolean(summaryCard)
  const showFollowUpPanel =
    hasComparePair &&
    hasResolvedSummary &&
    (isFollowUpExpanded || messages.length > 0 || isAssistantLoading || isPlanning)
  const briefingSection = hasComparePair ? (
    hasResolvedSummary ? (
      <section className="overflow-hidden rounded-[2.2rem] border border-border/70 bg-[linear-gradient(135deg,rgba(255,249,240,0.98),rgba(255,255,255,0.98)_46%,rgba(241,250,248,0.92))] shadow-[0_28px_80px_-58px_rgba(15,23,42,0.34)]">
        <div className="px-6 py-6 md:px-8 md:py-8">
          <div className="space-y-6">
            <div className="flex flex-wrap items-center gap-2 text-xs text-slate-600">
              <span className="inline-flex items-center gap-2 rounded-full border border-slate-900/10 bg-white/82 px-4 py-2 font-semibold uppercase tracking-[0.22em]">
                <ArrowLeftRight className="size-3.5" />
                AI briefing
              </span>
              <span className="rounded-full border border-slate-900/10 bg-white/65 px-4 py-2">
                {summaryCard?.mode === 'comparison' ? 'Baseline vs candidate' : 'Single signal'}
              </span>
              <span className="rounded-full border border-slate-900/10 bg-white/65 px-4 py-2">
                {groundingLabel}
              </span>
            </div>

            <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_18.5rem] xl:items-start">
              <div className="space-y-4">
                <div className="space-y-2">
                  <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-slate-500">
                    What changed
                  </p>
                  <h3 className="max-w-4xl text-[1.65rem] font-semibold leading-[1.08] tracking-tight text-slate-950 md:text-[1.95rem]">
                    {briefingHeadline}
                  </h3>
                  {briefingSummary ? (
                    <p className="max-w-4xl text-sm leading-6 text-slate-600">
                      {briefingSummary}
                    </p>
                  ) : null}
                </div>

                <article className="rounded-[1.45rem] border border-slate-900/10 bg-white/86 p-4 shadow-[0_16px_40px_-40px_rgba(15,23,42,0.28)] md:p-6">
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">
                    Why it matters
                  </p>
                  <p className="mt-2 text-sm leading-6 text-slate-700">{briefingImpact}</p>
                </article>

                <p className="text-xs leading-5 text-slate-500">
                  <span className="font-semibold uppercase tracking-[0.16em] text-slate-500">
                    Grounding
                  </span>{' '}
                  {analysisBasis}
                </p>
              </div>

              <article className="rounded-[1.45rem] border border-slate-900/10 bg-slate-950 p-4 text-white shadow-[0_18px_44px_-36px_rgba(15,23,42,0.45)] md:p-6">
                <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-white/60">
                  What to inspect next
                </p>
                <p className="mt-2 text-sm leading-6 text-white/88">
                  {recommendedNextStep || 'The assistant will suggest one supported next step here.'}
                </p>
                <div className="mt-4 flex flex-wrap gap-2">
                  <Button
                    className="rounded-full bg-white text-slate-950 hover:bg-white/90"
                    disabled={!recommendedNextStep}
                    onClick={() =>
                      void handleGuidedStep(
                        recommendedNextStep || 'What should I inspect next?',
                      )
                    }
                    size="sm"
                    type="button"
                    variant="secondary"
                  >
                    Run recommended step
                  </Button>
                  <Button
                    className="rounded-full border-white/20 bg-transparent text-white hover:bg-white/10"
                    onClick={() => setIsFollowUpExpanded(true)}
                    size="sm"
                    type="button"
                    variant="outline"
                  >
                    Ask follow-up
                  </Button>
                </div>
              </article>
            </div>
          </div>
        </div>
      </section>
    ) : (
      <section className="rounded-[1.8rem] border border-border/70 bg-[linear-gradient(135deg,rgba(255,249,240,0.98),rgba(255,255,255,0.98)_46%,rgba(241,250,248,0.92))] shadow-[0_24px_70px_-56px_rgba(15,23,42,0.3)]">
        <div className="grid gap-4 px-6 py-6 md:px-8 md:py-6 lg:grid-cols-[minmax(0,1fr)_minmax(280px,0.72fr)]">
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2 text-xs text-slate-600">
              <span className="inline-flex items-center gap-2 rounded-full border border-slate-900/10 bg-white/82 px-4 py-2 font-semibold uppercase tracking-[0.22em]">
                <ArrowLeftRight className="size-3.5" />
                AI briefing
              </span>
              <span className="rounded-full border border-slate-900/10 bg-white/65 px-4 py-2">
                Preparing compare summary
              </span>
            </div>
            <div className="space-y-2">
              <p className="text-[11px] font-semibold uppercase tracking-[0.22em] text-slate-500">
                Guided summary
              </p>
              <h3 className="text-[1.5rem] font-semibold leading-tight tracking-tight text-slate-950">
                {isSummaryLoading ? 'Generating the grounded comparison now.' : 'The compare briefing is not ready yet.'}
              </h3>
              <p className="max-w-3xl text-sm leading-6 text-slate-600">
                {isSummaryLoading
                  ? 'The backend is measuring the two signals and shaping the first plain-language explanation.'
                  : 'Try adjusting the pair or asking again once the compare context finishes loading.'}
              </p>
            </div>
          </div>

          <div className="rounded-[1.4rem] border border-dashed border-slate-900/12 bg-white/68 p-4">
            {isSummaryLoading ? (
              <LoadingSpinner label="Preparing compare briefing" />
            ) : (
              <p className="text-sm leading-6 text-slate-600">
                Once the compare summary is ready, this area will expand into the full explanation, impact, next step, and follow-up workflow.
              </p>
            )}
          </div>
        </div>
      </section>
    )
  ) : null

  const followUpSection = showFollowUpPanel ? (
    <section ref={followUpSectionRef}>
      <Card className="rounded-[1.55rem] border border-border/70 bg-background/92 shadow-[0_14px_38px_-34px_rgba(15,23,42,0.2)]">
        <CardContent className="space-y-4 p-4 md:p-6">
          <div className="flex flex-wrap items-start justify-between gap-4">
            <div className="space-y-2">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                Follow-up
              </p>
              <p className="text-sm leading-6 text-muted-foreground">
                Keep the answer moving without letting the follow-up workflow dominate the page.
              </p>
            </div>
            <Button
              className="rounded-full"
              onClick={() => setIsFollowUpExpanded(false)}
              size="sm"
              type="button"
              variant="outline"
            >
              Hide follow-up
            </Button>
          </div>

          <AssistantChat
            compact
            description="Probe the main finding, ask why it matters, or request one supported step."
            emptyStateMessage="Ask one grounded follow-up about what changed, why it matters, or what to inspect next."
            errorMessage={combinedError}
            eyebrow="Follow-up"
            followUps={guidancePrompts}
            isLoading={isAssistantLoading || isPlanning}
            messages={messages}
            onSelectPrompt={async (prompt) => {
              await handleGuidedStep(prompt)
            }}
            onSubmit={async (prompt) => {
              await handleGuidedStep(prompt)
            }}
            scrollable
            title="Keep the answer moving"
          />
        </CardContent>
      </Card>
    </section>
  ) : hasComparePair && hasResolvedSummary ? (
    <section ref={followUpSectionRef}>
      <Card className="rounded-[1.4rem] border border-border/70 bg-background/88 shadow-[0_12px_30px_-30px_rgba(15,23,42,0.14)]">
        <CardContent className="flex flex-wrap items-center justify-between gap-4 p-4 md:px-6">
          <div className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Follow-up
            </p>
            <p className="text-sm leading-6 text-muted-foreground">
              Ask one grounded follow-up or take the recommended next step when you want more detail.
            </p>
          </div>
          <div className="flex flex-wrap gap-2">
            {guidancePrompts.slice(0, 2).map((item) => (
              <Button
                className="rounded-full"
                key={item.id}
                onClick={() => void handleGuidedStep(item.prompt)}
                size="sm"
                type="button"
                variant="outline"
              >
                {item.label}
              </Button>
            ))}
            <Button
              className="rounded-full"
              onClick={() => setIsFollowUpExpanded(true)}
              size="sm"
              type="button"
              variant="outline"
            >
              Open follow-up
            </Button>
          </div>
        </CardContent>
      </Card>
    </section>
  ) : null

  return (
    <div
      className="flex min-h-fit flex-1 flex-col gap-6 pr-2"
      ref={scrollRootRef}
      style={{ overflowAnchor: 'none' }}
    >
      {briefingSection}
      {followUpSection}

      <section>
        <Card className={cn(
          'border border-border/70 bg-background/92 shadow-[0_16px_42px_-38px_rgba(15,23,42,0.26)]',
          hasComparePair ? 'rounded-[1.6rem]' : 'rounded-[1.85rem]',
        )}>
          <CardContent className={cn(
            'grid px-6 md:px-8 xl:grid-cols-[minmax(0,1.2fr)_auto]',
            hasComparePair ? 'gap-4 py-4 md:py-6' : 'gap-6 py-6 md:py-8',
          )}>
            <div className={cn(hasComparePair ? 'space-y-4' : 'space-y-6')}>
              <div className="flex flex-wrap items-start justify-between gap-4">
                <div className="space-y-2">
                  <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                    {hasComparePair ? 'Pairing' : 'Compare setup'}
                  </p>
                  <p className="text-sm leading-6 text-muted-foreground">
                    {suggestedBaseline && suggestedCandidate
                      ? hasComparePair
                        ? 'Reference and changed run are set. Open controls only if you need to change them.'
                        : `Reference ${suggestedBaseline.sourcePath} against ${suggestedCandidate.sourcePath}, or swap the pairing below.`
                      : 'Pick one reference run and one changed run to keep the answer narrow and clear.'}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  {hasComparePair ? (
                    <Button
                      className="rounded-full"
                      onClick={() => setIsPairingExpanded((current) => !current)}
                      size="sm"
                      type="button"
                      variant="outline"
                    >
                      {showPairingEditor ? 'Hide controls' : 'Change pair'}
                    </Button>
                  ) : null}
                  <Button
                    className="rounded-full"
                    disabled={!suggestedBaseline || !suggestedCandidate || isUsingSuggestedPair}
                    onClick={handleUseSuggestedPair}
                    size="sm"
                    type="button"
                    variant={isUsingSuggestedPair ? 'secondary' : 'outline'}
                  >
                    {isUsingSuggestedPair ? 'Using suggested pair' : 'Use suggested pair'}
                  </Button>
                  <Button
                    className="rounded-full"
                    disabled={!baselineFile || !candidateFile}
                    onClick={handleSwapPair}
                    size="sm"
                    type="button"
                    variant="outline"
                  >
                    <ArrowLeftRight className="mr-2 size-4" />
                    Swap roles
                  </Button>
                </div>
              </div>

              {showPairingEditor ? (
                <div className="grid gap-4 lg:grid-cols-2">
                  <SelectorCard
                    caption="Known-good or reference run"
                    files={sortedAudioFiles}
                    id="baseline-file"
                    label="Baseline"
                    onChange={handleBaselineChange}
                    value={baselineFile?.id ?? ''}
                  />
                  <SelectorCard
                    caption="The new run you want explained"
                    files={sortedAudioFiles.filter((file) => file.id !== baselineFile?.id)}
                    id="candidate-file"
                    label="Candidate"
                    onChange={handleCandidateChange}
                    placeholder="Choose a candidate"
                    value={candidateFile?.id ?? ''}
                  />
                </div>
              ) : null}

              <div className={cn(
                'flex flex-wrap gap-2 text-sm text-muted-foreground',
                showPairingEditor ? 'border-t border-border/60 pt-4' : 'pt-2',
              )}>
                <span className="rounded-full border border-border/70 bg-secondary/35 px-4 py-2">
                  Reference: {baselineFile ? `${baselineFile.sourcePath} • ${formatDuration(baselineFile.durationSeconds)}` : 'Not selected'}
                </span>
                <span className="rounded-full border border-border/70 bg-secondary/35 px-4 py-2">
                  Changed run: {candidateFile ? `${candidateFile.sourcePath} • ${formatDuration(candidateFile.durationSeconds)}` : 'Not selected'}
                </span>
              </div>
            </div>

            <div className="flex flex-wrap gap-2 xl:w-[18rem] xl:flex-col">
              <Button asChild className="rounded-full justify-center xl:justify-start" size="sm" variant="outline">
                <Link to={inspectHref}>
                  Open in Inspect
                  <ArrowUpRight className="ml-2 size-4" />
                </Link>
              </Button>
              <Button
                className="rounded-full justify-center xl:justify-start"
                disabled={!summaryCard}
                onClick={() => {
                  void copyBriefing(shareSummary, setCopyState)
                }}
                size="sm"
                type="button"
                variant="outline"
              >
                <Copy className="mr-2 size-4" />
                {copyState === 'copied'
                  ? 'Copied'
                  : copyState === 'error'
                    ? 'Copy failed'
                    : 'Copy briefing'}
              </Button>
              <Button
                className="rounded-full justify-center xl:justify-start"
                disabled={!summaryCard}
                onClick={() => {
                  if (!baselineFile || !candidateFile || !summaryCard) {
                    return
                  }

                  downloadBriefing(
                    `compare-${slugifyName(baselineFile.sourcePath)}-vs-${slugifyName(candidateFile.sourcePath)}.md`,
                    shareSummary,
                  )
                }}
                size="sm"
                type="button"
                variant="outline"
              >
                <Download className="mr-2 size-4" />
                Download brief
              </Button>
            </div>
          </CardContent>
        </Card>
      </section>

      {!hasAudioBaseline || !candidateFile ? (
        <Card className="rounded-[2rem] border border-border/70 bg-background/90 shadow-[0_18px_50px_-42px_rgba(15,23,42,0.3)]">
          <CardHeader className="space-y-4">
            <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
              Compare workflow
            </p>
            <CardTitle>Pick a candidate to unlock the guided comparison</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-3">
            <EmptyStep
              copy="Choose one baseline file and one candidate file. The guided page is intentionally strict so the answer stays clear."
              title="1. Keep the scope narrow"
            />
            <EmptyStep
              copy="The assistant will summarize the main difference, explain why it matters, and recommend one supported next step."
              title="2. Let the product frame the story"
            />
            <EmptyStep
              copy="If you need transforms, metrics, or the full inspect rail, move into Inspect with the current context already loaded."
              title="3. Go deeper only when needed"
            />
          </CardContent>
        </Card>
      ) : (
        <>
          <section>
            <Card className="rounded-[2rem] border border-border/70 bg-background/92 shadow-[0_18px_50px_-42px_rgba(15,23,42,0.24)]">
              <CardHeader className="space-y-6 px-6 py-6 md:px-8 md:py-8">
                <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                  <div className="space-y-2">
                    <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
                      Evidence
                    </p>
                    <CardTitle className="text-[1.35rem]">
                      Check the proof in the recommended view.
                    </CardTitle>
                    <p className="max-w-3xl text-sm leading-6 text-muted-foreground">
                      Baseline: <strong>{baselineFile!.sourcePath}</strong>
                      <span className="mx-2 text-border">/</span>
                      Candidate: <strong>{candidateFile!.sourcePath}</strong>
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
                    {VIEW_OPTIONS.map((view) => (
                      <Button
                        key={view.id}
                        onClick={() => handleChartViewChange(view.id)}
                        size="sm"
                        type="button"
                        variant={activeView === view.id ? 'default' : 'outline'}
                      >
                        {view.label}
                        {recommendedView === view.id ? (
                          <span className="ml-2 rounded-full bg-background/80 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-[0.16em] text-foreground">
                            Recommended
                          </span>
                        ) : null}
                      </Button>
                    ))}
                  </div>
                </div>

                <div className="rounded-[1.45rem] border border-border/60 bg-white p-4 md:p-6">
                  {activeView === 'waveform' ? (
                    <WaveformPanel
                      compact
                      comparisonRequests={[
                        {
                          fileId: baselineFile!.id,
                          label: 'Baseline',
                          transforms: activeTransforms,
                        },
                        {
                          fileId: candidateFile!.id,
                          label: 'Candidate',
                        },
                      ]}
                      fileId={baselineFile!.id}
                      transforms={activeTransforms}
                    />
                  ) : null}

                  {activeView === 'fft' ? (
                    <FftPanel
                      compact
                      comparisonRequests={[
                        {
                          fileId: baselineFile!.id,
                          label: 'Baseline',
                          transforms: activeTransforms,
                        },
                        {
                          fileId: candidateFile!.id,
                          label: 'Candidate',
                        },
                      ]}
                      fileId={baselineFile!.id}
                      transforms={activeTransforms}
                    />
                  ) : null}

                  {activeView === 'spectrogram' ? (
                    <SpectrogramPanel
                      compact
                      comparisonFileId={candidateFile!.id}
                      fileId={baselineFile!.id}
                      transforms={activeTransforms}
                    />
                  ) : null}
                </div>
              </CardHeader>

              <CardContent className="grid gap-4 px-6 pb-6 md:px-8 md:pb-8 xl:grid-cols-[minmax(0,1.2fr)_minmax(280px,0.8fr)]">
                <div className="rounded-[1.35rem] border border-border/60 bg-secondary/30 p-4">
                  <div className="flex items-center justify-between gap-4">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                      Key facts
                    </p>
                    <span className="text-xs text-muted-foreground">{groundingLabel}</span>
                  </div>
                  <div className="mt-4 flex flex-wrap gap-4">
                    {evidenceFacts.length > 0 ? (
                      evidenceFacts.map((fact) => (
                        <div
                          className="min-w-[11rem] rounded-[1.05rem] border border-border/60 bg-background/92 p-4 text-sm text-foreground"
                          key={fact.code}
                        >
                          <p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                            {fact.label}
                          </p>
                          <p className="mt-1 font-semibold">{fact.valueText}</p>
                        </div>
                      ))
                    ) : (
                      <span className="text-sm text-muted-foreground">Waiting for comparison facts.</span>
                    )}
                  </div>
                </div>

                <div className="rounded-[1.35rem] border border-border/60 bg-secondary/30 p-4">
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                    Why the assistant says that
                  </p>
                  <div className="mt-4 space-y-4">
                    {evidenceObservations.length > 0 ? (
                      evidenceObservations.map((observation) => (
                        <div
                          className="rounded-[1.15rem] border border-border/60 bg-background/92 p-4 text-sm leading-6 text-foreground"
                          key={observation.code}
                        >
                          {observation.message}
                        </div>
                      ))
                    ) : (
                      <span className="text-sm text-muted-foreground">Waiting for grounded observations.</span>
                    )}
                  </div>
                  {summaryCard?.limitations.length ? (
                    <p className="mt-4 text-xs leading-5 text-muted-foreground">
                      {summaryCard.limitations[0]}
                    </p>
                  ) : null}
                </div>
              </CardContent>
            </Card>
          </section>

          <section className="grid gap-4" ref={actionSectionRef}>
            {(proposal || executionResponse) ? (
              <div className="grid gap-4 xl:grid-cols-2">
                <AssistantActionProposal
                  isExecuting={isExecuting}
                  onCancel={() => setProposal(null)}
                  onConfirm={() => void handleConfirmProposal()}
                  proposal={proposal}
                />

                <AssistantResultSummary response={executionResponse} />
              </div>
            ) : null}
          </section>
        </>
      )}
    </div>
  )

  function updateCompareParams({
    baseline,
    candidateId,
  }: {
    baseline: IWorkspaceImportedFile | null
    candidateId: string | null
  }): void {
    const nextParams = new URLSearchParams(searchParams)

    if (baseline) {
      nextParams.set('batch', baseline.batchId)
      nextParams.set('file', baseline.id)
    }

    if (candidateId) {
      nextParams.set('candidate', candidateId)
    } else {
      nextParams.delete('candidate')
    }

    nextParams.delete('refresh')
    setSearchParams(nextParams, { replace: true })
  }
}

function CompareEmptyState({
  description,
  title,
}: {
  description: string
  title: string
}) {
  return (
    <div className="flex flex-1 items-center justify-center">
      <Card className="max-w-2xl rounded-[2rem] border border-border/70 bg-background/92 shadow-[0_18px_50px_-42px_rgba(15,23,42,0.35)]">
        <CardHeader className="space-y-3">
          <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
            Guided compare
          </p>
          <CardTitle>{title}</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <p className="text-sm leading-6 text-muted-foreground">{description}</p>
          <div className="rounded-[1.4rem] border border-dashed border-border/70 bg-secondary/25 p-4 text-sm text-muted-foreground">
            Import signals from the header, then come back here to compare a baseline and a candidate with AI guidance.
          </div>
        </CardContent>
      </Card>
    </div>
  )
}

function EmptyStep({
  copy,
  title,
}: {
  copy: string
  title: string
}) {
  return (
    <article className="rounded-[1.5rem] border border-border/60 bg-secondary/25 px-4 py-4">
      <p className="text-sm font-semibold text-foreground">{title}</p>
      <p className="mt-2 text-sm leading-6 text-muted-foreground">{copy}</p>
    </article>
  )
}

function SelectorCard({
  caption,
  files,
  id,
  label,
  onChange,
  placeholder = 'Choose a file',
  value,
}: {
  caption: string
  files: IWorkspaceImportedFile[]
  id: string
  label: string
  onChange: (fileId: string) => void
  placeholder?: string
  value: string
}) {
  return (
    <label className="grid gap-2" htmlFor={id}>
      <div className="flex items-center justify-between gap-3">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
            {label}
          </p>
          <p className="mt-1 text-sm text-slate-600">{caption}</p>
        </div>
      </div>
      <select
        className="h-12 rounded-[1.25rem] border border-slate-900/12 bg-white px-4 text-sm text-slate-900 shadow-sm outline-none transition focus:border-slate-900/30 focus:ring-2 focus:ring-slate-900/10"
        id={id}
        onChange={(event) => onChange(event.target.value)}
        value={value}
      >
        <option value="">{placeholder}</option>
        {files.map((file) => (
          <option key={file.id} value={file.id}>
            {formatFileOption(file)}
          </option>
        ))}
      </select>
    </label>
  )
}

function formatDuration(durationSeconds: number | null): string {
  if (!durationSeconds || durationSeconds <= 0) {
    return 'Unknown duration'
  }

  const minutes = Math.floor(durationSeconds / 60)
  const seconds = Math.round(durationSeconds % 60)
  return `${minutes}:${seconds.toString().padStart(2, '0')}`
}

function formatFileOption(file: IWorkspaceImportedFile): string {
  return `${file.sourcePath} • ${formatDuration(file.durationSeconds)}`
}

function pickSuggestedCandidate(
  files: IWorkspaceImportedFile[],
  baseline: IWorkspaceImportedFile | null,
): IWorkspaceImportedFile | null {
  if (!baseline) {
    return files[1] ?? null
  }

  const newestAlternative =
    [...files]
      .reverse()
      .find((file) => file.id !== baseline.id) ?? null

  if (newestAlternative) {
    return newestAlternative
  }

  return files.find((file) => file.id !== baseline.id) ?? null
}

function buildSummaryFollowUps(nextSteps: string[]): IAiFollowUpPrompt[] {
  return nextSteps.slice(0, 4).map((step, index) => ({
    id: `summary-step-${index}`,
    intent: 'compare',
    label: step,
    prompt: step,
  }))
}

function buildShareSummary(
  baseline: IWorkspaceImportedFile | null,
  candidate: IWorkspaceImportedFile | null,
  summaryCard: IAiSummaryCard | null,
  activeView: AssistantAnalysisView,
): string {
  if (!baseline || !candidate || !summaryCard) {
    return ''
  }

  const sections = [
    '# Guided signal comparison',
    '',
    `Baseline: ${baseline.sourcePath}`,
    `Candidate: ${candidate.sourcePath}`,
    `Recommended view: ${summaryCard.recommendedView ?? activeView}`,
    '',
    '## What changed',
    summaryCard.primaryFinding || summaryCard.summary,
    '',
    '## Why it matters',
    summaryCard.impactSummary || 'No impact summary was available.',
    '',
    '## What to inspect next',
    summaryCard.recommendedNextStep || summaryCard.nextSteps[0] || 'No guided next step was available.',
    '',
  ]

  if (summaryCard.keyFacts.length > 0) {
    sections.push('## Key facts', '')
    summaryCard.keyFacts.slice(0, 5).forEach((fact) => {
      sections.push(`- ${fact.label}: ${fact.valueText}`)
    })
    sections.push('')
  }

  if (summaryCard.topObservations.length > 0) {
    sections.push('## Grounded observations', '')
    summaryCard.topObservations.slice(0, 3).forEach((observation) => {
      sections.push(`- ${observation.message}`)
    })
    sections.push('')
  }

  if (summaryCard.limitations.length > 0) {
    sections.push('## Limits', '')
    summaryCard.limitations.slice(0, 3).forEach((limitation) => {
      sections.push(`- ${limitation}`)
    })
  }

  return sections.join('\n').trim()
}

async function copyBriefing(
  content: string,
  setCopyState: (state: 'idle' | 'copied' | 'error') => void,
): Promise<void> {
  if (!content) {
    setCopyState('error')
    return
  }

  try {
    await navigator.clipboard.writeText(content)
    setCopyState('copied')
  } catch {
    setCopyState('error')
  }
}

function downloadBriefing(filename: string, content: string): void {
  if (!content) {
    return
  }

  const blob = new Blob([content], { type: 'text/markdown;charset=utf-8' })
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function slugifyName(value: string): string {
  return value
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 48)
}

function buildInspectHref(
  baseline: IWorkspaceImportedFile,
  candidate: IWorkspaceImportedFile | null,
  activeView: AssistantAnalysisView,
): string {
  const query = new URLSearchParams({
    batch: baseline.batchId,
    file: baseline.id,
    view: activeView,
  })

  if (candidate) {
    query.set('compareFileIds', candidate.id)
  }

  return `/inspect?${query.toString()}`
}

function isActionLike(prompt: string): boolean {
  const normalized = prompt.toLowerCase()

  return (
    normalized.includes('apply ') ||
    normalized.includes('switch ') ||
    normalized.includes('reset ') ||
    normalized.includes('normalize') ||
    normalized.includes('trim silence') ||
    normalized.includes('gain') ||
    normalized.includes('set ') ||
    normalized.includes('compare these')
  )
}
