import type { JSX } from 'react'
import { useEffect, useState } from 'react'
import {
  CircleAlert,
  Database,
  PanelRightClose,
  PanelRightOpen,
  RefreshCw,
  SlidersHorizontal,
} from 'lucide-react'

import { CompareControls } from '@/features/compare-mode/components/CompareControls/CompareControls'
import { useCompareSelection } from '@/features/compare-mode/hooks/useCompareSelection'
import { FftPanel } from '@/features/fft/components/FftPanel/FftPanel'
import { SpectrogramPanel } from '@/features/spectrogram/components/SpectrogramPanel/SpectrogramPanel'
import { TransformsPanel } from '@/features/transforms/components/TransformsPanel/TransformsPanel'
import { useSignalTransforms } from '@/features/transforms/hooks/useSignalTransforms'
import { WaveformPanel } from '@/features/waveform/components/WaveformPanel/WaveformPanel'
import { Button } from '@/shared/ui/button'

import { AudioPreviewPlayer } from '../components/AudioPreviewPlayer/AudioPreviewPlayer'
import { useWorkspaceImports } from '../hooks/useWorkspaceImports'
import styles from './workspace-page.module.scss'

const byteFormatter = new Intl.NumberFormat(undefined, {
  maximumFractionDigits: 1,
  notation: 'compact',
})

const ANALYSIS_TABS = [
  {
    id: 'waveform',
    label: 'Waveform',
  },
  {
    id: 'fft',
    label: 'FFT',
  },
  {
    id: 'spectrogram',
    label: 'Spectrogram',
  },
] as const

type AnalysisView = (typeof ANALYSIS_TABS)[number]['id']
type SidebarPanel = 'details' | 'transforms' | null

