import { describe, expect, it } from 'vitest'
import { notificationReceived, notificationsSlice, setUnreadCount } from './notificationsSlice'

const reduce = notificationsSlice.reducer

describe('notificationsSlice', () => {
  it('starts with nothing unread', () => {
    expect(notificationsSlice.getInitialState()).toEqual({ unreadCount: 0 })
  })

  it('counts one more unread for every notification the hub pushes', () => {
    let state = reduce(undefined, notificationReceived())
    expect(state.unreadCount).toBe(1)
    state = reduce(state, notificationReceived())
    expect(state.unreadCount).toBe(2)
  })

  it('replaces the count with the one unreadCountChanged carries', () => {
    const state = reduce({ unreadCount: 7 }, setUnreadCount(2))
    expect(state.unreadCount).toBe(2)
  })

  it('counts on from an authoritative count', () => {
    let state = reduce(undefined, setUnreadCount(99))
    state = reduce(state, notificationReceived())
    expect(state.unreadCount).toBe(100)
  })

  it('drops to zero when everything is read', () => {
    expect(reduce({ unreadCount: 42 }, setUnreadCount(0)).unreadCount).toBe(0)
  })
})
