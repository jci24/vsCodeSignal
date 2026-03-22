interface ComingSoonPageProps {
  title: string
}

export function ComingSoonPage({ title }: ComingSoonPageProps) {
  return (
    <section className="flex flex-1 items-start justify-center">
      <div className="grid w-full max-w-5xl gap-8">
        <div className="grid gap-3">
          <p className="text-sm font-medium uppercase tracking-[0.28em] text-muted-foreground">
            Section placeholder
          </p>
          <h3 className="text-3xl font-semibold tracking-tight text-foreground md:text-5xl">
            Coming soon
          </h3>
          <p className="max-w-3xl text-base leading-7 text-muted-foreground">
            This area is reserved for the {title.toLowerCase()} experience. The
            shell is ready, and the first real feature slice will replace this
            placeholder next.
          </p>
        </div>

        <div className="rounded-[2rem] border border-border/70 bg-background/72 p-6 md:p-8">
          <div className="grid gap-6">
            <div className="grid gap-3">
              <p className="text-sm font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Ready next
              </p>
              <p className="max-w-2xl text-2xl font-semibold tracking-tight text-foreground md:text-3xl">
                First {title.toLowerCase()} workflow
              </p>
              <p className="max-w-3xl text-sm leading-7 text-muted-foreground">
                The page shell is in place. The next slice replaces this placeholder
                with a real working surface instead of a demo scaffold.
              </p>
            </div>

            <dl className="grid gap-4 border-t border-border/60 pt-6 md:grid-cols-3">
              <div className="rounded-[1.4rem] border border-border/60 bg-background px-5 py-4">
                <dt className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Status
                </dt>
                <dd className="mt-2 text-base font-medium text-foreground">
                  Stub ready
                </dd>
              </div>
              <div className="rounded-[1.4rem] border border-border/60 bg-background px-5 py-4">
                <dt className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Next
                </dt>
                <dd className="mt-2 text-base font-medium text-foreground">
                  Feature slice entry
                </dd>
              </div>
              <div className="rounded-[1.4rem] border border-border/60 bg-background px-5 py-4">
                <dt className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                Scope
                </dt>
                <dd className="mt-2 text-base font-medium text-foreground">
                  Layout and interaction surface
                </dd>
              </div>
            </dl>
          </div>
        </div>
      </div>
    </section>
  )
}
