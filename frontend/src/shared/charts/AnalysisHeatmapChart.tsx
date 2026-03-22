import type { JSX } from 'react'
import { useEffect, useEffectEvent, useRef } from 'react'
import { HeatmapChart, type HeatmapSeriesOption } from 'echarts/charts'
import {
  GridComponent,
  TooltipComponent,
  VisualMapComponent,
} from 'echarts/components'
import * as echarts from 'echarts/core'
import type { ComposeOption } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

import styles from './AnalysisHeatmapChart.module.scss'

type EChartsOption = ComposeOption<HeatmapSeriesOption>

echarts.use([
  CanvasRenderer,
  GridComponent,
  HeatmapChart,
  TooltipComponent,
  VisualMapComponent,
])

export interface IAnalysisHeatmapPoint {
  frequencyIndex: number
  intensity: number
  timeIndex: number
}

interface AnalysisHeatmapChartProps {
  className?: string
  compact?: boolean
  frequencies: number[]
  points: IAnalysisHeatmapPoint[]
  times: number[]
}

export function AnalysisHeatmapChart({
  className,
  compact = false,
  frequencies,
  points,
  times,
}: AnalysisHeatmapChartProps): JSX.Element {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const chartRef = useRef<echarts.EChartsType | null>(null)
  const resizeTimeoutRef = useRef<number | null>(null)

  const resizeChart = useEffectEvent(() => {
    chartRef.current?.resize()
  })

  const scheduleResize = useEffectEvent(() => {
    window.requestAnimationFrame(() => {
      chartRef.current?.resize()

      window.requestAnimationFrame(() => {
        chartRef.current?.resize()
      })
    })

    if (resizeTimeoutRef.current !== null) {
      window.clearTimeout(resizeTimeoutRef.current)
    }

    resizeTimeoutRef.current = window.setTimeout(() => {
      chartRef.current?.resize()
      resizeTimeoutRef.current = null
    }, 120)
  })

  useEffect(() => {
    if (!containerRef.current) {
      return
    }

    chartRef.current = echarts.init(containerRef.current, undefined, {
      renderer: 'canvas',
    })

    const observer = new ResizeObserver(() => {
      resizeChart()
    })

    observer.observe(containerRef.current)
    scheduleResize()

    return () => {
      if (resizeTimeoutRef.current !== null) {
        window.clearTimeout(resizeTimeoutRef.current)
        resizeTimeoutRef.current = null
      }

      observer.disconnect()
      chartRef.current?.dispose()
      chartRef.current = null
    }
  }, [resizeChart, scheduleResize])

  useEffect(() => {
    if (!chartRef.current) {
      return
    }

    const option: EChartsOption = {
      animation: false,
      backgroundColor: 'transparent',
      grid: {
        bottom: 20,
        containLabel: true,
        left: 18,
        right: 18,
        top: 24,
      },
      series: [
        {
          data: points.map((point) => [
            point.timeIndex,
            point.frequencyIndex,
            point.intensity,
          ]),
          itemStyle: {
            borderRadius: 2,
          },
          progressive: 3_000,
          type: 'heatmap',
        },
      ],
      textStyle: {
        color: '#111827',
        fontFamily: 'IBM Plex Sans, sans-serif',
      },
      tooltip: {
        backgroundColor: 'rgba(255,255,255,0.98)',
        borderColor: 'rgba(17,24,39,0.08)',
        borderWidth: 1,
        extraCssText:
          'border-radius: 14px; box-shadow: 0 18px 34px -24px rgba(15,23,42,0.28);',
        formatter: (params: { data?: [number, number, number] }) => {
          const data = params.data

          if (!data) {
            return ''
          }

          const timeValue = times[data[0]] ?? 0
          const frequencyValue = frequencies[data[1]] ?? 0

          return [
            `<strong>${formatSeconds(timeValue)}</strong>`,
            `${formatFrequency(frequencyValue)}`,
            `Intensity ${Math.round(data[2] * 100)}%`,
          ].join('<br/>')
        },
        textStyle: {
          color: '#111827',
          fontFamily: 'IBM Plex Sans, sans-serif',
          fontSize: 12,
        },
      },
      visualMap: {
        calculable: false,
        inRange: {
          color: [
            '#fbfbfd',
            '#e8eef8',
            '#bed8f5',
            '#86b6ef',
            '#4586dc',
            '#2150a8',
            '#1d8f8a',
            '#79c75b',
            '#f0bf4c',
            '#e56b3d',
          ],
        },
        max: 1,
        min: 0,
        orient: 'vertical',
        right: 0,
        show: false,
        top: 'middle',
      },
      xAxis: {
        axisLabel: {
          color: '#6b7280',
          fontFamily: 'IBM Plex Sans, sans-serif',
          fontSize: 11,
          formatter: (value: string) => formatSeconds(times[Number(value)] ?? 0),
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.08)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.03)',
          },
        },
        data: times.map((_, index) => index),
        type: 'category',
      },
      yAxis: {
        axisLabel: {
          color: '#6b7280',
          fontFamily: 'IBM Plex Sans, sans-serif',
          fontSize: 11,
          formatter: (value: string) => formatFrequency(frequencies[Number(value)] ?? 0),
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.08)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.03)',
          },
        },
        data: frequencies.map((_, index) => index),
        type: 'category',
      },
    }

    chartRef.current.clear()
    chartRef.current.setOption(option, {
      lazyUpdate: true,
      notMerge: true,
      replaceMerge: ['series', 'xAxis', 'yAxis', 'visualMap'],
    })
    scheduleResize()
  }, [frequencies, points, scheduleResize, times])

  return (
    <div
      className={[styles.root, compact ? styles.compact : null, className]
        .filter(Boolean)
        .join(' ')}
      ref={containerRef}
    />
  )
}

function formatSeconds(value: number): string {
  if (value >= 10) {
    return `${value.toFixed(0)}s`
  }

  return `${value.toFixed(1)}s`
}

function formatFrequency(value: number): string {
  if (value >= 1000) {
    return `${(value / 1000).toFixed(1)} kHz`
  }

  return `${Math.round(value)} Hz`
}
