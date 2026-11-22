'use client'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import ReactApexChart from 'react-apexcharts'
import { ApexOptions } from 'apexcharts'
import { useAppSelector } from '@/store/hooks'
import { Activity, Eye, LogOut, TrendingDown, TrendingUp, UserPlus, Users } from 'lucide-react'
import { useTranslation, humanizeKey } from '@/i18n'
import { Badge } from '@/components/ui'
import { cn } from '@/lib/utils'

/** The design tokens the charts are painted with, read from the document so they follow the theme. */
const TOKEN_NAMES = ['primary', 'secondary', 'success', 'warning', 'danger', 'info', 'border', 'muted-foreground', 'foreground', 'surface', 'surface-3'] as const
type TokenName = (typeof TOKEN_NAMES)[number]
type ChartTokens = Record<TokenName, string>

/** Resolves every chart token to the value the active theme gives it on `<html>`. */
const readTokens = (): ChartTokens => {
  const style = getComputedStyle(document.documentElement)
  return Object.fromEntries(TOKEN_NAMES.map((name) => [name, style.getPropertyValue(`--${name}`).trim()])) as ChartTokens
}

/** The traffic chart's selectable periods. */
const PERIODS = ['weekly', 'monthly', 'yearly'] as const
type Period = (typeof PERIODS)[number]

/** Demo traffic figures for each period - the dashboard is a showcase, not a report. */
const TRAFFIC: Record<Period, { visitors: number[]; pageViews: number[] }> = {
  weekly: {
    visitors: [1820, 2140, 2010, 2380, 2650, 1940, 1710],
    pageViews: [5240, 6180, 5890, 6920, 7480, 5310, 4870],
  },
  monthly: {
    visitors: [41200, 43800, 47500, 45100, 49800, 52300, 50600, 54900, 58200, 56700, 61400, 64800],
    pageViews: [118000, 124500, 136200, 129800, 142600, 151300, 147200, 158900, 167400, 163100, 176800, 186500],
  },
  yearly: {
    visitors: [214000, 312000, 405000, 538000, 626000],
    pageViews: [602000, 884000, 1162000, 1541000, 1802000],
  },
}

/**
 * Demo KPI figures: the value, how it is formatted, its change against the previous period, and whether
 * a rise is good news (a falling bounce rate is the improvement).
 */
const KPIS = [
  { key: 'visitors', value: 64800, format: 'number', change: 0.055, higherIsBetter: true, icon: Users },
  { key: 'pageViews', value: 186500, format: 'number', change: 0.054, higherIsBetter: true, icon: Eye },
  { key: 'signUps', value: 1284, format: 'number', change: 0.127, higherIsBetter: true, icon: UserPlus },
  { key: 'bounceRate', value: 0.384, format: 'percent', change: -0.021, higherIsBetter: false, icon: LogOut },
] as const

/** Demo traffic-source split: each source's key and its share of visits. */
const SOURCES = [
  { key: 'organicSearch', value: 28400 },
  { key: 'direct', value: 17900 },
  { key: 'social', value: 11200 },
  { key: 'referral', value: 7300 },
] as const

/** Demo most-visited pages: the path and its views. Paths are URLs, so they are not translated. */
const TOP_PAGES = [
  { path: '/', views: 48200 },
  { path: '/pricing', views: 26900 },
  { path: '/blog', views: 19400 },
  { path: '/features', views: 14100 },
  { path: '/contact', views: 6800 },
] as const

/** Props for a dashboard card: its heading, optional end-aligned controls and the body. */
interface ChartCardProps {
  title: ReactNode
  subtitle?: ReactNode
  actions?: ReactNode
  className?: string
  children: ReactNode
}

/** A titled card that holds one chart or summary block on the dashboard. */
const ChartCard = ({ title, subtitle, actions, className, children }: ChartCardProps) => (
  <section className={cn('flex min-w-0 flex-col rounded-xl border border-border bg-surface p-5 shadow-xs', className)}>
    <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        <h2 className="text-base font-semibold text-foreground">{title}</h2>
        {subtitle && <div className="mt-0.5 text-sm text-muted-foreground">{subtitle}</div>}
      </div>
      {actions}
    </div>
    {children}
  </section>
)

/** The placeholder a chart shows until the browser has mounted it and resolved the theme tokens. */
const ChartSkeleton = ({ height }: { height: number }) => <div className="animate-pulse rounded-lg bg-surface-2" style={{ height }} />

/**
 * Interactive client-side website analytics dashboard with KPI cards and traffic, traffic source, sign-up,
 * top page and active user charts. Every chart colour is read from the theme's CSS tokens at render time
 * and read again whenever the colour scheme changes, so the charts follow light and dark mode like the
 * rest of the page. The figures are demo data.
 */
