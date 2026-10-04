'use client'
import { useEffect, useMemo, useState, type ReactNode } from 'react'
import ReactApexChart from 'react-apexcharts'
import { ApexOptions } from 'apexcharts'
import { useAppSelector } from '@/store/hooks'
import { CreditCard, DollarSign, Inbox, ShoppingCart, Tag, TrendingDown, TrendingUp, Wallet } from 'lucide-react'
import { useTranslation } from '@/i18n'
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

/** The revenue chart's selectable periods. */
const PERIODS = ['weekly', 'monthly', 'yearly'] as const
type Period = (typeof PERIODS)[number]

/** Demo revenue figures for each period - the dashboard is a showcase, not a report. */
const REVENUE: Record<Period, { income: number[]; expenses: number[] }> = {
  weekly: {
    income: [3200, 4100, 3800, 4600, 5200, 4900, 5600],
    expenses: [2400, 2900, 2600, 3100, 3500, 3300, 3600],
  },
  monthly: {
    income: [18500, 19200, 17800, 21500, 18900, 22000, 24500, 23000, 21800, 25000, 23500, 26000],
    expenses: [15800, 16300, 15000, 16500, 15200, 17800, 14500, 15200, 14800, 16500, 15800, 17200],
  },
  yearly: {
    income: [182000, 214000, 236000, 251000, 268000],
    expenses: [148000, 162000, 171000, 183000, 190000],
  },
}

