# EasyForNet

### Build your app with AI agents on a solid, maintainable architecture.

A full-stack **ASP.NET 10 + Next.js 16** template and a one-command CLI that gives your coding agents
the structure, rules and guardrails to build production-grade software — not just code that runs.

<a href="https://easyfornet.com" target="_blank" rel="noopener noreferrer"><strong>Web</strong></a> · <a href="https://easyfornet.com/docs" target="_blank" rel="noopener noreferrer"><strong>Documentation</strong></a> · <a href="https://easyfornet.com/docs/getting-started/quick-start" target="_blank" rel="noopener noreferrer"><strong>Quick Start</strong></a> · <a href="https://www.nuget.org/packages/EasyForNetTool" target="_blank" rel="noopener noreferrer"><strong>NuGet</strong></a> · <a href="https://github.com/ma496/EasyForNet" target="_blank" rel="noopener noreferrer"><strong>GitHub</strong></a>

---

## The Problem

Coding agents can ship an app in an afternoon — and leave you a mess you'll be untangling for months:

- **No architecture** – every feature shaped differently, logic leaking across modules.
- **Not optimized** – queries that load whole tables, N+1 calls, nothing that scales past one server.
- **Not secure** – auth bolted on, tenants that can read each other's data, secrets in the wrong place.
- **Not consistent** – each prompt invents its own conventions, and the codebase drifts with every change.

## The Solution

EasyForNet starts your project on a foundation built for agents to extend — and **checks** their work
instead of trusting it.

- 🏗️ **Architected** – vertical feature slices, one file per endpoint, and slice isolation enforced by architecture tests.
- ⚡ **Optimized** – lean projected queries, compile-time mapping, Redis sessions, background jobs and a scale-out-ready real-time hub.
- 🔒 **Secured** – server-side sessions revoked the moment access changes, tenant isolation from the session, hierarchical permissions, guard hooks that block destructive commands.
- 🎯 **Consistent** – a `CLAUDE.md`, 21 skills and 7 specialist agents teach every session the same conventions; reviewers sign off before anything is committed.
- 🧠 **Learns as it builds** – when a run hits a trap, it records the lesson in `.claude/memory/lessons/`, and every later task whose work it touches gets that lesson in its brief, so the project gets smarter with every task instead of rediscovering the same pitfalls.

## Quick Start

You need the **.NET 10 SDK**, **Node.js 24+**, **Git** and **Docker**.

```sh
# 1. Install the CLI
dotnet tool install --global EasyForNetTool

# 2. Generate a project
dotnet efn cp -n my-shop
cd my-shop

# 3. Restore, build and add the initial migration
dotnet tool restore
dotnet build MyShop.slnx
dotnet ef migrations add Initial --project src/backend/Source

# 4. Run everything: PostgreSQL, Redis, the API and the web app
npm run dev
```

Open **http://app.localhost:3000** and sign in as `admin` (the platform), or
**http://default.localhost:3000** as `tenantadmin` (the Default tenant). Both use the password `Admin#123` locally; production seeds them with a random one.

👉 Options, troubleshooting and next steps: <a href="https://easyfornet.com/docs/getting-started/quick-start" target="_blank" rel="noopener noreferrer">easyfornet.com/docs/getting-started/quick-start</a>

## Build With AI Agents

Describe a feature, and a team of agents builds it the way the codebase expects:

```text
/feature add invoices with line items and PDF export
```

The lead agent loads the matching skills, delegates design, data, backend and frontend work to
specialists, runs the quality gate, has QA, security and code reviewers sign off, and commits — then
stops for your review.

For bigger work, write a spec in `specs/` and let the loop build it unattended:

```sh
npm run loop       # plan the spec into tasks, build each one, verify, review, commit
npm run monitor    # watch the run live
```

Every task is verified by the runner itself, capped in cost, committed one task at a time, and never
pushed or merged without your say-so.

## What's Inside

| | |
|---|---|
| **Identity** | Users, roles, hierarchical permissions, sign-up, email verification, password reset |
| **Multi-tenancy** | A subdomain per tenant, isolated data, memberships and seats, platform admin area |
| **Plans** | Editions with feature toggles and limits per tenant |
| **Settings** | Typed run-time settings per platform and tenant, with encrypted secrets |
| **Real-time** | Persistent notifications pushed over SignalR |
| **Files & Email** | Uploads behind a pluggable storage provider; SMTP email through background jobs |
| **Localization** | Eight languages, right-to-left support, translations editable in the app |
| **Testing** | xUnit v3, architecture tests, Vitest and one quality gate: `npm run gate` |
| **Deployment** | Docker Compose with automatic HTTPS, or one command to a VPS: `npm run deploy:vps` |

**Stack:** ASP.NET 10 · FastEndpoints · EF Core 10 · PostgreSQL · Redis · Hangfire · SignalR · Next.js 16 · React 19 · RTK Query · Tailwind CSS 4

## Learn More

Everything else — architecture, backend and frontend guides, configuration, deployment and the AI
workflow — is at <strong><a href="https://easyfornet.com/docs" target="_blank" rel="noopener noreferrer">easyfornet.com</a></strong>.

## Custom Development

If you want custom development, you can send me an email at [support@easyfornet.com](mailto:support@easyfornet.com).

## Support the Project

If EasyForNet saves you time, ⭐ <a href="https://github.com/ma496/EasyForNet" target="_blank" rel="noopener noreferrer">star it on GitHub</a> — it helps others find it and keeps the project growing.