export function WorkspacePage(): JSX.Element {
  const [activeView, setActiveView] = useState<AnalysisView>('waveform')
  const [activeSidebarPanel, setActiveSidebarPanel] = useState<SidebarPanel>(null)
  const {
    batches,
    errorMessage,
    failedFileCount,
    isLoading,
    reloadWorkspace,
    selectedBatch,
    selectedFile,
    selectFile,
  } = useWorkspaceImports()
  const importedFiles = batches.flatMap((batch) => batch.importedFiles)
  const comparableFiles = selectedFile
    ? importedFiles.filter((file) => file.signalKind === selectedFile.signalKind)
    : importedFiles
  const {
    canCompare,
    compareFiles,
    clearCompareSelection,
    isFileSelectedForCompare,
    isCompareMode,
    layoutMode,
    setLayoutMode,
    toggleComparisonFile,
  } = useCompareSelection({
    files: comparableFiles,
    primaryFileId: selectedFile?.id ?? null,
  })
  const {
    activeTransforms,
    hasActiveTransforms,
    resetTransforms,
    setGainDb,
    setNormalize,
    setTrimSilence,
  } = useSignalTransforms(selectedFile?.id ?? null)

  useEffect(() => {
    if (!selectedFile) {
      setActiveSidebarPanel(null)
    }
  }, [selectedFile])

  useEffect(() => {
    if (!selectedFile) {
      setActiveView('waveform')
    }
  }, [selectedFile])

  if (isLoading) {
    return (
      <div className={styles.state}>
        <p className={styles.stateEyebrow}>Workspace</p>
        <h3 className={styles.stateTitle}>Loading imports</h3>
        <p className={styles.stateCopy}>
          Pulling the current workspace snapshot from the backend.
        </p>
      </div>
    )
  }

  if (errorMessage) {
    return (
      <div className={styles.state}>
        <p className={styles.stateEyebrow}>Workspace</p>
        <h3 className={styles.stateTitle}>Imports unavailable</h3>
        <p className={styles.stateCopy}>{errorMessage}</p>
        <Button onClick={() => void reloadWorkspace()} type="button" variant="outline">
          <RefreshCw className="size-4" />
          Retry
        </Button>
      </div>
    )
  }

  if (batches.length === 0) {
    return (
      <div className={styles.welcomeState}>
        <section className={styles.welcomeHero}>
          <div className={styles.welcomeIntro}>
            <p className={styles.stateEyebrow}>Quick start</p>
            <h3 className={styles.stateTitle}>Start with your first signal</h3>
            <p className={styles.stateCopy}>
              Use <strong>Import</strong> in the top-right corner to bring files into this
              workspace. After that, pick a file on the left to inspect its waveform, FFT, or
              spectrogram.
            </p>
          </div>

          <div className={styles.welcomeSteps}>
            <div className={styles.welcomeStep}>
              <span className={styles.welcomeStepNumber}>1</span>
              <div className={styles.welcomeStepBody}>
                <strong className={styles.welcomeStepTitle}>Import one or more files</strong>
                <p className={styles.welcomeStepCopy}>
                  Start with a single run or load a small batch to compare later.
                </p>
              </div>
            </div>

            <div className={styles.welcomeStep}>
              <span className={styles.welcomeStepNumber}>2</span>
              <div className={styles.welcomeStepBody}>
                <strong className={styles.welcomeStepTitle}>Select a file from the rail</strong>
                <p className={styles.welcomeStepCopy}>
                  The selected file opens directly in the analysis view.
                </p>
              </div>
            </div>

            <div className={styles.welcomeStep}>
              <span className={styles.welcomeStepNumber}>3</span>
              <div className={styles.welcomeStepBody}>
                <strong className={styles.welcomeStepTitle}>Switch views and compare</strong>
                <p className={styles.welcomeStepCopy}>
                  Move between waveform, FFT, and spectrogram, then compare runs from the same
                  workspace.
                </p>
              </div>
            </div>
          </div>
        </section>

        <section className={styles.welcomeGrid}>
          <article className={styles.welcomeCard}>
            <p className={styles.sectionEyebrow}>Analysis</p>
            <h4 className={styles.welcomeCardTitle}>Inspect the same file three ways</h4>
            <p className={styles.welcomeCardCopy}>
              Waveform gives the time view, FFT shows frequency content, and spectrogram shows
              how energy changes over time.
            </p>
          </article>

          <article className={styles.welcomeCard}>
            <p className={styles.sectionEyebrow}>Compare</p>
            <h4 className={styles.welcomeCardTitle}>Check multiple runs side by side</h4>
            <p className={styles.welcomeCardCopy}>
              Once files are imported, use the checkboxes in the file rail to compare stacked,
              grid, or overlay views.
            </p>
          </article>

          <article className={styles.welcomeCard}>
            <p className={styles.sectionEyebrow}>Workspace</p>
            <h4 className={styles.welcomeCardTitle}>Keep one focused analysis session</h4>
            <p className={styles.welcomeCardCopy}>
              Imports stay in the current workspace so you can move between files, transforms,
              and details without reloading them.
            </p>
          </article>
        </section>
      </div>
    )
  }

  const failedEntries = batches.flatMap((batch) =>
    batch.failedPaths.map((failure) => ({
      batchId: batch.id,
      path: failure.path,
      reason: failure.reason,
    })),
  )
  const detailItems = selectedFile ? getDetailItems(selectedFile) : []
  const metadataEntries = selectedFile
    ? Object.entries(selectedFile.metadata).slice(0, 3)
    : []
  const isAudioFile = selectedFile?.signalKind === 'audio'
  const allowOverlay = activeView !== 'spectrogram'
  const effectiveLayout =
    isCompareMode && layoutMode === 'overlay' && !allowOverlay ? 'stack' : layoutMode
  const useCompactCharts = isCompareMode && effectiveLayout !== 'overlay'
  const analysisFiles = selectedFile
    ? isCompareMode
      ? [selectedFile, ...compareFiles]
      : [selectedFile]
    : []
  const selectedFileTransforms = selectedFile?.signalKind === 'audio' ? activeTransforms : undefined
  const isSidebarOpen = activeSidebarPanel !== null

  return (
    <div className={styles.root} data-compare={isCompareMode ? 'true' : 'false'}>
      <div
        className={styles.workspaceGrid}
        data-compare={isCompareMode ? 'true' : 'false'}
      >
        <aside className={styles.fileRail}>
          <div className={styles.sectionHeader}>
            <p className={styles.sectionEyebrow}>Files</p>
          </div>

          <ul className={styles.fileList}>
            {importedFiles.map((file) => (
              <li key={file.id}>
                <div className={styles.fileRow}>
                  {selectedFile &&
                  file.id !== selectedFile.id &&
                  file.signalKind === selectedFile.signalKind ? (
                    <label className={styles.compareCheckbox}>
                      <input
                        checked={isFileSelectedForCompare(file.id)}
                        onChange={() => toggleComparisonFile(file.id)}
                        type="checkbox"
                      />
                      <span className={styles.compareCheckboxIndicator} />
                    </label>
                  ) : (
                    <span aria-hidden="true" className={styles.compareCheckboxSpacer} />
                  )}

                  <button
                    className={styles.fileButton}
                    data-active={selectedFile?.id === file.id}
                    onClick={() => selectFile(file.batchId, file.id)}
                    type="button"
                  >
                    <span className={styles.fileName}>{file.sourcePath}</span>
                  </button>
                </div>
              </li>
            ))}
          </ul>
        </aside>

        <section
          className={styles.previewPane}
          data-compare={isCompareMode ? 'true' : 'false'}
        >
          {failedEntries.length > 0 ? (
            <section className={styles.failureStrip}>
              <div className={styles.failureHeader}>
                <span className={styles.failureLabel}>
                  <CircleAlert className="size-4" />
                  Some files could not be imported
                </span>
                <span className={styles.failureCount}>{failedFileCount} failed</span>
              </div>
              <ul className={styles.failureList}>
                {failedEntries.slice(0, 2).map((failure) => (
                  <li className={styles.failureItem} key={`${failure.batchId}-${failure.path}`}>
                    <span className={styles.failurePath}>{failure.path}</span>
                    <span className={styles.failureReason}>{failure.reason}</span>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}

          {selectedFile ? (
            <>
              <div
                className={styles.previewLayout}
                data-compare={isCompareMode ? 'true' : 'false'}
                data-details-open={isSidebarOpen ? 'true' : 'false'}
              >
                <div
                  className={styles.previewCard}
                  data-compare={isCompareMode ? 'true' : 'false'}
                >
                  <div className={styles.previewHeader}>
                    <div className={styles.previewHeading}>
                      <p className={styles.sectionEyebrow}>
                        {isCompareMode ? 'Compare view' : 'Selected file'}
                      </p>
                      <h3 className={styles.previewTitle}>
                        {isCompareMode
                          ? `${analysisFiles.length} files`
                          : selectedFile.sourcePath}
                      </h3>
                    </div>

                    <div className={styles.analysisToolbar}>
                      {isAudioFile ? (
                        <>
                          <div
                            aria-label="Analysis views"
                            className={styles.tabList}
                            role="tablist"
                          >
                            {ANALYSIS_TABS.map((tab) => (
                              <button
                                aria-selected={activeView === tab.id}
                                className={styles.tabButton}
                                data-active={activeView === tab.id}
                                key={tab.id}
                                onClick={() => setActiveView(tab.id)}
                                role="tab"
                                type="button"
                              >
                                {tab.label}
                              </button>
                            ))}
                          </div>

                          <CompareControls
                            allowOverlay={allowOverlay}
                            canCompare={canCompare}
                            isCompareMode={isCompareMode}
                            layoutMode={effectiveLayout}
                            onClear={clearCompareSelection}
                            onLayoutChange={setLayoutMode}
                          />
                        </>
                      ) : null}

                      <div className={styles.sidebarActions}>
                        {selectedFile.signalKind === 'audio' ? (
                          <Button
                            aria-pressed={activeSidebarPanel === 'transforms'}
                            className={styles.sidebarToggle}
                            data-open={activeSidebarPanel === 'transforms'}
                            onClick={() =>
                              setActiveSidebarPanel((current) =>
                                current === 'transforms' ? null : 'transforms',
                              )
                            }
                            type="button"
                            variant="outline"
                          >
                            <SlidersHorizontal className="size-4" />
                            Transforms
                          </Button>
                        ) : null}

                        <Button
                          aria-label={
                            activeSidebarPanel === 'details'
                              ? 'Hide file details'
                              : 'Show file details'
                          }
                          aria-pressed={activeSidebarPanel === 'details'}
                          className={styles.sidebarToggle}
                          data-open={activeSidebarPanel === 'details'}
                          onClick={() =>
                            setActiveSidebarPanel((current) =>
                              current === 'details' ? null : 'details',
                            )
                          }
                          type="button"
                          variant="outline"
                        >
                          {activeSidebarPanel === 'details' ? (
                            <PanelRightClose className="size-4" />
                          ) : (
                            <PanelRightOpen className="size-4" />
                          )}
                          Details
                        </Button>
                      </div>
                    </div>
                  </div>

                  <div
                    className={styles.previewContent}
                    data-compare={isCompareMode ? 'true' : 'false'}
                  >
                    <div
                      className={styles.previewSurface}
                      data-compare={isCompareMode ? 'true' : 'false'}
                    >
                      {selectedFile.signalKind === 'audio' ? (
                        <div
                          className={styles.analysisCollection}
                          data-layout={isCompareMode ? effectiveLayout : 'single'}
                        >
                          {isCompareMode && effectiveLayout === 'overlay' ? (
                            <article className={styles.analysisItem} key="overlay">
                              <div className={styles.analysisItemHeader}>
                                <p className={styles.analysisItemEyebrow}>Overlay</p>
                                <h4 className={styles.analysisItemTitle}>
                                  {analysisFiles.length} files on one chart
                                </h4>
                              </div>

                              {activeView === 'waveform' ? (
                                <WaveformPanel
                                  comparisonFileIds={compareFiles.map((file) => file.id)}
                                  fileId={selectedFile.id}
                                  transforms={selectedFileTransforms}
                                />
                              ) : null}
                              {activeView === 'fft' ? (
                                <FftPanel
                                  comparisonFileIds={compareFiles.map((file) => file.id)}
                                  fileId={selectedFile.id}
                                  transforms={selectedFileTransforms}
                                />
                              ) : null}
                            </article>
                          ) : (
                            analysisFiles.map((file, index) => (
                              <article className={styles.analysisItem} key={file.id}>
                                {isCompareMode ? (
                                  <div className={styles.analysisItemHeader}>
                                    <p className={styles.analysisItemEyebrow}>
                                      {index === 0 ? 'Selected' : 'Compare'}
                                    </p>
                                    <h4 className={styles.analysisItemTitle}>
                                      {file.sourcePath}
                                    </h4>
                                  </div>
                                ) : null}

                                {activeView === 'waveform' ? (
                                  <WaveformPanel
                                    compact={useCompactCharts}
                                    fileId={file.id}
                                    transforms={index === 0 ? selectedFileTransforms : undefined}
                                  />
                                ) : null}
                                {activeView === 'fft' ? (
                                  <FftPanel
                                    compact={useCompactCharts}
                                    fileId={file.id}
                                    transforms={index === 0 ? selectedFileTransforms : undefined}
                                  />
                                ) : null}
                                {activeView === 'spectrogram' ? (
                                  <SpectrogramPanel
                                    compact={useCompactCharts}
                                    fileId={file.id}
                                    transforms={index === 0 ? selectedFileTransforms : undefined}
                                  />
                                ) : null}
                              </article>
                            ))
                          )}
                        </div>
                      ) : (
                        <div className={styles.previewFallback}>
                          <Database className="size-5" />
                          <p>Waveform, FFT, and spectrogram views are enabled for audio in this MVP.</p>
                        </div>
                      )}
                    </div>

                    {selectedFile.signalKind === 'audio' ? (
                      <div
                        className={styles.playerDock}
                        data-compare={isCompareMode ? 'true' : 'false'}
                      >
                        <AudioPreviewPlayer src={selectedFile.previewUrl} />
                      </div>
                    ) : null}
                  </div>
                </div>

                {activeSidebarPanel ? (
                  <aside className={styles.detailsSidebar}>
                    {activeSidebarPanel === 'details' ? (
                      <>
                        <div className={styles.sectionHeader}>
                          <p className={styles.sectionEyebrow}>File details</p>
                        </div>

                        {detailItems.length > 0 ? (
                          <div className={styles.metricColumn}>
                            {detailItems.map((item) => (
                              <div className={styles.metric} key={item.label}>
                                <span className={styles.metricLabel}>{item.label}</span>
                                <strong className={styles.metricValue}>{item.value}</strong>
                              </div>
                            ))}
                          </div>
                        ) : null}

                        {metadataEntries.length > 0 ? (
                          <div className={styles.detailsBlock}>
                            <dl className={styles.detailList}>
                              {metadataEntries.map(([key, value]) => (
                                <DetailRow key={key} label={toTitleCase(key)} value={value} />
                              ))}
                            </dl>
                          </div>
                        ) : null}
                      </>
                    ) : null}

                    {activeSidebarPanel === 'transforms' && selectedFile.signalKind === 'audio' ? (
                      <TransformsPanel
                        hasActiveTransforms={hasActiveTransforms}
                        onGainDbChange={setGainDb}
                        onNormalizeChange={setNormalize}
                        onReset={resetTransforms}
                        onTrimSilenceChange={setTrimSilence}
                        transforms={activeTransforms}
                      />
                    ) : null}
                  </aside>
                ) : null}
              </div>
            </>
          ) : selectedBatch ? (
            <div className={styles.emptySelection}>
              <p className={styles.sectionEyebrow}>Batch selected</p>
              <h3 className={styles.previewTitle}>No imported files in this batch</h3>
              <p className={styles.previewMeta}>
                Review the failures on the left and try importing again.
              </p>
            </div>
          ) : null}
        </section>
      </div>
    </div>
  )
}

interface IDetailItem {
  label: string
  value: string
}

interface IDetailRowProps {
  label: string
  value: string
}

function DetailRow({ label, value }: IDetailRowProps): JSX.Element {
  return (
    <div className={styles.detailRow}>
      <dt className={styles.detailLabel}>{label}</dt>
      <dd className={styles.detailValue}>{value}</dd>
    </div>
  )
}

function getDetailItems(file: {
  channelCount: number | null
  durationSeconds: number | null
  sampleRateHz: number | null
  sizeBytes: number
}): IDetailItem[] {
  const items: IDetailItem[] = []

  if (file.durationSeconds) {
    items.push({
      label: 'Duration',
      value: formatDuration(file.durationSeconds),
    })
  }

  if (file.sampleRateHz) {
    items.push({
      label: 'Sample rate',
      value: formatSampleRate(file.sampleRateHz),
    })
  }

  if (file.channelCount) {
    items.push({
      label: 'Channels',
      value: formatChannels(file.channelCount),
    })
  }

  if (file.sizeBytes > 0) {
    items.push({
      label: 'Size',
      value: formatBytes(file.sizeBytes),
    })
  }

  return items
}

function formatDuration(value: number | null): string {
  if (value === null || Number.isNaN(value)) {
    return 'Unavailable'
  }

  const minutes = Math.floor(value / 60)
  const seconds = Math.floor(value % 60)

  if (minutes === 0) {
    return `${seconds}s`
  }

  return `${minutes}m ${seconds.toString().padStart(2, '0')}s`
}

function formatSampleRate(value: number | null): string {
  if (!value) {
    return 'Unavailable'
  }

  return `${value.toLocaleString()} Hz`
}

function formatChannels(value: number | null): string {
  if (!value) {
    return 'Unavailable'
  }

  return value === 1 ? 'Mono' : `${value} channels`
}

function formatBytes(value: number): string {
  if (value <= 0) {
    return '0 B'
  }

  return `${byteFormatter.format(value)}B`
}

function toTitleCase(value: string): string {
  return value
    .replace(/([A-Z])/g, ' $1')
    .replace(/[-_]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim()
    .replace(/^\w/, (character) => character.toUpperCase())
}
