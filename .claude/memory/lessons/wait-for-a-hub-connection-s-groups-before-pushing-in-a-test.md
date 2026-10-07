---
scope: signalr hub
learned: 2026-09-30
---

# Wait for a hub connection's groups before pushing in a test

SignalR answers the handshake before the hub's OnConnectedAsync joins its groups, so HubConnection.StartAsync returns before the connection can receive a group send; a test that pushes right away times out or passes a negative check vacuously. Use HubProbe.ConnectAsync, which resends a sentinel to the u: group (joined last) until one arrives. The TestServer WebSocket client also bypasses WebSocketOptions.AllowedOrigins, so test the origin check on WebSocketMiddleware directly.
