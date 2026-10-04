import { describe, expect, it } from 'vitest'
import { resolveCardLayout } from './card-layout'

describe('resolveCardLayout', () => {
  it('makes the first data column the title when no column claims it', () => {
    const layout = resolveCardLayout([{ id: 'name' }, { id: 'email' }, { id: 'actions' }])

    expect(layout.title).toBe('name')
    expect(layout.fields).toEqual(['email'])
    expect(layout.actions).toBe('actions')
  })

  it('honours an explicit title wherever the column sits', () => {
    const layout = resolveCardLayout([{ id: 'type' }, { id: 'title', placement: 'title' }, { id: 'date' }])

    expect(layout.title).toBe('title')
    expect(layout.fields).toEqual(['type', 'date'])
  })

  it('keeps only the first of several explicit titles', () => {
    const layout = resolveCardLayout([
      { id: 'a', placement: 'title' },
      { id: 'b', placement: 'title' },
    ])

    expect(layout.title).toBe('a')
    expect(layout.fields).toEqual([])
  })

  it('sorts marked columns into their slots and drops hidden ones', () => {
    const layout = resolveCardLayout([
      { id: 'select' },
      { id: 'username' },
      { id: 'email', placement: 'subtitle' },
      { id: 'isActive', placement: 'badge' },
      { id: 'notes', placement: 'wide' },
      { id: 'internal', placement: 'hidden' },
      { id: 'roles' },
      { id: 'actions' },
    ])

    expect(layout).toEqual({
      select: 'select',
      title: 'username',
      subtitles: ['email'],
      badges: ['isActive'],
      fields: ['roles'],
      wide: ['notes'],
      actions: 'actions',
    })
  })

  it('does not promote a badge, subtitle or wide column to the title', () => {
    const layout = resolveCardLayout([
      { id: 'status', placement: 'badge' },
      { id: 'body', placement: 'wide' },
    ])

    expect(layout.title).toBeUndefined()
  })
})
