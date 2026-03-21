import { CheckCircle2, LoaderCircle, TriangleAlert } from 'lucide-react'

import type { PipelineStep } from '@/entities/pipeline/model/types'
import { Badge } from '@/shared/ui/badge'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/card'
import { ScrollArea } from '@/shared/ui/scroll-area'

export function PipelineTrace({ steps }: { steps: PipelineStep[] }) {
  return (
    <Card className="panel-surface flex h-full min-h-0 flex-col rounded-[1.5rem] border">
      <CardHeader className="space-y-2">
        <CardTitle className="text-xl">Pipeline trace</CardTitle>
        <CardDescription>
          Execution detail area for reusable pipelines, step diagnostics, and
          runtime hints from the backend.
        </CardDescription>
      </CardHeader>
      <CardContent className="min-h-0 flex-1">
        <ScrollArea className="h-full pr-4">
          <div className="space-y-3">
            {steps.map((step, index) => (
              <div
                key={step.name}
                className="grid gap-3 rounded-2xl border border-border/70 bg-background/80 p-4 md:grid-cols-[auto_minmax(0,1fr)_auto]"
              >
                <div className="flex items-center gap-3">
                  <span className="inline-flex h-9 w-9 items-center justify-center rounded-2xl bg-secondary font-semibold">
                    {index + 1}
                  </span>
                  <StatusIcon status={step.status} />
                </div>
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <p className="font-semibold">{step.name}</p>
                    <Badge variant="outline">{step.durationMs} ms</Badge>
                  </div>
                  <p className="text-sm text-muted-foreground">{step.summary}</p>
                </div>
                <Badge
                  className={
                    step.status === 'running'
                      ? 'bg-chart-3/15 text-chart-3'
                      : step.status === 'warning'
                        ? 'bg-amber-500/15 text-amber-700'
                        : 'bg-chart-2/15 text-chart-2'
                  }
                >
                  {step.status}
                </Badge>
              </div>
            ))}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  )
}

function StatusIcon({ status }: { status: PipelineStep['status'] }) {
  if (status === 'warning') {
    return <TriangleAlert className="h-4 w-4 text-amber-700" />
  }

  if (status === 'running') {
    return <LoaderCircle className="h-4 w-4 animate-spin text-chart-3" />
  }

  return <CheckCircle2 className="h-4 w-4 text-chart-2" />
}
