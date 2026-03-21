import { BotMessageSquare, Sparkles, WandSparkles } from 'lucide-react'

import { Button } from '@/shared/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/shared/ui/card'
import { ScrollArea } from '@/shared/ui/scroll-area'

const prompts = [
  'Explain the shift in phase after 1200ms',
  'Suggest the next filter to isolate drift noise',
  'Summarize the selected segment for a report',
]

const insights = [
  {
    emphasis: 'Most likely cause',
    text: 'The filtered torque channel stabilizes later than the raw IMU trace, which hints at downstream resampling lag instead of sensor dropout.',
  },
  {
    emphasis: 'Recommended next step',
    text: 'Overlay the normalized and low-pass outputs with cross-correlation enabled to confirm whether the delay is systematic across captures.',
  },
]

export function AiReasoningPanel() {
  return (
    <Card className="panel-surface flex h-full min-h-0 flex-col rounded-[1.5rem] border">
      <CardHeader className="space-y-2">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <BotMessageSquare className="h-4 w-4" />
          AI reasoning
        </div>
        <CardTitle className="text-xl">Context-aware copilot</CardTitle>
        <CardDescription>
          Prompt stubs ready to be grounded in the active selection, pipeline
          trace, and signal metadata.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex min-h-0 flex-1 flex-col gap-4">
        <div className="shrink-0 rounded-2xl border border-border/70 bg-background/70 p-4">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Quick prompts
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            {prompts.map((prompt) => (
              <Button
                key={prompt}
                className="justify-start rounded-full"
                size="sm"
                variant="outline"
              >
                <Sparkles className="mr-2 h-3.5 w-3.5" />
                {prompt}
              </Button>
            ))}
          </div>
        </div>

        <ScrollArea className="min-h-0 flex-1 pr-4">
          <div className="space-y-3">
            <div className="rounded-2xl border border-border/70 bg-secondary/70 p-4">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                Working theory
              </p>
              <p className="mt-2 text-sm text-muted-foreground">
                These grouped notes are the pieces the assistant should reason
                about first before it attempts a full narrative summary.
              </p>
            </div>
            {insights.map((insight) => (
              <div
                key={insight.emphasis}
                className="rounded-2xl border border-border/70 bg-secondary/70 p-4"
              >
                <div className="flex items-center gap-2 text-sm font-semibold">
                  <WandSparkles className="h-4 w-4 text-primary" />
                  {insight.emphasis}
                </div>
                <p className="mt-2 text-sm text-muted-foreground">
                  {insight.text}
                </p>
              </div>
            ))}
          </div>
        </ScrollArea>
      </CardContent>
    </Card>
  )
}
