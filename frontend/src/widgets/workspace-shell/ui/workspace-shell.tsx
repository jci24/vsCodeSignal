import {
  ActivitySquare,
  ArrowLeftRight,
  ChevronLeft,
  ChevronRight,
  PanelLeft,
  Settings2,
} from 'lucide-react'
import { useState } from 'react'
import { NavLink, Outlet, useMatches } from 'react-router-dom'

import { Import } from '@/features/import/Import'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/button'
import { SignalStudioLogo } from '@/shared/ui/signal-studio-logo/SignalStudioLogo'

const primaryNavigationItems = [
  {
    icon: ArrowLeftRight,
    label: 'Compare',
    to: '/',
  },
]

const advancedNavigationItems = [
  {
    icon: PanelLeft,
    label: 'Inspect',
    to: '/inspect',
  },
]

const utilityNavigationItems = [
  {
    icon: Settings2,
    label: 'Settings',
    to: '/settings',
  },
] as const

interface RouteHandle {
  description?: string
  title?: string
}

function NavigationLink({
  isCollapsed,
  item,
}: {
  isCollapsed: boolean
  item: (typeof primaryNavigationItems)[number]
}) {
  const Icon = item.icon

  return (
    <NavLink
      className={({ isActive }) =>
        cn(
          'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors',
          isActive
            ? 'border border-border/70 bg-secondary text-foreground'
            : 'text-muted-foreground hover:bg-secondary/70 hover:text-foreground',
          isCollapsed ? 'md:justify-center' : 'justify-start',
        )
      }
      title={item.label}
      to={item.to}
    >
      <Icon className="size-4 shrink-0" />
      <span className={cn(isCollapsed ? 'md:hidden' : 'inline')}>{item.label}</span>
    </NavLink>
  )
}

export function WorkspaceShell() {
  const [isCollapsed, setIsCollapsed] = useState(false)
  const matches = useMatches()
  const activeHandle = matches.at(-1)?.handle as RouteHandle | undefined
  const activeTitle = activeHandle?.title ?? 'Compare'
  const activeDescription = activeHandle?.description
  const isAdvancedRoute = activeTitle === 'Inspect'

  return (
    <div className="min-h-svh overflow-hidden bg-background p-3">
      <div className="flex h-[calc(100svh-1.5rem)] flex-col md:flex-row">
        <aside
          className={cn(
            'flex shrink-0 flex-col overflow-hidden border-b border-border/60 bg-background transition-[width] duration-200 md:h-full md:border-r md:border-b-0',
            isCollapsed ? 'md:w-24' : 'md:w-80',
          )}
        >
          <div
            className={cn(
              'border-b border-border/60 md:min-h-[108px]',
              isCollapsed
                ? 'flex flex-col items-center justify-center gap-3 px-3 py-3'
                : 'flex items-center justify-between gap-3 px-4 py-3.5',
            )}
          >
            <div
              className={cn(
                'min-w-0',
                isCollapsed ? 'flex flex-col items-center text-center' : 'block',
              )}
            >
              {isCollapsed ? (
                <SignalStudioLogo compact />
              ) : (
                <SignalStudioLogo />
              )}
            </div>
            <Button
              aria-label={isCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
              className="shrink-0"
              onClick={() => setIsCollapsed((value) => !value)}
              size="icon"
              type="button"
              variant="outline"
            >
              {isCollapsed ? (
                <ChevronRight className="size-4" />
              ) : (
                <ChevronLeft className="size-4" />
              )}
            </Button>
          </div>

          <nav aria-label="Primary navigation" className="min-h-0 flex-1 overflow-y-auto px-3 py-3">
            <p
              className={cn(
                'px-2 pb-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground',
                isCollapsed ? 'md:hidden' : 'block',
              )}
            >
              Main flow
            </p>
            <ul className="space-y-1.5">
              {primaryNavigationItems.map((item) => (
                <li key={item.label}>
                  <NavigationLink isCollapsed={isCollapsed} item={item} />
                </li>
              ))}
            </ul>

            <div
              className={cn(
                'mt-5 rounded-2xl border border-border/70 bg-secondary/25 p-3',
                isCollapsed ? 'md:px-2 md:py-3' : '',
              )}
            >
              <div
                className={cn(
                  'flex items-start gap-3',
                  isCollapsed ? 'md:flex-col md:items-center md:text-center' : '',
                )}
              >
                <div className="rounded-full border border-border/70 bg-background p-2">
                  <PanelLeft className="size-4" />
                </div>
                <div className={cn('space-y-1', isCollapsed ? 'md:hidden' : 'block')}>
                  <p className="text-xs font-semibold uppercase tracking-[0.16em] text-muted-foreground">
                    Advanced mode
                  </p>
                  <p className="text-sm leading-5 text-foreground">
                    Inspect is still available when you need deeper charts, transforms, and technical drill-down.
                  </p>
                </div>
              </div>
              <div className={cn('mt-3', isCollapsed ? 'md:mt-2' : '')}>
                {advancedNavigationItems.map((item) => (
                  <NavigationLink isCollapsed={isCollapsed} item={item} key={item.label} />
                ))}
              </div>
            </div>
          </nav>

          <div className="border-t border-border/60 px-4 py-3">
            <div className="space-y-3">
              {utilityNavigationItems.map((item) => (
                <NavigationLink isCollapsed={isCollapsed} item={item} key={item.label} />
              ))}
            </div>
            <div className="mt-3 flex items-center gap-2 text-sm font-medium text-foreground">
              <ActivitySquare className="size-4 text-foreground" />
              <span className={cn(isCollapsed ? 'md:hidden' : 'inline')}>
                {isAdvancedRoute ? 'Advanced inspection active' : 'Guided compare ready'}
              </span>
            </div>
          </div>
        </aside>

        <main className="flex min-h-0 min-w-0 flex-1 overflow-hidden">
          <div className="flex min-h-full w-full flex-1 flex-col overflow-hidden px-5 pt-3 pb-4 md:px-7 md:pt-0 md:pb-4">
            <header className="shrink-0 border-b border-border/60 md:min-h-[76px]">
              <div className="flex flex-col gap-3 pb-3 md:h-full md:flex-row md:items-center md:justify-between md:pb-0">
                <div>
                  <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-muted-foreground">
                    {isAdvancedRoute ? 'Advanced mode' : 'Guided product'}
                  </p>
                  <h2 className="text-2xl font-semibold tracking-tight text-foreground md:text-[2rem]">
                    {activeTitle}
                  </h2>
                  {activeDescription ? (
                    <p className="mt-1 max-w-3xl text-sm leading-6 text-muted-foreground">
                      {activeDescription}
                    </p>
                  ) : null}
                </div>
                <Import />
              </div>
            </header>
            <div className="flex min-h-0 flex-1 overflow-hidden py-3">
              <Outlet />
            </div>
          </div>
        </main>
      </div>
    </div>
  )
}
