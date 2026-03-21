import { render, screen } from '@testing-library/react'
import { RouterProvider, createMemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'

import { appRoutes } from '@/app/router'

describe('WorkspaceShell', () => {
  it('renders the sidebar and page placeholder for the current route', () => {
    const router = createMemoryRouter(appRoutes, {
      initialEntries: ['/signals'],
    })

    render(<RouterProvider router={router} />)

    expect(screen.getByRole('navigation', { name: /Primary navigation/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Workspace/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Signals/i })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(
      screen.getByRole('heading', { name: /^Signals$/i }),
    ).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /Coming soon/i })).toBeInTheDocument()
    expect(screen.getByText(/reserved for the signals experience/i)).toBeInTheDocument()
  })
})