/** Demo KPI figures: the value, its change against the previous period, and whether that change is good news. */
const KPIS = [
  { key: 'totalProfit', value: 18750, currency: true, change: 0.124, good: true, icon: DollarSign },
  { key: 'income', value: 125800, currency: true, change: 0.081, good: true, icon: Wallet },
  { key: 'expenses', value: 73450, currency: true, change: -0.032, good: true, icon: CreditCard },
  { key: 'totalOrders', value: 4875, currency: false, change: 0.056, good: true, icon: ShoppingCart },
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
 * Interactive client-side sales dashboard with KPI cards and revenue, category, daily sales, summary and
 * total orders charts. Every chart colour is read from the theme's CSS tokens at render time and read
 * again whenever the colour scheme changes, so the charts follow light and dark mode like the rest of
 * the page. The figures are demo data.
 */
export const SaleDashboard = () => {
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
      currency: new Intl.NumberFormat(locale, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }),
      number: new Intl.NumberFormat(locale),
      compact: new Intl.NumberFormat(locale, { notation: 'compact', maximumFractionDigits: 1 }),
      percent: new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 1, signDisplay: 'exceptZero' }),
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

  const revenue = REVENUE[period]
  const revenueOptions = (tk: ChartTokens): ApexOptions => {
    const shared = base(tk)
    return {
      ...shared,
      chart: { ...shared.chart, type: 'area' },
      stroke: { show: true, curve: 'smooth', width: 2 },
      colors: [tk.primary, tk.danger],
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
      tooltip: { ...shared.tooltip, x: { show: true }, y: { formatter: (value: number) => formats.currency.format(value) } },
      fill: { type: 'gradient', gradient: { shadeIntensity: 1, inverseColors: false, opacityFrom: isDark ? 0.25 : 0.2, opacityTo: 0, stops: [0, 95] } },
    }
  }

  const categoryOptions = (tk: ChartTokens): ApexOptions => {
    const shared = base(tk)
    return {
      ...shared,
      chart: { ...shared.chart, type: 'donut' },
      stroke: { show: true, width: 3, colors: [tk.surface] },
      colors: [tk.primary, tk.info, tk.warning],
      legend: { ...shared.legend, position: 'bottom', horizontalAlign: 'center', markers: { size: 5, strokeWidth: 0 }, itemMargin: { horizontal: 8, vertical: 4 } },
      plotOptions: {
        pie: {
          donut: {
            size: '72%',
            background: 'transparent',
            labels: {
              show: true,
              name: { show: true, fontSize: '13px', color: tk['muted-foreground'], offsetY: 20 },
              value: { show: true, fontSize: '26px', fontWeight: 600, color: tk.foreground, offsetY: -12, formatter: (value: string) => formats.number.format(Number(value)) },
              total: {
                show: true,
                label: t('page.dashboard.sales'),
                fontSize: '13px',
                color: tk['muted-foreground'],
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                formatter: (w: any) => formats.number.format(w.globals.seriesTotals.reduce((a: number, b: number) => a + b, 0)),
              },
            },
          },
        },
      },
      labels: ['Apparel', 'Sports', 'Others'],
      states: {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        hover: { filter: { type: 'none' } as any },
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        active: { filter: { type: 'none' } as any },
      },
    }
  }

  const dailyOptions = (tk: ChartTokens): ApexOptions => {
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

  const ordersOptions = (tk: ChartTokens): ApexOptions => {
    const shared = base(tk)
    return {
      ...shared,
      chart: { ...shared.chart, type: 'area', sparkline: { enabled: true } },
      stroke: { curve: 'smooth', width: 2 },
      colors: [tk.success],
      labels: ['1', '2', '3', '4', '5', '6', '7', '8', '9', '10'],
      yaxis: { min: 0, show: false },
      fill: { type: 'gradient', gradient: { shadeIntensity: 1, inverseColors: false, opacityFrom: 0.3, opacityTo: 0, stops: [0, 100] } },
      tooltip: { ...shared.tooltip, x: { show: false } },
    }
  }

  const summary = [
    { key: 'income', value: 125800, share: 92, icon: Inbox, tile: 'bg-primary/10 text-primary', bar: 'bg-primary' },
    { key: 'profit', value: 52350, share: 65, icon: Tag, tile: 'bg-success/10 text-success', bar: 'bg-success' },
    { key: 'expenses', value: 73450, share: 80, icon: CreditCard, tile: 'bg-warning/10 text-warning', bar: 'bg-warning' },
  ] as const

  return (
    <div className="flex flex-col gap-6">
      {/* KPI row */}
      <div className="grid gap-6 sm:grid-cols-2 xl:grid-cols-4">
        {KPIS.map((kpi) => {
          const Icon = kpi.icon
          const Trend = kpi.change >= 0 ? TrendingUp : TrendingDown
          return (
            <div key={kpi.key} className="rounded-xl border border-border bg-surface p-5 shadow-xs">
              <div className="flex items-center justify-between gap-3">
                <span className="text-sm font-medium text-muted-foreground">{t(`page.dashboard.${kpi.key}`)}</span>
                <span className="grid size-8 shrink-0 place-content-center rounded-lg bg-surface-2 text-muted-foreground">
                  <Icon className="size-4" />
                </span>
              </div>
              <div className="mt-3 flex flex-wrap items-end justify-between gap-2">
                <span className="text-2xl font-semibold tracking-tight text-foreground tabular-nums">{kpi.currency ? formats.currency.format(kpi.value) : formats.number.format(kpi.value)}</span>
                <Badge variant={kpi.good ? 'success' : 'danger'} className="tabular-nums">
                  <Trend className="size-3" />
                  {formats.percent.format(kpi.change)}
                </Badge>
              </div>
            </div>
          )
        })}
      </div>

      {/* Revenue and category */}
      <div className="grid gap-6 xl:grid-cols-3">
        <ChartCard
          className="xl:col-span-2"
          title={t('page.dashboard.revenue')}
          subtitle={
            <span>
              {t('page.dashboard.totalProfit')} <span className="ms-1 font-semibold text-foreground tabular-nums">{formats.currency.format(18750)}</span>
            </span>
          }
          actions={
            <div className="inline-flex rounded-lg bg-surface-2 p-0.5" role="group" aria-label={t('page.dashboard.revenue')}>
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
                  {t(`page.dashboard.${candidate}`)}
                </button>
              ))}
            </div>
          }
        >
          <div className="-mx-2">
            {tokens ? (
              <ReactApexChart
                key={`revenue-${chartKey}`}
                series={[
                  { name: t('page.dashboard.income'), data: revenue.income },
                  { name: t('page.dashboard.expenses'), data: revenue.expenses },
                ]}
                options={revenueOptions(tokens)}
                type="area"
                height={320}
                width="100%"
              />
            ) : (
              <ChartSkeleton height={320} />
            )}
          </div>
        </ChartCard>

        <ChartCard title={t('page.dashboard.salesByCategory')}>
          <div className="flex flex-1 items-center">
            <div className="w-full">
              {tokens ? (
                <ReactApexChart key={`category-${chartKey}`} series={[1250, 850, 420]} options={categoryOptions(tokens)} type="donut" height={320} width="100%" />
              ) : (
                <ChartSkeleton height={320} />
              )}
            </div>
          </div>
        </ChartCard>
      </div>

      {/* Daily sales, summary and orders */}
      <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        <ChartCard title={t('page.dashboard.dailySales')} subtitle={t('page.dashboard.goToColumns')}>
          {tokens ? (
            <ReactApexChart
              key={`daily-${chartKey}`}
              series={[
                { name: t('page.dashboard.sales'), data: [58, 65, 72, 83, 59, 75, 68] },
                { name: 'Last Week', data: [42, 53, 48, 35, 41, 62, 55] },
              ]}
              options={dailyOptions(tokens)}
              type="bar"
              height={200}
              width="100%"
            />
          ) : (
            <ChartSkeleton height={200} />
          )}
        </ChartCard>

        <ChartCard title={t('page.dashboard.summary')}>
          <ul className="flex flex-1 flex-col justify-center gap-6">
            {summary.map((item) => {
              const Icon = item.icon
              return (
                <li key={item.key} className="flex items-center gap-3">
                  <span className={cn('grid size-9 shrink-0 place-content-center rounded-lg', item.tile)}>
                    <Icon className="size-4" />
                  </span>
                  <div className="min-w-0 flex-1">
                    <div className="mb-2 flex items-baseline justify-between gap-2 text-sm">
                      <span className="text-muted-foreground">{t(`page.dashboard.${item.key}`)}</span>
                      <span className="font-semibold text-foreground tabular-nums">{formats.currency.format(item.value)}</span>
                    </div>
                    <div className="h-1.5 rounded-full bg-surface-2">
                      <div className={cn('h-full rounded-full', item.bar)} style={{ width: `${item.share}%` }} />
                    </div>
                  </div>
                </li>
              )
            })}
          </ul>
        </ChartCard>

        <ChartCard
          className="overflow-hidden md:col-span-2 xl:col-span-1"
          title={t('page.dashboard.totalOrders')}
          actions={
            <span className="grid size-9 place-content-center rounded-lg bg-success/10 text-success">
              <ShoppingCart className="size-4" />
            </span>
          }
        >
          <div className="flex items-center gap-2">
            <span className="text-2xl font-semibold tracking-tight text-foreground tabular-nums">{formats.number.format(4875)}</span>
            <Badge variant="success" className="tabular-nums">
              <TrendingUp className="size-3" />
              {formats.percent.format(0.056)}
            </Badge>
          </div>
          <div className="-mx-5 mt-auto -mb-5 pt-4">
            {tokens ? (
              <ReactApexChart
                key={`orders-${chartKey}`}
                series={[{ name: t('page.dashboard.sales'), data: [45, 52, 49, 65, 58, 72, 63, 75, 60, 68] }]}
                options={ordersOptions(tokens)}
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
