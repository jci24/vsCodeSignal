import { BotMessageSquare, Sparkles, WandSparkles } from 'lucide-react'

import type { IAiSummaryCard } from '@/entities/assistant/model/types'
import { Button } from '@/shared/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { LoadingSpinner } from '@/shared/ui/loading-spinner'

interface AssistantBriefingProps {
  isPlanning?: boolean
  isSummaryLoading?: boolean
  onOpenDrawer: () => void
  onPlanFft: () => void
  onPlanHighPassCompare: () => void
  onQuickCompare: () => void
  onQuickExplain: () => void
  onQuickRecommend: () => void
  summaryCard: IAiSummaryCard | null
}

export function AssistantBriefing({
  isPlanning = false,
  isSummaryLoading = false,
  onOpenDrawer,
  onPlanFft,
  onPlanHighPassCompare,
  onQuickCompare,
  onQuickExplain,
  onQuickRecommend,
  summaryCard,
}: AssistantBriefingProps) {
  const nextSteps = summaryCard?.nextSteps.slice(0, 2) ?? []
  const keyFacts = summaryCard?.keyFacts.slice(0, 3) ?? []

  return (
    <div className="flex h-full min-h-0 flex-col gap-4">
      <Card className="rounded-[1.75rem] border border-border/70 bg-background/85 shadow-[0_16px_40px_-32px_rgba(15,23,42,0.28)]">
        <CardHeader className="space-y-3">
          <div className="flex items-start justify-between gap-3">
            <div className="space-y-1">
              <p className="text-xs font-semibold uppercase tracking-[0.2em] text-muted-foreground">
                AI briefing
              </p>
              <CardTitle>{summaryCard?.title ?? 'Signal summary'}</CardTitle>
            </div>
            <div className="flex shrink-0 items-center gap-2">
              <Button className="rounded-full" onClick={onOpenDrawer} size="sm" type="button">
                <BotMessageSquare className="mr-2 size-4" />
                Open
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent className="space-y-4">
          {isSummaryLoading ? (
            <LoadingSpinner label="Refreshing briefing" />
          ) : (
            <p className="text-sm leading-6 text-foreground">
              {summaryCard?.summary ?? 'Open the assistant drawer to ask a grounded question about the current signal.'}
            </p>
          )}

          {keyFacts.length > 0 ? (
            <div className="grid gap-2">
              {keyFacts.map((fact) => (
                <div
                  className="rounded-2xl border border-border/60 bg-secondary/35 px-3 py-2"
                  key={fact.code}
                >
                  <p className="text-[11px] uppercase tracking-[0.14em] text-muted-foreground">
                    {fact.label}
                  </p>
                  <p className="mt-1 text-sm font-semibold text-foreground">{fact.valueText}</p>
                </div>
              ))}
            </div>
          ) : null}

          {nextSteps.length > 0 ? (
            <section className="space-y-2">
              <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                Next steps
              </p>
              <div className="flex flex-wrap gap-2">
                {nextSteps.map((step) => (
                  <Button
                    className="rounded-full"
                    key={step}
                    onClick={onOpenDrawer}
                    size="sm"
                    type="button"
                    variant="outline"
                  >
                    {step}
                  </Button>
                ))}
              </div>
            </section>
          ) : null}
        </CardContent>
      </Card>

      <section className="rounded-[1.75rem] border border-border/70 bg-background/80 px-5 py-4">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
          Quick actions
        </p>
        <div className="mt-3 flex flex-wrap gap-2">
          <Button onClick={onQuickExplain} size="sm" type="button" variant="outline">
            <Sparkles className="mr-2 size-4" />
            Explain
          </Button>
          <Button onClick={onQuickCompare} size="sm" type="button" variant="outline">
            <Sparkles className="mr-2 size-4" />
            Compare
          </Button>
          <Button onClick={onQuickRecommend} size="sm" type="button" variant="outline">
            <Sparkles className="mr-2 size-4" />
            Recommend
          </Button>
          <Button onClick={onPlanFft} size="sm" type="button" variant="outline">
            <WandSparkles className="mr-2 size-4" />
            FFT
          </Button>
          <Button onClick={onPlanHighPassCompare} size="sm" type="button" variant="outline">
            <WandSparkles className="mr-2 size-4" />
            High-pass
          </Button>
        </div>
        {isPlanning ? <p className="mt-3 text-xs text-muted-foreground">Planning action…</p> : null}
      </section>
    </div>
  )
}
