'use client'
import { Dropdown } from '@/components/ui/dropdown'
import { useEffect, useState } from 'react'
import ReactApexChart from 'react-apexcharts'
import { ApexOptions } from 'apexcharts'
import { useAppSelector } from '@/store/hooks'
import { CreditCard, DollarSign, MoreHorizontal, Inbox, ShoppingCart, Tag } from 'lucide-react'
import { useTranslation } from '@/i18n'

/**
 * Interactive client-side sales dashboard page content with revenue, category, daily sales, and total orders charts that adapt to the active theme and direction.
 */
export const SaleDashboard = () => {
  const isDark = useAppSelector((state) => state.theme.theme === 'dark' || state.theme.isDarkMode)
  const isRtl = useAppSelector((state) => state.theme.rtlClass) === 'rtl'
  const { t } = useTranslation()

  const [isMounted, setIsMounted] = useState(false)
  const [chartKey, setChartKey] = useState(0)

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsMounted(true)
  }, [])

  // Update charts when theme changes
  useEffect(() => {
    if (isMounted) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setChartKey((prev) => prev + 1)
    }
  }, [isDark, isMounted])

  //Revenue Chart
  const revenueChart: { series: ApexAxisChartSeries; options: ApexOptions } = {
    series: [
      {
        name: t('page.dashboard.income'),
        data: [18500, 19200, 17800, 21500, 18900, 22000, 24500, 23000, 21800, 25000, 23500, 26000],
      },
      {
        name: t('page.dashboard.expenses'),
        data: [15800, 16300, 15000, 16500, 15200, 17800, 14500, 15200, 14800, 16500, 15800, 17200],
      },
    ],
    options: {
      chart: {
        height: 325,
        type: 'area',
        fontFamily: 'Nunito, sans-serif',
        zoom: {
          enabled: false,
        },
        toolbar: {
          show: false,
        },
        dropShadow: {
          enabled: true,
          opacity: 0.2,
          blur: 10,
          left: -7,
          top: 22,
        },
      },

      dataLabels: {
        enabled: false,
      },
      stroke: {
        show: true,
        curve: 'smooth',
        width: 2,
        lineCap: 'square',
      },

      colors: isDark ? ['#2196F3', '#E7515A'] : ['#1B55E2', '#E7515A'],
      markers: {
        discrete: [
          {
            seriesIndex: 0,
            dataPointIndex: 6,
            fillColor: '#1B55E2',
            strokeColor: 'transparent',
            size: 7,
          },
          {
            seriesIndex: 1,
            dataPointIndex: 5,
            fillColor: '#E7515A',
            strokeColor: 'transparent',
            size: 7,
          },
        ],
      },
      labels: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
      xaxis: {
        axisBorder: {
          show: false,
        },
        axisTicks: {
          show: false,
        },
        crosshairs: {
          show: true,
        },
        labels: {
          offsetX: isRtl ? 2 : 0,
          offsetY: 5,
          style: {
            fontSize: '12px',
            cssClass: 'apexcharts-xaxis-title',
          },
        },
      },
      yaxis: {
        tickAmount: 7,
        labels: {
          formatter: (value: number) => {
            return value / 1000 + 'K'
          },
          offsetX: isRtl ? -30 : -10,
          offsetY: 0,
          style: {
            fontSize: '12px',
            cssClass: 'apexcharts-yaxis-title',
          },
        },
        opposite: isRtl ? true : false,
      },
      grid: {
        borderColor: isDark ? '#191E3A' : '#E0E6ED',
        strokeDashArray: 5,
        xaxis: {
          lines: {
            show: false,
          },
        },
        yaxis: {
          lines: {
            show: true,
          },
        },
        padding: {
          top: 0,
          right: 0,
          bottom: 0,
          left: 0,
        },
      },
      legend: {
        position: 'top',
        horizontalAlign: 'right',
        fontSize: '16px',
        markers: {
          offsetX: -2,
        },
        itemMargin: {
          horizontal: 10,
          vertical: 5,
        },
      },
      tooltip: {
        marker: {
          show: true,
        },
        x: {
          show: false,
        },
      },
      fill: {
        type: 'gradient',
        gradient: {
          shadeIntensity: 1,
          inverseColors: !1,
          opacityFrom: isDark ? 0.19 : 0.28,
          opacityTo: 0.05,
          stops: isDark ? [100, 100] : [45, 100],
        },
      },
    },
  }

  //Sales By Category
  const salesByCategory: { series: number[]; options: ApexOptions } = {
    series: [1250, 850, 420],
    options: {
      chart: {
        type: 'donut',
        height: 460,
        fontFamily: 'Nunito, sans-serif',
      },
      dataLabels: {
        enabled: false,
      },
      stroke: {
        show: true,
        width: 25,
        colors: isDark ? ['#0e1726'] : ['#fff'],
      },
      colors: isDark ? ['#5c1ac3', '#e2a03f', '#e7515a', '#e2a03f'] : ['#e2a03f', '#5c1ac3', '#e7515a'],
      legend: {
        position: 'bottom',
        horizontalAlign: 'center',
        fontSize: '14px',
        markers: {
          offsetX: -2,
        },
        height: 50,
        offsetY: 20,
      },
      plotOptions: {
        pie: {
          donut: {
            size: '65%',
            background: 'transparent',
            labels: {
              show: true,
              name: {
                show: true,
                fontSize: '29px',
                offsetY: -10,
              },
              value: {
                show: true,
                fontSize: '26px',
                color: isDark ? '#bfc9d4' : undefined,
                offsetY: 16,
                formatter: (val: string) => {
                  return val
                },
              },
              total: {
                show: true,
                label: 'Total',
                color: '#888ea8',
                fontSize: '29px',
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                formatter: (w: any) => {
                  return w.globals.seriesTotals.reduce(function (a: number, b: number) {
                    return a + b
                  }, 0)
                },
              },
            },
          },
        },
      },
      labels: ['Apparel', 'Sports', 'Others'],
      states: {
        hover: {
          filter: {
            type: 'none',
            value: 0.15,
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
          } as any,
        },
        active: {
          filter: {
            type: 'none',
            value: 0.15,
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
          } as any,
        },
      },
    },
  }

  //Daily Sales
  const dailySales: { series: ApexAxisChartSeries; options: ApexOptions } = {
    series: [
      {
        name: t('page.dashboard.sales'),
        data: [58, 65, 72, 83, 59, 75, 68],
      },
      {
        name: 'Last Week',
        data: [42, 53, 48, 35, 41, 62, 55],
      },
    ],
    options: {
      chart: {
        height: 160,
        type: 'bar',
        fontFamily: 'Nunito, sans-serif',
        toolbar: {
          show: false,
        },
        stacked: true,
        stackType: '100%',
      },
      dataLabels: {
        enabled: false,
      },
      stroke: {
        show: true,
        width: 1,
      },
      colors: ['#e2a03f', '#e0e6ed'],
      responsive: [
        {
          breakpoint: 480,
          options: {
            legend: {
              position: 'bottom',
              offsetX: -10,
              offsetY: 0,
            },
          },
        },
      ],
      xaxis: {
        labels: {
          show: false,
        },
        categories: ['Sun', 'Mon', 'Tue', 'Wed', 'Thur', 'Fri', 'Sat'],
      },
      yaxis: {
        show: false,
      },
      fill: {
        opacity: 1,
      },
      plotOptions: {
        bar: {
          horizontal: false,
          columnWidth: '25%',
        },
      },
      legend: {
        show: false,
      },
      grid: {
        show: false,
        xaxis: {
          lines: {
            show: false,
          },
        },
        padding: {
          top: 10,
          right: -20,
          bottom: -20,
          left: -20,
        },
      },
    },
  }

  //Total Orders
  const totalOrders: { series: ApexAxisChartSeries; options: ApexOptions } = {
    series: [
      {
        name: t('page.dashboard.sales'),
        data: [45, 52, 49, 65, 58, 72, 63, 75, 60, 68],
      },
    ],
    options: {
      chart: {
        height: 290,
        type: 'area',
        fontFamily: 'Nunito, sans-serif',
        sparkline: {
          enabled: true,
        },
      },
      stroke: {
        curve: 'smooth',
        width: 2,
      },
      colors: isDark ? ['#00ab55'] : ['#00ab55'],
      labels: ['1', '2', '3', '4', '5', '6', '7', '8', '9', '10'],
      yaxis: {
        min: 0,
        show: false,
      },
      grid: {
        padding: {
          top: 125,
          right: 0,
          bottom: 0,
          left: 0,
        },
      },
      fill: {
        opacity: 1,
        type: 'gradient',
        gradient: {
          type: 'vertical',
          shadeIntensity: 1,
          inverseColors: !1,
          opacityFrom: 0.3,
          opacityTo: 0.05,
          stops: [100, 100],
        },
      },
      tooltip: {
        x: {
          show: false,
        },
      },
    },
  }

  return (
    <div>
      <div className="mb-6 grid gap-6 xl:grid-cols-3">
        <div className="panel h-full xl:col-span-2">
          <div className="mb-5 flex items-center justify-between dark:text-white-light">
            <h5 className="text-lg font-semibold">{t('page.dashboard.revenue')}</h5>
            <div className="dropdown">
              <Dropdown placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`} button={<MoreHorizontal className="text-black/70 hover:text-primary! dark:text-white/70" />}>
                <ul>
                  <li>
                    <button type="button">{t('page.dashboard.weekly')}</button>
                  </li>
                  <li>
                    <button type="button">{t('page.dashboard.monthly')}</button>
                  </li>
                  <li>
                    <button type="button">{t('page.dashboard.yearly')}</button>
                  </li>
                </ul>
              </Dropdown>
            </div>
          </div>
          <p className="text-lg dark:text-white-light/90">
            {t('page.dashboard.totalProfit')} <span className="ml-2 text-primary">$18,750</span>
          </p>
          <div className="relative">
            <div className="rounded-lg bg-white dark:bg-black">
              {isMounted ? (
                <ReactApexChart series={revenueChart.series} options={revenueChart.options} type="area" height={325} width={'100%'} />
              ) : (
                <div className="dark:bg-opacity-[0.08] grid min-h-81.25 place-content-center bg-white-light/30 dark:bg-dark">
                  <span className="inline-flex h-5 w-5 animate-spin rounded-full border-2 border-black border-l-transparent! dark:border-white"></span>
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="panel h-full">
          <div className="mb-5 flex items-center">
            <h5 className="text-lg font-semibold dark:text-white-light">{t('page.dashboard.salesByCategory')}</h5>
          </div>
          <div>
            <div className="rounded-lg bg-white dark:bg-black">
              {isMounted ? (
                <ReactApexChart key={`sales-by-category-${chartKey}`} series={salesByCategory.series} options={salesByCategory.options} type="donut" height={460} width={'100%'} />
              ) : (
                <div className="dark:bg-opacity-[0.08] grid min-h-81.25 place-content-center bg-white-light/30 dark:bg-dark">
                  <span className="inline-flex h-5 w-5 animate-spin rounded-full border-2 border-black border-l-transparent! dark:border-white"></span>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      <div className="mb-6 grid gap-6 sm:grid-cols-2 xl:grid-cols-3">
        <div className="panel h-full sm:col-span-2 xl:col-span-1">
          <div className="mb-5 flex items-center">
            <h5 className="text-lg font-semibold dark:text-white-light">
              {t('page.dashboard.dailySales')}
              <span className="block text-sm font-normal text-white-dark">{t('page.dashboard.goToColumns')}</span>
            </h5>
            <div className="relative ltr:ml-auto rtl:mr-auto">
              <div className="grid h-11 w-11 place-content-center rounded-full bg-[#ffeccb] text-warning dark:bg-warning dark:text-[#ffeccb]">
                <DollarSign size={16} />
              </div>
            </div>
          </div>
          <div>
            <div className="rounded-lg bg-white dark:bg-black">
              {isMounted ? (
                <ReactApexChart series={dailySales.series} options={dailySales.options} type="bar" height={160} width={'100%'} />
              ) : (
                <div className="dark:bg-opacity-[0.08] grid min-h-81.25 place-content-center bg-white-light/30 dark:bg-dark">
                  <span className="inline-flex h-5 w-5 animate-spin rounded-full border-2 border-black border-l-transparent! dark:border-white"></span>
                </div>
              )}
            </div>
          </div>
        </div>
        <div className="panel h-full">
          <div className="mb-5 flex items-center justify-between dark:text-white-light">
            <h5 className="text-lg font-semibold">{t('page.dashboard.summary')}</h5>
            <div className="dropdown">
              <Dropdown placement={`${isRtl ? 'bottom-start' : 'bottom-end'}`} button={<MoreHorizontal size={16} />}>
                <ul>
                  <li>
                    <button type="button">{t('page.dashboard.viewReport')}</button>
                  </li>
                  <li>
                    <button type="button">{t('page.dashboard.editReport')}</button>
                  </li>
                  <li>
                    <button type="button">{t('page.dashboard.markAsDone')}</button>
                  </li>
                </ul>
              </Dropdown>
            </div>
          </div>
          <div className="space-y-9">
            <div className="flex items-center">
              <div className="h-9 w-9 ltr:mr-3 rtl:ml-3">
                <div className="grid h-9 w-9 place-content-center rounded-full bg-secondary-light text-secondary dark:bg-secondary dark:text-secondary-light">
                  <Inbox size={16} />
                </div>
              </div>
              <div className="flex-1">
                <div className="mb-2 flex font-semibold text-white-dark">
                  <h6>{t('page.dashboard.income')}</h6>
                  <p className="ltr:ml-auto rtl:mr-auto">$125,800</p>
                </div>
                <div className="h-2 rounded-full bg-dark-light shadow-sm dark:bg-[#1b2e4b]">
                  <div className="h-full w-11/12 rounded-full bg-linear-to-r from-[#7579ff] to-[#b224ef]"></div>
                </div>
              </div>
            </div>
            <div className="flex items-center">
              <div className="h-9 w-9 ltr:mr-3 rtl:ml-3">
                <div className="grid h-9 w-9 place-content-center rounded-full bg-success-light text-success dark:bg-success dark:text-success-light">
                  <Tag size={16} />
                </div>
              </div>
              <div className="flex-1">
                <div className="mb-2 flex font-semibold text-white-dark">
                  <h6>{t('page.dashboard.profit')}</h6>
                  <p className="ltr:ml-auto rtl:mr-auto">$52,350</p>
                </div>
                <div className="h-2 w-full rounded-full bg-dark-light shadow-sm dark:bg-[#1b2e4b]">
                  <div className="h-full w-full rounded-full bg-linear-to-r from-[#3cba92] to-[#0ba360]" style={{ width: '65%' }}></div>
                </div>
              </div>
            </div>
            <div className="flex items-center">
              <div className="h-9 w-9 ltr:mr-3 rtl:ml-3">
                <div className="grid h-9 w-9 place-content-center rounded-full bg-warning-light text-warning dark:bg-warning dark:text-warning-light">
                  <CreditCard size={16} />
                </div>
              </div>
              <div className="flex-1">
                <div className="mb-2 flex font-semibold text-white-dark">
                  <h6>{t('page.dashboard.expenses')}</h6>
                  <p className="ltr:ml-auto rtl:mr-auto">$73,450</p>
                </div>
                <div className="h-2 w-full rounded-full bg-dark-light shadow-sm dark:bg-[#1b2e4b]">
                  <div className="h-full w-full rounded-full bg-linear-to-r from-[#f09819] to-[#ff5858]" style={{ width: '80%' }}></div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div className="panel h-full p-0">
          <div className="absolute flex w-full items-center justify-between p-5">
            <div className="relative">
              <div className="flex h-11 w-11 items-center justify-center rounded-lg bg-success-light text-success dark:bg-success dark:text-success-light">
                <ShoppingCart size={16} />
              </div>
            </div>
            <h5 className="text-2xl font-semibold ltr:text-right rtl:text-left dark:text-white-light">
              4,875
              <span className="block text-sm font-normal">{t('page.dashboard.totalOrders')}</span>
            </h5>
          </div>
          <div className="rounded-lg bg-transparent">
            {/* loader */}
            {isMounted ? (
              <ReactApexChart series={totalOrders.series} options={totalOrders.options} type="area" height={290} width={'100%'} />
            ) : (
              <div className="dark:bg-opacity-[0.08] grid min-h-81.25 place-content-center bg-white-light/30 dark:bg-dark">
                <span className="inline-flex h-5 w-5 animate-spin rounded-full border-2 border-black border-l-transparent! dark:border-white"></span>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  )
}

