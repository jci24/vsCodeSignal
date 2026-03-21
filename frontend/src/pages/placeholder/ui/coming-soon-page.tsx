import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card'

interface ComingSoonPageProps {
  title: string
}

export function ComingSoonPage({ title }: ComingSoonPageProps) {
  return (
    <section className="flex flex-1 items-center justify-center">
      <Card className="w-full max-w-3xl rounded-[2rem] border border-border/70 bg-card shadow-[0_18px_44px_-34px_rgba(15,23,42,0.12)]">
        <CardHeader className="gap-3">
          <p className="text-sm font-medium uppercase tracking-[0.28em] text-muted-foreground">
            Section placeholder
          </p>
          <CardTitle className="text-3xl md:text-4xl">Coming soon</CardTitle>
          <CardDescription className="max-w-2xl text-base leading-7">
            This area is reserved for the {title.toLowerCase()} experience. The
            shell is ready, and the first real feature slice will replace this
            placeholder next.
          </CardDescription>
        </CardHeader>
        <CardContent className="border-t border-border/60 pt-6">
          <dl className="grid gap-5 md:grid-cols-3">
            <div>
              <dt className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Status
              </dt>
              <dd className="mt-2 text-base font-medium text-foreground">
                Stub ready
              </dd>
            </div>
            <div>
              <dt className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Next
              </dt>
              <dd className="mt-2 text-base font-medium text-foreground">
                First {title.toLowerCase()} workflow
              </dd>
            </div>
            <div>
              <dt className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Scope
              </dt>
              <dd className="mt-2 text-base font-medium text-foreground">
                Page layout and feature slice entry points
              </dd>
            </div>
          </dl>
        </CardContent>
      </Card>
    </section>
  )
}
