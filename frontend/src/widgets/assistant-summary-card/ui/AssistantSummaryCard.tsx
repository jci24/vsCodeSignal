import type { IAiSummaryCard } from '@/entities/assistant/model/types'
import { Card, CardContent, CardHeader, CardTitle } from '@/shared/ui/card'
import { LoadingSpinner } from '@/shared/ui/loading-spinner'

interface AssistantSummaryCardProps {
  isLoading?: boolean
  summaryCard: IAiSummaryCard | null
}

export function AssistantSummaryCard({
  isLoading = false,
  summaryCard,
}: AssistantSummaryCardProps) {
  return (
    <Card className="rounded-[1.75rem] border border-border/70 bg-background/80">
      <CardHeader className="space-y-2">
        <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
          Auto summary
        </p>
        <CardTitle>{summaryCard?.title ?? 'Signal summary'}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {isLoading ? (
          <LoadingSpinner label="Refreshing summary" />
        ) : (
          <p className="text-sm leading-6 text-foreground">
            {summaryCard?.summary ?? 'Select a signal to generate a grounded summary.'}
          </p>
        )}

        {summaryCard?.keyFacts.length ? (
          <section className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Key facts
            </p>
            <div className="grid gap-2">
              {summaryCard.keyFacts.map((fact) => (
                <div
                  className="rounded-2xl border border-border/60 bg-secondary/40 px-3 py-2"
                  key={fact.code}
                >
                  <p className="text-xs text-muted-foreground">{fact.label}</p>
                  <strong className="text-sm text-foreground">{fact.valueText}</strong>
                </div>
              ))}
            </div>
          </section>
        ) : null}

        {summaryCard?.topObservations.length ? (
          <section className="space-y-2">
            <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
              Observations
            </p>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {summaryCard.topObservations.map((item) => (
                <li className="rounded-2xl border border-border/60 bg-secondary/30 px-3 py-2" key={item.code}>
                  {item.message}
                </li>
              ))}
            </ul>
          </section>
        ) : null}
      </CardContent>
    </Card>
  )
}
