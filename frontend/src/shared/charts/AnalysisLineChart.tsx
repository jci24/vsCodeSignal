import type { JSX } from 'react'
import { useEffect, useEffectEvent, useRef } from 'react'
import { LineChart, type LineSeriesOption } from 'echarts/charts'
import {
  DataZoomComponent,
  GridComponent,
  TooltipComponent,
} from 'echarts/components'
import * as echarts from 'echarts/core'
import type { ComposeOption } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'

import styles from './AnalysisLineChart.module.scss'

type EChartsOption = ComposeOption<LineSeriesOption>

echarts.use([
  CanvasRenderer,
  DataZoomComponent,
  GridComponent,
  LineChart,
  TooltipComponent,
])

export interface IAnalysisLinePoint {
  x: number
  y: number
}

export interface IAnalysisLineSeries {
  color: string
  id: string
  name: string
  opacity?: number
  points: IAnalysisLinePoint[]
  width?: number
}

interface AnalysisLineChartProps {
  className?: string
  compact?: boolean
  series: IAnalysisLineSeries[]
  xAxisMin?: number
  xAxisType?: 'log' | 'value'
  xAxisFormatter?: (value: number) => string
  yAxisFormatter?: (value: number) => string
}

export function AnalysisLineChart({
  className,
  compact = false,
  series,
  xAxisMin,
  xAxisType = 'value',
  xAxisFormatter,
  yAxisFormatter,
}: AnalysisLineChartProps): JSX.Element {
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
      animation: false,
      backgroundColor: 'transparent',
      dataZoom: [
        {
          backgroundColor: 'rgba(17,24,39,0.04)',
          brushSelect: false,
          borderColor: 'rgba(17,24,39,0.08)',
          bottom: 8,
          fillerColor: 'rgba(17,24,39,0.08)',
          filterMode: 'none',
          handleIcon:
            'path://M0 0H12V12H0z',
          handleSize: 14,
          handleStyle: {
            borderColor: 'rgba(17,24,39,0.18)',
            color: 'rgba(255,255,255,0.96)',
            shadowBlur: 0,
          },
          height: 10,
          moveOnMouseMove: true,
          moveHandleSize: 0,
          selectedDataBackground: {
            lineStyle: {
              color: 'rgba(17,24,39,0.2)',
            },
          },
          showDataShadow: false,
          showDetail: false,
          textStyle: {
            color: '#6b7280',
            fontFamily: 'IBM Plex Sans, sans-serif',
            fontSize: 11,
          },
          type: 'slider',
        },
      ],
      grid: {
        bottom: compact ? 26 : 34,
        containLabel: true,
        left: compact ? 40 : 28,
        right: 18,
        top: compact ? 12 : 24,
      },
      series: series.map((entry) => ({
        data: entry.points.map((point) => [point.x, point.y]),
        lineStyle: {
          color: entry.color,
          opacity: entry.opacity ?? 1,
          width: entry.width ?? 1.7,
        },
        name: entry.name,
        showSymbol: false,
        smooth: false,
        symbol: 'none',
        type: 'line',
      })),
      textStyle: {
        color: '#111827',
        fontFamily: 'IBM Plex Sans, sans-serif',
      },
      tooltip: {
        axisPointer: {
          lineStyle: {
            color: 'rgba(17,24,39,0.18)',
          },
        },
        backgroundColor: 'rgba(255,255,255,0.98)',
        borderColor: 'rgba(17,24,39,0.08)',
        borderWidth: 1,
        extraCssText:
          'border-radius: 14px; box-shadow: 0 18px 34px -24px rgba(15,23,42,0.28);',
        padding: [10, 12],
        textStyle: {
          color: '#111827',
          fontFamily: 'IBM Plex Sans, sans-serif',
          fontSize: 12,
        },
        trigger: 'axis',
      },
      xAxis: {
        axisLabel: {
          color: '#6b7280',
          fontFamily: 'IBM Plex Sans, sans-serif',
          fontSize: compact ? 10 : 11,
          hideOverlap: true,
          formatter: (value: number | string) =>
            xAxisFormatter ? xAxisFormatter(Number(value)) : String(value),
          margin: compact ? 8 : 12,
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.08)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.045)',
          },
        },
        min: xAxisMin,
        type: xAxisType,
      },
      yAxis: {
        axisLabel: {
          color: '#6b7280',
          fontFamily: 'IBM Plex Sans, sans-serif',
          fontSize: compact ? 10 : 11,
          hideOverlap: true,
          formatter: (value: number | string) =>
            yAxisFormatter ? yAxisFormatter(Number(value)) : String(value),
          margin: compact ? 8 : 12,
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.08)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.045)',
          },
        },
        splitNumber: compact ? 3 : 5,
        type: 'value',
      },
    }

    chartRef.current.setOption(option, {
      lazyUpdate: true,
      notMerge: false,
      replaceMerge: ['series', 'xAxis', 'yAxis'],
    })
  }, [series, xAxisFormatter, xAxisMin, xAxisType, yAxisFormatter])

  return (
    <div
      className={[styles.root, compact ? styles.compact : null, className]
        .filter(Boolean)
        .join(' ')}
      ref={containerRef}
    />
  )
}
