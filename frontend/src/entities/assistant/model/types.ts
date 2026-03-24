import type { ITransformRecipe } from '@/features/transforms/utils/types'

export type AssistantAnalysisView = 'waveform' | 'fft' | 'spectrogram'

export interface ISelectionRange {
  endSeconds?: number | null
  startSeconds?: number | null
}

export interface IAssistantWorkspaceContextRequest {
  activeView: AssistantAnalysisView
  compareFileIds: string[]
  fileId: string
  selection?: ISelectionRange | null
  transforms: ITransformRecipe
  workspaceId: string
}

export interface IAssistantWorkspaceFileReference {
  fileId: string
  signalKind: string
  sourcePath: string
}

export interface IAssistantWorkspaceContext {
  activeView: AssistantAnalysisView
  analysisBasis: string
  availableFiles: IAssistantWorkspaceFileReference[]
  compareFileIds: string[]
  compareFiles: IAssistantWorkspaceFileReference[]
  isSelectionApplied: boolean
  selectedFile: IAssistantWorkspaceFileReference | null
  selectedFileId: string
  selection?: ISelectionRange | null
  selectionScope: string
  supportedCommands: string[]
  transforms: ITransformRecipe
  warnings: string[]
  workspaceId: string
}

export interface IEvidenceItem {
  basis: string
  code: string
  confidence: string
  kind: string
  label: string
  source: string
  valueText: string
}

export interface IObservation {
  basis: string
  code: string
  confidence: string
  evidenceCodes: string[]
  message: string
  severity: string
}

export interface ISignalSummary {
  crestFactor: number
  crestFactorDb: number
  dominantFrequencyHz: number
  dominantMagnitudeDb: number
  durationSeconds: number
  facts: IEvidenceItem[]
  fileId: string
  nearFullScaleCount: number
  peak: number
  rms: number
  sampleRateHz: number
  samplesOverFullScaleCount: number
  sourcePath: string
}

export interface IComparisonDelta {
  dominantFrequencyDeltaHz: number
  durationDeltaSeconds: number
  facts: IEvidenceItem[]
  fileId: string
  peakDeltaDbFs: number
  rmsDeltaDb: number
  sourcePath: string
}

export interface IComparisonSummary {
  comparisons: IComparisonDelta[]
  compareFileIds: string[]
  primaryFileId: string
}

export interface IAiConversationTurn {
  content: string
  role: 'assistant' | 'user'
}

export interface IAiRequest extends IAssistantWorkspaceContextRequest {
  history?: IAiConversationTurn[]
  prompt: string
}

export interface IAiActionStep {
  command: string
  compareSignalIds: string[]
  cutoffHz?: number | null
  displayText: string
  enabled?: boolean | null
  filterMode?: string | null
  gainDb?: number | null
  highCutoffHz?: number | null
  lowCutoffHz?: number | null
  primarySignalId?: string | null
  q?: number | null
  view?: AssistantAnalysisView | null
}

export interface IAiActionProposal {
  clarificationQuestion: string
  closestSupportedAction: string
  proposalId: string
  requiresConfirmation: boolean
  status: 'needs_confirmation' | 'needs_clarification' | 'unsupported' | 'ready'
  steps: IAiActionStep[]
  summary: string
  title: string
  unsupportedReason: string
  warnings: string[]
}

export interface IWorkspaceStatePatch {
  activeView?: AssistantAnalysisView | null
  compareFileIds?: string[] | null
  targetFileId?: string | null
  transforms?: ITransformRecipe | null
}

export interface ICommandExecutionResult {
  executedSteps: string[]
  message: string
  patch: IWorkspaceStatePatch
  succeeded: boolean
}

export interface IAiFollowUpPrompt {
  id: string
  intent: string
  label: string
  prompt: string
}

export interface IAiSummaryCard {
  keyFacts: IEvidenceItem[]
  limitations: string[]
  nextSteps: string[]
  summary: string
  title: string
  topObservations: IObservation[]
}

export interface IAiResponse {
  actionProposal?: IAiActionProposal | null
  context: IAssistantWorkspaceContext
  executionResult?: ICommandExecutionResult | null
  followUpPrompts: IAiFollowUpPrompt[]
  intent: string
  limitations: string[]
  message: string
  observations: IObservation[]
  status: 'ready' | 'degraded' | 'needs_confirmation' | 'needs_clarification' | 'unsupported' | 'error'
  summaryCard: IAiSummaryCard
  usedFallback: boolean
  workspacePatch?: IWorkspaceStatePatch | null
}

export interface IPlanActionRequest extends IAssistantWorkspaceContextRequest {
  history?: IAiConversationTurn[]
  prompt: string
}

export interface IExecuteActionRequest extends IAssistantWorkspaceContextRequest {
  confirmed: boolean
  proposal: IAiActionProposal
}
