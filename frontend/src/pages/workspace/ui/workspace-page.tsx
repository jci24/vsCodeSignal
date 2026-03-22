import type { JSX } from 'react'
import { useEffect, useState } from 'react'
import { CircleAlert, Database, PanelRightClose, PanelRightOpen, RefreshCw } from 'lucide-react'

import { CompareControls } from '@/features/compare-mode/components/CompareControls/CompareControls'
import { useCompareSelection } from '@/features/compare-mode/hooks/useCompareSelection'
import { FftPanel } from '@/features/fft/components/FftPanel/FftPanel'
import { SpectrogramPanel } from '@/features/spectrogram/components/SpectrogramPanel/SpectrogramPanel'
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

export function WorkspacePage(): JSX.Element {
  const [activeView, setActiveView] = useState<AnalysisView>('waveform')
  const [isDetailsOpen, setIsDetailsOpen] = useState(false)
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

  useEffect(() => {
    if (!selectedFile) {
      setIsDetailsOpen(false)
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
      <div className={styles.state}>
        <p className={styles.stateEyebrow}>Workspace</p>
        <h3 className={styles.stateTitle}>No imports yet</h3>
        <p className={styles.stateCopy}>
          Import audio or signal files from the top-right action to start a workspace session.
        </p>
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
                data-details-open={isDetailsOpen ? 'true' : 'false'}
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

                      <Button
                        aria-label={isDetailsOpen ? 'Hide file details' : 'Show file details'}
                        className={styles.detailsToggle}
                        data-open={isDetailsOpen}
                        onClick={() => setIsDetailsOpen((current) => !current)}
                        type="button"
                        variant="outline"
                      >
                        {isDetailsOpen ? (
                          <>
                            <PanelRightClose className="size-4" />
                            Hide details
                          </>
                        ) : (
                          <>
                            <PanelRightOpen className="size-4" />
                            Show details
                          </>
                        )}
                      </Button>
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
                                />
                              ) : null}
                              {activeView === 'fft' ? (
                                <FftPanel
                                  comparisonFileIds={compareFiles.map((file) => file.id)}
                                  fileId={selectedFile.id}
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
                                  <WaveformPanel compact={useCompactCharts} fileId={file.id} />
                                ) : null}
                                {activeView === 'fft' ? (
                                  <FftPanel compact={useCompactCharts} fileId={file.id} />
                                ) : null}
                                {activeView === 'spectrogram' ? (
                                  <SpectrogramPanel compact={useCompactCharts} fileId={file.id} />
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

                {isDetailsOpen ? (
                  <aside className={styles.detailsSidebar}>
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
