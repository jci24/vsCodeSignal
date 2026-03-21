import {
  DataZoomComponent,
  GridComponent,
  LegendComponent,
  TooltipComponent,
} from 'echarts/components'
import { LineChart, type LineSeriesOption } from 'echarts/charts'
import {
  CanvasRenderer,
} from 'echarts/renderers'
import * as echarts from 'echarts/core'
import type { ComposeOption } from 'echarts/core'
import { useEffect, useEffectEvent, useRef } from 'react'

import type { SignalSeries } from '@/entities/signal/model/types'
import { cn } from '@/shared/lib/cn'

type EChartsOption = ComposeOption<LineSeriesOption>

echarts.use([
  CanvasRenderer,
  DataZoomComponent,
  GridComponent,
  LegendComponent,
  LineChart,
  TooltipComponent,
])

export function SignalChart({
  className,
  series,
}: {
  className?: string
  series: SignalSeries[]
}) {
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
      animationDuration: 550,
      backgroundColor: 'transparent',
      dataZoom: [
        {
          bottom: 0,
          filterMode: 'none',
          height: 14,
          textStyle: {
            color: '#687185',
          },
          type: 'slider',
        },
      ],
      grid: {
        bottom: 34,
        containLabel: true,
        left: 18,
        right: 18,
        top: 34,
      },
      legend: {
        itemHeight: 10,
        itemWidth: 10,
        left: 'left',
        top: 0,
        textStyle: {
          color: '#475569',
          fontFamily: 'IBM Plex Sans',
        },
      },
      series: series.map((channel) => ({
        data: channel.points.map((point) => [point.timeMs, point.value]),
        lineStyle: {
          color: channel.color,
          width: 2.2,
        },
        name: channel.name,
        showSymbol: false,
        smooth: true,
        symbol: 'none',
        type: 'line',
      })),
      textStyle: {
        color: '#1e293b',
        fontFamily: 'IBM Plex Sans',
      },
      tooltip: {
        backgroundColor: 'rgba(255,255,255,0.96)',
        borderColor: 'rgba(148,163,184,0.28)',
        borderWidth: 1,
        textStyle: {
          color: '#0f172a',
        },
        trigger: 'axis',
      },
      xAxis: {
        axisLabel: {
          color: '#64748b',
          formatter: '{value} ms',
        },
        axisLine: {
          lineStyle: {
            color: '#cbd5e1',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(148,163,184,0.14)',
          },
        },
        type: 'value',
      },
      yAxis: {
        axisLabel: {
          color: '#64748b',
          formatter: '{value} g',
        },
        axisLine: {
          lineStyle: {
            color: '#cbd5e1',
          },
        },
        splitLine: {
          lineStyle: {
            color: 'rgba(148,163,184,0.14)',
          },
        },
        type: 'value',
      },
    }

    chartRef.current.setOption(option, true)
  }, [series])

  return <div className={cn('h-[22rem] min-h-[12rem] w-full', className)} ref={containerRef} />
}