export const AnalyticsDashboard = () => {
  const isDark = useAppSelector((state) => state.theme.isDarkMode)
  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const { t, i18n } = useTranslation()
  const locale = i18n.language

  const [tokens, setTokens] = useState<ChartTokens | null>(null)
  const [period, setPeriod] = useState<Period>('monthly')

  // Read the tokens once mounted and again whenever the theme flips. The `dark` class on <html> is
  // toggled by the app shell in its own effect, which can run after this one, so the class change is
  // observed as well rather than trusting the store's flag alone.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setTokens(readTokens())
    const observer = new MutationObserver(() => setTokens(readTokens()))
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] })
    return () => observer.disconnect()
  }, [isDark])

  const formats = useMemo(
    () => ({
      number: new Intl.NumberFormat(locale),
      compact: new Intl.NumberFormat(locale, { notation: 'compact', maximumFractionDigits: 1 }),
      percent: new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 1 }),
      change: new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 1, signDisplay: 'exceptZero' }),
      month: new Intl.DateTimeFormat(locale, { month: 'short' }),
      weekday: new Intl.DateTimeFormat(locale, { weekday: 'short' }),
    }),
    [locale],
  )

  // 7 January 2024 is a Sunday, so these run Sunday to Saturday.
  const weekdays = Array.from({ length: 7 }, (_, day) => formats.weekday.format(new Date(2024, 0, 7 + day)))
  const months = Array.from({ length: 12 }, (_, month) => formats.month.format(new Date(2024, month, 1)))
  const periodLabels: Record<Period, string[]> = { weekly: weekdays, monthly: months, yearly: ['2021', '2022', '2023', '2024', '2025'] }

  // Remount the charts when the palette changes, so no series keeps a colour from the old theme.
  const chartKey = tokens ? `${tokens.primary}-${tokens.surface}` : 'pending'

  /** What every chart shares: inherited font, transparent background, theme-coloured axes, grid and tooltip. */
  const base = (tk: ChartTokens): ApexOptions => ({
    chart: { fontFamily: 'inherit', background: 'transparent', foreColor: tk['muted-foreground'], toolbar: { show: false }, zoom: { enabled: false } },
    theme: { mode: isDark ? 'dark' : 'light' },
    grid: { borderColor: tk.border, strokeDashArray: 4 },
    tooltip: { theme: isDark ? 'dark' : 'light' },
    dataLabels: { enabled: false },
    legend: { labels: { colors: tk['muted-foreground'] }, fontSize: '13px' },
  })

  const traffic = TRAFFIC[period]
  const trafficOptions = (tk: ChartTokens): ApexOptions => {
    const shared = base(tk)
    return {
      ...shared,
      chart: { ...shared.chart, type: 'area' },
      stroke: { show: true, curve: 'smooth', width: 2 },
      colors: [tk.primary, tk.info],
      labels: periodLabels[period],
      xaxis: {
        axisBorder: { show: false },
        axisTicks: { show: false },
        crosshairs: { show: true },
        labels: { offsetY: 2, style: { fontSize: '12px', colors: tk['muted-foreground'] } },
      },
      yaxis: {
        tickAmount: 5,
        labels: {
          formatter: (value: number) => formats.compact.format(value),
          style: { fontSize: '12px', colors: tk['muted-foreground'] },
        },
        opposite: isRtl,
      },
      grid: { ...shared.grid, xaxis: { lines: { show: false } }, yaxis: { lines: { show: true } }, padding: { top: 0, right: 8, bottom: 0, left: 8 } },
      legend: { ...shared.legend, position: 'top', horizontalAlign: isRtl ? 'left' : 'right', markers: { size: 5, strokeWidth: 0 }, itemMargin: { horizontal: 8, vertical: 4 } },
      tooltip: { ...shared.tooltip, x: { show: true }, y: { formatter: (value: number) => formats.number.format(value) } },
      fill: { type: 'gradient', gradient: { shadeIntensity: 1, inverseColors: false, opacityFrom: isDark ? 0.25 : 0.2, opacityTo: 0, stops: [0, 95] } },
    }
  }

  const sourcesOptions = (tk: ChartTokens): ApexOptions => {
    const shared = base(tk)
    return {
      ...shared,
      chart: { ...shared.chart, type: 'donut' },
      stroke: { show: true, width: 3, colors: [tk.surface] },
      colors: [tk.primary, tk.info, tk.warning, tk.success],
      legend: { ...shared.legend, position: 'bottom', horizontalAlign: 'center', markers: { size: 5, strokeWidth: 0 }, itemMargin: { horizontal: 8, vertical: 4 } },
      plotOptions: {
        pie: {
          donut: {
            size: '72%',
            background: 'transparent',
            labels: {
              show: true,
              name: { show: true, fontSize: '13px', color: tk['muted-foreground'], offsetY: 20 },
              value: { show: true, fontSize: '26px', fontWeight: 600, color: tk.foreground, offsetY: -12, formatter: (value: string) => formats.compact.format(Number(value)) },
              total: {
                show: true,
                label: t('page.dashboard.visitors') || 'Visitors',
                fontSize: '13px',
                color: tk['muted-foreground'],
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                formatter: (w: any) => formats.compact.format(w.globals.seriesTotals.reduce((a: number, b: number) => a + b, 0)),
              },
            },
          },
        },
      },
      labels: SOURCES.map((source) => t(`page.dashboard.${source.key}`) || humanizeKey(source.key)),
      tooltip: { ...shared.tooltip, y: { formatter: (value: number) => formats.number.format(value) } },
      states: {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        hover: { filter: { type: 'none' } as any },
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        active: { filter: { type: 'none' } as any },
      },
    }
  }

  const signUpsOptions = (tk: ChartTokens): ApexOptions => {
    const shared = base(tk)
    return {
      ...shared,
      chart: { ...shared.chart, type: 'bar' },
      colors: [tk.primary, tk['surface-3']],
      plotOptions: { bar: { horizontal: false, columnWidth: '55%', borderRadius: 3, borderRadiusApplication: 'end' } },
      xaxis: {
        categories: weekdays,
        axisBorder: { show: false },
        axisTicks: { show: false },
        labels: { style: { fontSize: '12px', colors: tk['muted-foreground'] } },
      },
      yaxis: { show: false },
      legend: { show: false },
      grid: { ...shared.grid, yaxis: { lines: { show: true } }, padding: { top: -10, right: 0, bottom: 0, left: 0 } },
    }
  }

  const activeOptions = (tk: ChartTokens): ApexOptions => {
    const shared = base(tk)
    return {
      ...shared,
      chart: { ...shared.chart, type: 'area', sparkline: { enabled: true } },
      stroke: { curve: 'smooth', width: 2 },
      colors: [tk.success],
      labels: Array.from({ length: 15 }, (_, index) => String(index + 1)),
      yaxis: { min: 0, show: false },
      fill: { type: 'gradient', gradient: { shadeIntensity: 1, inverseColors: false, opacityFrom: 0.3, opacityTo: 0, stops: [0, 100] } },
      tooltip: { ...shared.tooltip, x: { show: false } },
    }
  }

  const topPageMax = Math.max(...TOP_PAGES.map((page) => page.views))

  return (
    <div className="flex flex-col gap-6">
      {/* KPI row */}
      <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-4">
        {KPIS.map((kpi) => {
          const Icon = kpi.icon
          const Trend = kpi.change >= 0 ? TrendingUp : TrendingDown
          const good = kpi.change >= 0 === kpi.higherIsBetter
          return (
            <div key={kpi.key} className="rounded-xl border border-border bg-surface p-5 shadow-xs">
              <div className="flex items-center justify-between gap-3">
                <span className="text-sm font-medium text-muted-foreground">{t(`page.dashboard.${kpi.key}`) || humanizeKey(kpi.key)}</span>
                <span className="grid size-8 shrink-0 place-content-center rounded-lg bg-surface-2 text-muted-foreground">
                  <Icon className="size-4" />
                </span>
              </div>
              <div className="mt-3 flex flex-wrap items-end justify-between gap-2">
                <span className="text-2xl font-semibold tracking-tight text-foreground tabular-nums">{kpi.format === 'percent' ? formats.percent.format(kpi.value) : formats.number.format(kpi.value)}</span>
                <Badge variant={good ? 'success' : 'danger'} className="tabular-nums">
                  <Trend className="size-3" />
                  {formats.change.format(kpi.change)}
                </Badge>
              </div>
            </div>
          )
        })}
      </div>

      {/* Traffic and its sources */}
      <div className="grid gap-6 xl:grid-cols-3">
        <ChartCard
          className="xl:col-span-2"
          title={t('page.dashboard.traffic') || 'Traffic'}
          subtitle={
            <span>
              {t('page.dashboard.visitors') || 'Visitors'} <span className="ms-1 font-semibold text-foreground tabular-nums">{formats.number.format(traffic.visitors.reduce((a, b) => a + b, 0))}</span>
            </span>
          }
          actions={
            <div className="inline-flex rounded-lg bg-surface-2 p-0.5" role="group" aria-label={t('page.dashboard.traffic') || 'Traffic'}>
              {PERIODS.map((candidate) => (
                <button
                  key={candidate}
                  type="button"
                  aria-pressed={period === candidate}
                  onClick={() => setPeriod(candidate)}
                  className={cn(
                    'cursor-pointer rounded-md px-3 py-1 text-xs font-medium transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-none',
                    period === candidate ? 'bg-surface text-foreground shadow-xs' : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  {t(`page.dashboard.${candidate}`) || humanizeKey(candidate)}
                </button>
              ))}
            </div>
          }
        >
          <div className="-mx-2">
            {tokens ? (
              <ReactApexChart
                key={`traffic-${chartKey}`}
                series={[
                  { name: t('page.dashboard.visitors') || 'Visitors', data: traffic.visitors },
                  { name: t('page.dashboard.pageViews') || 'Page Views', data: traffic.pageViews },
                ]}
                options={trafficOptions(tokens)}
                type="area"
                height={320}
                width="100%"
              />
            ) : (
              <ChartSkeleton height={320} />
            )}
          </div>
        </ChartCard>

        <ChartCard title={t('page.dashboard.trafficSources') || 'Traffic Sources'}>
          <div className="flex flex-1 items-center">
            <div className="w-full">
              {tokens ? (
                <ReactApexChart key={`sources-${chartKey}`} series={SOURCES.map((source) => source.value)} options={sourcesOptions(tokens)} type="donut" height={320} width="100%" />
              ) : (
                <ChartSkeleton height={320} />
              )}
            </div>
          </div>
        </ChartCard>
      </div>

      {/* Sign-ups, top pages and active users */}
      <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        <ChartCard
          title={t('page.dashboard.newSignUps') || 'New Sign-ups'}
          subtitle={
            <span className="inline-flex flex-wrap items-center gap-x-3 gap-y-1">
              <span className="inline-flex items-center gap-1.5">
                <span className="size-2 rounded-full bg-primary" />
                {t('page.dashboard.thisWeek') || 'This Week'}
              </span>
              <span className="inline-flex items-center gap-1.5">
                <span className="size-2 rounded-full bg-surface-3" />
                {t('page.dashboard.lastWeek') || 'Last Week'}
              </span>
            </span>
          }
        >
          {tokens ? (
            <ReactApexChart
              key={`sign-ups-${chartKey}`}
              series={[
                { name: t('page.dashboard.thisWeek') || 'This Week', data: [38, 52, 47, 61, 58, 34, 29] },
                { name: t('page.dashboard.lastWeek') || 'Last Week', data: [31, 44, 49, 50, 46, 28, 25] },
              ]}
              options={signUpsOptions(tokens)}
              type="bar"
              height={200}
              width="100%"
            />
          ) : (
            <ChartSkeleton height={200} />
          )}
        </ChartCard>

        <ChartCard title={t('page.dashboard.topPages') || 'Top Pages'}>
          <ul className="flex flex-1 flex-col justify-center gap-4">
            {TOP_PAGES.map((page) => (
              <li key={page.path}>
                <div className="mb-1.5 flex items-baseline justify-between gap-2 text-sm">
                  <span className="truncate font-mono text-foreground" dir="ltr">
                    {page.path}
                  </span>
                  <span className="shrink-0 text-muted-foreground tabular-nums">{formats.number.format(page.views)}</span>
                </div>
                <div className="h-1.5 rounded-full bg-surface-2">
                  <div className="h-full rounded-full bg-primary" style={{ width: `${(page.views / topPageMax) * 100}%` }} />
                </div>
              </li>
            ))}
          </ul>
        </ChartCard>

        <ChartCard
          className="overflow-hidden md:col-span-2 xl:col-span-1"
          title={t('page.dashboard.activeUsers') || 'Active Users'}
          subtitle={t('page.dashboard.lastThirtyMinutes') || 'In the last 30 minutes'}
          actions={
            <span className="grid size-9 place-content-center rounded-lg bg-success/10 text-success">
              <Activity className="size-4" />
            </span>
          }
        >
          <div className="flex items-center gap-2">
            <span className="text-2xl font-semibold tracking-tight text-foreground tabular-nums">{formats.number.format(342)}</span>
            <Badge variant="success" className="tabular-nums">
              <TrendingUp className="size-3" />
              {formats.change.format(0.083)}
            </Badge>
          </div>
          <div className="-mx-5 mt-auto -mb-5 pt-4">
            {tokens ? (
              <ReactApexChart
                key={`active-${chartKey}`}
                series={[{ name: t('page.dashboard.activeUsers') || 'Active Users', data: [264, 281, 275, 298, 312, 290, 305, 327, 318, 301, 322, 336, 329, 351, 342] }]}
                options={activeOptions(tokens)}
                type="area"
                height={180}
                width="100%"
              />
            ) : (
              <ChartSkeleton height={180} />
            )}
          </div>
        </ChartCard>
      </div>
    </div>
  )
}
