import type { JSX } from 'react'
import { useEffect, useMemo, useState } from 'react'
import {
  ArrowLeftRight,
  ArrowUpRight,
  BotMessageSquare,
  ChevronRight,
  Copy,
  Download,
  RefreshCw,
  Sparkles,
  WandSparkles,
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
  const [searchParams, setSearchParams] = useSearchParams()
  const [activeView, setActiveView] = useState<AssistantAnalysisView>('waveform')
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'error'>('idle')
  const [executionResponse, setExecutionResponse] = useState<IAiResponse | null>(null)
  const [hasPinnedView, setHasPinnedView] = useState(false)
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
      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto pr-1">
        <CompareEmptyState
          description="Import a baseline and a candidate signal to get an AI-guided comparison."
          title="Start with two signals"
        />
      </div>
    )
  }

  if (audioFiles.length === 0) {
    return (
      <div className="flex min-h-0 flex-1 flex-col overflow-y-auto pr-1">
        <CompareEmptyState
          description="This guided compare flow is audio-first right now. Import audio files or open Inspect for the full technical workspace."
          title="Audio compare only in this first slice"
        />
      </div>
    )
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-6 overflow-y-auto pr-1">
      <section className="overflow-hidden rounded-[2.25rem] border border-border/70 bg-[linear-gradient(135deg,rgba(255,247,237,0.98),rgba(255,255,255,0.98)_42%,rgba(236,253,245,0.9))] shadow-[0_28px_80px_-56px_rgba(15,23,42,0.35)]">
        <div className="grid gap-8 px-6 py-7 md:px-8 md:py-8 xl:grid-cols-[minmax(0,1.2fr)_minmax(360px,0.8fr)]">
          <div className="space-y-4">
            <div className="inline-flex items-center gap-2 rounded-full border border-slate-900/10 bg-white/80 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.22em] text-slate-600">
              <ArrowLeftRight className="size-3.5" />
              Guided compare
            </div>
            <div className="space-y-3">
              <h3 className="max-w-2xl text-3xl font-semibold tracking-tight text-slate-950 md:text-[2.4rem]">
                Understand what changed before you open the deep inspection tools.
              </h3>
              <p className="max-w-2xl text-sm leading-7 text-slate-600 md:text-[15px]">
                Pick a baseline and one candidate. The backend computes the real comparison, then the
                assistant turns it into a plain-language briefing, evidence, and a guided next step.
              </p>
            </div>
            <div className="flex flex-wrap gap-2 text-xs text-slate-600">
              <span className="rounded-full border border-slate-900/10 bg-white/70 px-3 py-1.5">
                Grounded in computed DSP
              </span>
              <span className="rounded-full border border-slate-900/10 bg-white/70 px-3 py-1.5">
                One baseline + one candidate
              </span>
              <span className="rounded-full border border-slate-900/10 bg-white/70 px-3 py-1.5">
                AI explains, backend measures
              </span>
            </div>
          </div>

          <div className="grid gap-4 rounded-[2rem] border border-slate-900/10 bg-white/84 p-4 shadow-[0_20px_50px_-42px_rgba(15,23,42,0.35)]">
            <div className="rounded-[1.6rem] border border-slate-900/10 bg-slate-950/[0.03] px-4 py-4">
              <div className="flex flex-col gap-4">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
                      Suggested pairing
                    </p>
                    <p className="mt-1 text-sm leading-6 text-slate-600">
                      {suggestedBaseline && suggestedCandidate
                        ? `Use ${suggestedBaseline.sourcePath} as the reference run and ${suggestedCandidate.sourcePath} as the changed run.`
                        : 'Import at least two audio files to get a guided baseline-versus-candidate pairing.'}
                    </p>
                  </div>
                  <div className="flex flex-wrap gap-2">
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

                <div className="grid gap-3 md:grid-cols-2">
                  <RoleCard
                    file={baselineFile}
                    label="Reference run"
                    tone="reference"
                  />
                  <RoleCard
                    file={candidateFile}
                    label="Changed run"
                    tone="candidate"
                  />
                </div>
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] md:items-end">
              <SelectorCard
                caption="Known-good or reference run"
                files={sortedAudioFiles}
                id="baseline-file"
                label="Baseline"
                onChange={handleBaselineChange}
                value={baselineFile?.id ?? ''}
              />

              <div className="flex justify-center md:pb-1">
                <Button
                  className="rounded-full"
                  disabled={!baselineFile || !candidateFile}
                  onClick={handleSwapPair}
                  size="icon"
                  type="button"
                  variant="outline"
                >
                  <ArrowLeftRight className="size-4" />
                </Button>
              </div>

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

            <div className="flex flex-wrap items-center justify-between gap-3 rounded-[1.5rem] border border-dashed border-slate-900/12 bg-slate-950/[0.03] px-4 py-3">
              <div>
                <p className="text-xs font-semibold uppercase tracking-[0.18em] text-slate-500">
                  Handoff
                </p>
                <p className="mt-1 text-sm leading-6 text-slate-600">
                  Open the full technical workspace only when you need deeper manual inspection.
                </p>
              </div>
              <Button asChild className="rounded-full" size="sm" variant="outline">
                <Link to={inspectHref}>
                  Open in Inspect
                  <ArrowUpRight className="ml-2 size-4" />
                </Link>
              </Button>
            </div>

            <div className="flex flex-wrap gap-2">
              <Button
                className="rounded-full"
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
                className="rounded-full"
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
          </div>
        </div>
      </section>

      {!hasAudioBaseline || !candidateFile ? (
        <Card className="rounded-[2rem] border border-border/70 bg-background/90 shadow-[0_18px_50px_-42px_rgba(15,23,42,0.3)]">
          <CardHeader className="space-y-3">
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
          <section className="grid gap-4 xl:grid-cols-3">
            <BriefingCard
              badge={summaryCard?.mode === 'comparison' ? 'Baseline vs candidate' : 'Summary'}
              body={summaryCard?.primaryFinding ?? 'Waiting for the guided comparison summary.'}
              ctaLabel="Ask follow-up"
              icon={<Sparkles className="size-4" />}
              isLoading={isSummaryLoading}
              onClick={() => void handlePrompt('What changed between these two signals?')}
              title="What changed"
            />
            <BriefingCard
              badge="Impact"
              body={summaryCard?.impactSummary ?? 'The product will explain why the current difference matters.'}
              ctaLabel="Why does it matter?"
              icon={<BotMessageSquare className="size-4" />}
              isLoading={isSummaryLoading}
              onClick={() => void handlePrompt('Why does this difference matter?')}
              title="Why it matters"
            />
            <BriefingCard
              badge="Recommended next step"
              body={recommendedNextStep || 'Choose a candidate to get the next suggested check.'}
              ctaLabel={recommendedNextStep ? 'Run step' : 'Ask next step'}
              icon={<WandSparkles className="size-4" />}
              isLoading={isSummaryLoading}
              onClick={() =>
                void handleGuidedStep(
                  recommendedNextStep || 'What should I inspect next?',
                )
              }
              title="What to inspect next"
            />
          </section>

          <section className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(320px,0.8fr)]">
            <Card className="rounded-[2rem] border border-border/70 bg-background/90 shadow-[0_18px_50px_-42px_rgba(15,23,42,0.25)]">
              <CardHeader className="space-y-4">
                <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
                  <div className="space-y-2">
                    <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
                      Evidence
                    </p>
                    <CardTitle className="text-[1.45rem]">
                      See the comparison in the view the assistant recommends first.
                    </CardTitle>
                    <p className="max-w-2xl text-sm leading-6 text-muted-foreground">
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

                <div className="grid gap-3 lg:grid-cols-2">
                  <div className="rounded-[1.4rem] border border-border/60 bg-secondary/35 px-4 py-3">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                      Key facts
                    </p>
                    <div className="mt-3 flex flex-wrap gap-2">
                      {summaryCard?.keyFacts.slice(0, 4).map((fact) => (
                        <div
                          className="rounded-full border border-border/60 bg-background/90 px-3 py-2 text-sm text-foreground"
                          key={fact.code}
                        >
                          <span className="mr-2 text-[11px] uppercase tracking-[0.14em] text-muted-foreground">
                            {fact.label}
                          </span>
                          <strong>{fact.valueText}</strong>
                        </div>
                      )) ?? (
                        <span className="text-sm text-muted-foreground">Waiting for comparison facts.</span>
                      )}
                    </div>
                  </div>

                  <div className="rounded-[1.4rem] border border-border/60 bg-secondary/35 px-4 py-3">
                    <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                      Observations
                    </p>
                    <div className="mt-3 space-y-2.5">
                      {summaryCard?.topObservations.slice(0, 3).map((observation) => (
                        <div
                          className="rounded-[1.15rem] border border-border/60 bg-background/90 px-3 py-2.5 text-sm text-foreground"
                          key={observation.code}
                        >
                          {observation.message}
                        </div>
                      )) ?? (
                        <span className="text-sm text-muted-foreground">Waiting for grounded observations.</span>
                      )}
                    </div>
                  </div>
                </div>
              </CardHeader>
              <CardContent>
                <div className="rounded-[1.6rem] border border-border/60 bg-white px-3 py-3">
                  {activeView === 'waveform' ? (
                    <WaveformPanel
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
                      comparisonFileId={candidateFile!.id}
                      fileId={baselineFile!.id}
                      transforms={activeTransforms}
                    />
                  ) : null}
                </div>
              </CardContent>
            </Card>

            <div className="flex min-h-0 flex-col gap-4">
              <AssistantChat
                errorMessage={combinedError}
                followUps={followUps}
                isLoading={isAssistantLoading || isPlanning}
                messages={messages}
                onSelectPrompt={async (prompt) => {
                  await handleGuidedStep(prompt)
                }}
                onSubmit={async (prompt) => {
                  await handlePrompt(prompt)
                }}
              />

              <AssistantActionProposal
                isExecuting={isExecuting}
                onCancel={() => setProposal(null)}
                onConfirm={() => void handleConfirmProposal()}
                proposal={proposal}
              />

              <AssistantResultSummary response={executionResponse} />
            </div>
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
          <div className="rounded-[1.4rem] border border-dashed border-border/70 bg-secondary/25 px-4 py-3 text-sm text-muted-foreground">
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

function RoleCard({
  file,
  label,
  tone,
}: {
  file: IWorkspaceImportedFile | null
  label: string
  tone: 'candidate' | 'reference'
}) {
  const toneClasses =
    tone === 'reference'
      ? 'border-emerald-300/55 bg-emerald-50/80'
      : 'border-amber-300/55 bg-amber-50/80'

  return (
    <article className={`rounded-[1.35rem] border px-4 py-3 ${toneClasses}`}>
      <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-slate-500">
        {label}
      </p>
      {file ? (
        <div className="mt-2 space-y-1.5">
          <p className="text-sm font-semibold leading-6 text-slate-950">{file.sourcePath}</p>
          <p className="text-sm text-slate-600">
            {formatDuration(file.durationSeconds)} • {formatImportedAt(file.importedAtUtc)}
          </p>
        </div>
      ) : (
        <p className="mt-2 text-sm leading-6 text-slate-500">
          No file selected for this role yet.
        </p>
      )}
    </article>
  )
}

function BriefingCard({
  badge,
  body,
  ctaLabel,
  icon,
  isLoading,
  onClick,
  title,
}: {
  badge: string
  body: string
  ctaLabel: string
  icon: JSX.Element
  isLoading: boolean
  onClick: () => void
  title: string
}) {
  return (
    <Card className="rounded-[1.9rem] border border-border/70 bg-background/90 shadow-[0_18px_50px_-42px_rgba(15,23,42,0.24)]">
      <CardHeader className="space-y-3">
        <div className="flex items-center justify-between gap-3">
          <div className="inline-flex rounded-full border border-border/60 bg-secondary/45 px-3 py-1 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            {badge}
          </div>
          <div className="text-muted-foreground">{icon}</div>
        </div>
        <CardTitle className="text-[1.35rem]">{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <LoadingSpinner label="Refreshing briefing" />
        ) : (
          <p className="text-sm leading-7 text-foreground">{body}</p>
        )}
        <Button className="rounded-full" onClick={onClick} size="sm" type="button" variant="outline">
          {ctaLabel}
          <ChevronRight className="ml-2 size-4" />
        </Button>
      </CardContent>
    </Card>
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
    <label className="grid gap-2.5" htmlFor={id}>
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

function formatImportedAt(value: string): string {
  const date = new Date(value)

  if (Number.isNaN(date.getTime())) {
    return 'Imported recently'
  }

  return new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    month: 'short',
  }).format(date)
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
