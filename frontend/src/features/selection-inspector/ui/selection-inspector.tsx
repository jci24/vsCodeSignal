import { Activity, Crosshair, Gauge, ScissorsLineDashed } from 'lucide-react'

import { selectedWindow } from '@/entities/signal/model/mock-signals'
import { Badge } from '@/shared/ui/badge'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/card'

export function SelectionInspector() {
  return (
    <Card className="panel-surface flex h-full min-h-0 flex-col rounded-[1.5rem] border">
      <CardHeader className="space-y-2">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Crosshair className="h-4 w-4" />
          Selection inspector
        </div>
        <CardTitle className="text-xl">Focused region</CardTitle>
        <CardDescription>
          The current cursor window is grouped into one summary, one metric set,
          and one decision note.
        </CardDescription>
      </CardHeader>
      <CardContent className="min-h-0 flex-1">
        <div className="grid h-full min-h-0 gap-3">
          <div className="rounded-2xl border border-border/70 bg-secondary/70 px-4 py-3">
            <div className="flex items-center justify-between gap-3">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                Selection summary
              </p>
              <Badge className="bg-chart-3/15 text-chart-3">watch</Badge>
            </div>
            <p className="mt-2 text-xl font-semibold text-foreground">
              {selectedWindow.startMs}ms to {selectedWindow.endMs}ms
            </p>
            <p className="mt-2 text-sm text-muted-foreground">
              First phase shift after the resample stage. This is the handoff
              point the AI panel should explain first.
            </p>
          </div>

          <div className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Signal metrics
            </p>
            <div className="grid grid-cols-2 gap-3">
              <InspectorStat icon={ScissorsLineDashed} label="Range" value="180ms" />
              <InspectorStat icon={Gauge} label="Peak drift" value="7.2%" />
              <InspectorStat icon={Activity} label="RMS delta" value="0.18 g" />
              <InspectorStat icon={Crosshair} label="Cursor" value="1.24s" />
            </div>
          </div>

          <div className="rounded-2xl border border-border/70 bg-background/70 px-4 py-3">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Decision note
            </p>
            <p className="mt-2 text-sm text-muted-foreground">
              Compare the normalized and filtered channels before asking the AI
              assistant for a report summary. This keeps the explanation grounded
              in the same chunk of data the user is currently viewing.
            </p>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}

function InspectorStat({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Activity
  label: string
  value: string
}) {
  return (
    <div className="rounded-2xl border border-border/70 bg-background/70 p-3">
      <div className="flex items-center gap-2 text-xs uppercase tracking-[0.16em] text-muted-foreground">
        <Icon className="h-3.5 w-3.5" />
        {label}
      </div>
      <p className="mt-2 text-lg font-semibold">{value}</p>
    </div>
  )
}
