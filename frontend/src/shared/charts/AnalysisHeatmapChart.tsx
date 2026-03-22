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
  frequencyHz: number
  intensity: number
  timeSeconds: number
}

interface AnalysisHeatmapChartProps {
  className?: string
  points: IAnalysisHeatmapPoint[]
}

export function AnalysisHeatmapChart({
  className,
  points,
}: AnalysisHeatmapChartProps): JSX.Element {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const chartRef = useRef<echarts.EChartsType | null>(null)

  const resizeChart = useEffectEvent(() => {
    chartRef.current?.resize()
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

    return () => {
      observer.disconnect()
      chartRef.current?.dispose()
      chartRef.current = null
    }
  }, [resizeChart])

  useEffect(() => {
    if (!chartRef.current) {
      return
    }

    const option: EChartsOption = {
      animationDuration: 220,
      backgroundColor: 'transparent',
      grid: {
        bottom: 14,
        containLabel: true,
        left: 10,
        right: 18,
        top: 20,
      },
      series: [
        {
          data: points.map((point) => [
            point.timeSeconds,
            point.frequencyHz,
            point.intensity,
          ]),
          progressive: 3_000,
          type: 'heatmap',
        },
      ],
      textStyle: {
        color: '#111827',
        fontFamily: 'IBM Plex Sans',
      },
      tooltip: {
        backgroundColor: 'rgba(255,255,255,0.96)',
        borderColor: 'rgba(17,24,39,0.12)',
        borderWidth: 1,
        formatter: (params: { data?: [number, number, number] }) => {
          const data = params.data

          if (!data) {
            return ''
          }

          return [
            `<strong>${formatSeconds(data[0])}</strong>`,
            `${formatFrequency(data[1])}`,
            `Intensity ${Math.round(data[2] * 100)}%`,
          ].join('<br/>')
        },
        textStyle: {
          color: '#111827',
        },
      },
      visualMap: {
        calculable: false,
        inRange: {
          color: ['#fafaf9', '#d6d3d1', '#78716c', '#1c1917'],
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
          formatter: (value: number | string) => formatSeconds(Number(value)),
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.12)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.04)',
          },
        },
        type: 'value',
      },
      yAxis: {
        axisLabel: {
          color: '#6b7280',
          formatter: (value: number | string) => formatFrequency(Number(value)),
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.12)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.04)',
          },
        },
        type: 'value',
      },
    }

    chartRef.current.setOption(option, true)
  }, [points])

  return (
    <div
      className={[styles.root, className].filter(Boolean).join(' ')}
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
