import type { PipelineStep } from '@/entities/pipeline/model/types'

export const pipelineSteps: PipelineStep[] = [
  {
    durationMs: 8,
    name: 'Normalize amplitude',
    status: 'ok',
    summary: 'Zero-centered the raw IMU signal and preserved the filtered torque baseline.',
  },
  {
    durationMs: 12,
    name: 'Low-pass filter',
    status: 'ok',
    summary: 'Removed high-frequency jitter while keeping the drift shoulder visible in the selected window.',
  },
  {
    durationMs: 19,
    name: 'FFT / anomaly score',
    status: 'running',
    summary: 'Scoring harmonic leakage around 81Hz with candidate drift correlation.',
  },
  {
    durationMs: 6,
    name: 'Report draft hook',
    status: 'warning',
    summary: 'Awaiting AI summary to complete the export narrative for the selected range.',
  },
]

export const pipelineInsights = [
  {
    label: 'Dominant band',
    note: 'Energy increases by 11% after the resample stage.',
    value: '81 Hz',
  },
  {
    label: 'Cross-channel lag',
    note: 'Torque filtered channel trails the IMU raw series.',
    value: '14 ms',
  },
  {
    label: 'Confidence',
    note: 'Enough evidence to suggest a deterministic processing artifact.',
    value: '0.87',
  },
]
