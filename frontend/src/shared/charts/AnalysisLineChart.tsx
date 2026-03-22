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
  points: IAnalysisLinePoint[]
}

interface AnalysisLineChartProps {
  className?: string
  series: IAnalysisLineSeries[]
  xAxisFormatter?: (value: number) => string
  yAxisFormatter?: (value: number) => string
}

export function AnalysisLineChart({
  className,
  series,
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
      animationDuration: 240,
      backgroundColor: 'transparent',
      dataZoom: [
        {
          bottom: 4,
          brushSelect: false,
          filterMode: 'none',
          height: 12,
          moveOnMouseMove: true,
          textStyle: {
            color: '#6b7280',
          },
          type: 'slider',
        },
      ],
      grid: {
        bottom: 32,
        containLabel: true,
        left: 10,
        right: 12,
        top: 20,
      },
      series: series.map((entry) => ({
        data: entry.points.map((point) => [point.x, point.y]),
        lineStyle: {
          color: entry.color,
          width: 1.8,
        },
        name: entry.name,
        showSymbol: false,
        smooth: false,
        symbol: 'none',
        type: 'line',
      })),
      textStyle: {
        color: '#111827',
        fontFamily: 'IBM Plex Sans',
      },
      tooltip: {
        axisPointer: {
          lineStyle: {
            color: 'rgba(17,24,39,0.2)',
          },
        },
        backgroundColor: 'rgba(255,255,255,0.96)',
        borderColor: 'rgba(17,24,39,0.12)',
        borderWidth: 1,
        textStyle: {
          color: '#111827',
        },
        trigger: 'axis',
      },
      xAxis: {
        axisLabel: {
          color: '#6b7280',
          formatter: (value: number | string) =>
            xAxisFormatter ? xAxisFormatter(Number(value)) : String(value),
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.12)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.06)',
          },
        },
        type: 'value',
      },
      yAxis: {
        axisLabel: {
          color: '#6b7280',
          formatter: (value: number | string) =>
            yAxisFormatter ? yAxisFormatter(Number(value)) : String(value),
        },
        axisLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.12)',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(17,24,39,0.06)',
          },
        },
        type: 'value',
      },
    }

    chartRef.current.setOption(option, true)
  }, [series, xAxisFormatter, yAxisFormatter])

  return (
    <div
      className={[styles.root, className].filter(Boolean).join(' ')}
      ref={containerRef}
    />
  )
}
