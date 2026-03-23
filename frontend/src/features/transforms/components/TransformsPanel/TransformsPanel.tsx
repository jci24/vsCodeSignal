import type { ChangeEvent, JSX } from 'react'

import { Button } from '@/shared/ui/button'

import type { ITransformRecipe } from '../../utils/types'
import styles from './TransformsPanel.module.scss'

interface TransformsPanelProps {
  hasActiveTransforms: boolean
  onGainDbChange: (gainDb: number) => void
  onNormalizeChange: (normalize: boolean) => void
  onReset: () => void
  onTrimSilenceChange: (trimSilence: boolean) => void
  transforms: ITransformRecipe
}

export function TransformsPanel({
  hasActiveTransforms,
  onGainDbChange,
  onNormalizeChange,
  onReset,
  onTrimSilenceChange,
  transforms,
}: TransformsPanelProps): JSX.Element {
  const handleGainChange = (event: ChangeEvent<HTMLInputElement>): void => {
    onGainDbChange(Number(event.target.value))
  }

  return (
    <section className={styles.root}>
      <div className={styles.header}>
        <div>
          <p className={styles.eyebrow}>Transforms</p>
          <p className={styles.copy}>Applies to the selected file analysis preview.</p>
        </div>
        <Button
          disabled={!hasActiveTransforms}
          onClick={onReset}
          type="button"
          variant="outline"
        >
          Reset
        </Button>
      </div>

      <label className={styles.toggleRow}>
        <span>
          <span className={styles.toggleLabel}>Normalize</span>
          <span className={styles.toggleHint}>Scale peak level to full range.</span>
        </span>
        <input
          checked={transforms.normalize}
          onChange={(event) => onNormalizeChange(event.target.checked)}
          type="checkbox"
        />
      </label>

      <label className={styles.toggleRow}>
        <span>
          <span className={styles.toggleLabel}>Trim silence</span>
          <span className={styles.toggleHint}>Remove quiet leading and trailing sections.</span>
        </span>
        <input
          checked={transforms.trimSilence}
          onChange={(event) => onTrimSilenceChange(event.target.checked)}
          type="checkbox"
        />
      </label>

      <div className={styles.gainBlock}>
        <div className={styles.gainHeader}>
          <span className={styles.toggleLabel}>Gain</span>
          <strong className={styles.gainValue}>
            {transforms.gainDb > 0 ? '+' : ''}
            {transforms.gainDb.toFixed(1)} dB
          </strong>
        </div>
        <input
          className={styles.gainSlider}
          max="12"
          min="-12"
          onChange={handleGainChange}
          step="0.5"
          type="range"
          value={transforms.gainDb}
        />
      </div>
    </section>
  )
}
