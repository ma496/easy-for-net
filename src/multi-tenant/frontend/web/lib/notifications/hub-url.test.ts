import { describe, expect, it } from 'vitest'
import { notificationHubUrl } from './hub-url'

describe('notificationHubUrl', () => {
  it('maps the hub at the root of the API host, outside the route prefix', () => {
    expect(notificationHubUrl('http://localhost:5000/api', 'http://localhost:3000'))
      .toBe('http://localhost:5000/hubs/notifications')
  })

  it('ignores a trailing slash on the API URL', () => {
    expect(notificationHubUrl('https://api.example.com/api/', 'https://app.example.com'))
      .toBe('https://api.example.com/hubs/notifications')
  })

  it('resolves a relative API URL against the page origin', () => {
    expect(notificationHubUrl('/api', 'https://app.example.com'))
      .toBe('https://app.example.com/hubs/notifications')
  })

  it('falls back to the page origin when no API URL is configured', () => {
    expect(notificationHubUrl(undefined, 'https://app.example.com'))
      .toBe('https://app.example.com/hubs/notifications')
  })
})
