import { useEffect, useState } from 'react'
import type { ChangeEvent, JSX } from 'react'

import { Button } from '@/shared/ui/button'

import {
  type FilterMode,
  type IFilterRecipe,
  type ITransformRecipe,
} from '@/features/transforms/utils/types'
import styles from './FilteringPanel.module.scss'

interface FilteringPanelProps {
  onApply: (filter: IFilterRecipe) => void
  onReset: () => void
  transforms: ITransformRecipe
}

const FILTER_MODE_OPTIONS: Array<{ label: string; value: FilterMode }> = [
  { label: 'Off', value: 'none' },
  { label: 'Low-pass', value: 'lowpass' },
  { label: 'High-pass', value: 'highpass' },
  { label: 'Band-pass', value: 'bandpass' },
  { label: 'Notch', value: 'notch' },
]

export function FilteringPanel({
  onApply,
  onReset,
  transforms,
}: FilteringPanelProps): JSX.Element {
  const [draftFilter, setDraftFilter] = useState<IFilterRecipe>(transforms.filter)
  const isBandPass = draftFilter.mode === 'bandpass'
  const usesSingleCutoff =
    draftFilter.mode === 'lowpass' || draftFilter.mode === 'highpass' || draftFilter.mode === 'notch'
  const usesQ =
    draftFilter.mode === 'lowpass' || draftFilter.mode === 'highpass' || draftFilter.mode === 'notch'
  const hasPendingChanges = !filtersEqual(draftFilter, transforms.filter)

  useEffect(() => {
    setDraftFilter(transforms.filter)
  }, [
    transforms.filter.cutoffHz,
    transforms.filter.highCutoffHz,
    transforms.filter.lowCutoffHz,
    transforms.filter.mode,
    transforms.filter.q,
  ])

  const handleModeChange = (event: ChangeEvent<HTMLSelectElement>): void => {
    const mode = event.target.value as FilterMode

    setDraftFilter((current) => ({
      ...current,
      mode,
    }))
  }

  return (
    <section className={styles.root}>
      <div className={styles.header}>
        <p className={styles.eyebrow}>Filtering</p>
        <div className={styles.actions}>
          <Button
            disabled={transforms.filter.mode === 'none' && !hasPendingChanges}
            onClick={onReset}
            type="button"
            variant="outline"
          >
            Reset
          </Button>
          <Button disabled={!hasPendingChanges} onClick={() => onApply(draftFilter)} type="button">
            Apply
          </Button>
        </div>
      </div>

      <div className={styles.block}>
        <span className={styles.fieldLabel}>Mode</span>
        <span className={styles.fieldHint}>Choose the filter shape for the current preview.</span>
        <select className={styles.modeSelect} onChange={handleModeChange} value={draftFilter.mode}>
          {FILTER_MODE_OPTIONS.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
      </div>

      {usesSingleCutoff ? (
        <div className={styles.block}>
          <div className={styles.blockHeader}>
            <span className={styles.fieldLabel}>
              {draftFilter.mode === 'notch' ? 'Center frequency' : 'Cutoff'}
            </span>
            <strong className={styles.sliderValue}>{formatFrequency(draftFilter.cutoffHz)}</strong>
          </div>
          <input
            className={styles.slider}
            max="10000"
            min="20"
            onChange={(event) =>
              setDraftFilter((current) => ({
                ...current,
                cutoffHz: Number(event.target.value),
              }))
            }
            step="10"
            type="range"
            value={draftFilter.cutoffHz}
          />
        </div>
      ) : null}

      {isBandPass ? (
        <>
          <div className={styles.block}>
            <div className={styles.blockHeader}>
              <span className={styles.fieldLabel}>Low cutoff</span>
              <strong className={styles.sliderValue}>{formatFrequency(draftFilter.lowCutoffHz)}</strong>
            </div>
            <input
              className={styles.slider}
              max="9000"
              min="20"
              onChange={(event) =>
                setDraftFilter((current) => {
                  const nextLow = Math.min(Number(event.target.value), current.highCutoffHz - 50)

                  return {
                    ...current,
                    lowCutoffHz: nextLow,
                  }
                })
              }
              step="10"
              type="range"
              value={draftFilter.lowCutoffHz}
            />
          </div>

          <div className={styles.block}>
            <div className={styles.blockHeader}>
              <span className={styles.fieldLabel}>High cutoff</span>
              <strong className={styles.sliderValue}>{formatFrequency(draftFilter.highCutoffHz)}</strong>
            </div>
            <input
              className={styles.slider}
              max="10000"
              min="100"
              onChange={(event) =>
                setDraftFilter((current) => {
                  const nextHigh = Math.max(Number(event.target.value), current.lowCutoffHz + 50)

                  return {
                    ...current,
                    highCutoffHz: nextHigh,
                  }
                })
              }
              step="10"
              type="range"
              value={draftFilter.highCutoffHz}
            />
          </div>
        </>
      ) : null}

      {usesQ ? (
        <div className={styles.block}>
          <div className={styles.blockHeader}>
            <span className={styles.fieldLabel}>Resonance (Q)</span>
            <strong className={styles.sliderValue}>{draftFilter.q.toFixed(2)}</strong>
          </div>
          <input
            className={styles.slider}
            max="8"
            min="0.35"
            onChange={(event) =>
              setDraftFilter((current) => ({
                ...current,
                q: Number(event.target.value),
              }))
            }
            step="0.05"
            type="range"
            value={draftFilter.q}
          />
        </div>
      ) : null}
    </section>
  )
}

function formatFrequency(value: number): string {
  if (value >= 1000) {
    return `${(value / 1000).toFixed(2)} kHz`
  }

  return `${Math.round(value)} Hz`
}

function filtersEqual(left: IFilterRecipe, right: IFilterRecipe): boolean {
  return (
    left.mode === right.mode &&
    Math.abs(left.cutoffHz - right.cutoffHz) < 0.001 &&
    Math.abs(left.lowCutoffHz - right.lowCutoffHz) < 0.001 &&
    Math.abs(left.highCutoffHz - right.highCutoffHz) < 0.001 &&
    Math.abs(left.q - right.q) < 0.001
  )
}
