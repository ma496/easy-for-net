'use client'
import { useId, useState } from 'react'
import { useAppDispatch, useAppSelector } from '@/store/hooks'
import { toggleAnimation, toggleNavbar, toggleTheme } from '@/store/slices/themeConfigSlice'
import { Settings, X, Sun, Moon, Laptop } from 'lucide-react'

/**
 * Off-canvas "template customizer" panel that exposes runtime toggles for color scheme, navbar type, and router transition animation, persisting choices via the theme config slice.
 */
export const Setting = () => {
  const themeConfig = useAppSelector((state) => state.theme)
  const dispatch = useAppDispatch()

  const [showCustomizer, setShowCustomizer] = useState(false)

  return (
    <div>
      <div className={`${(showCustomizer && 'block!') || ''} fixed inset-0 z-51 hidden bg-[black]/60 px-4 transition-[display]`} onClick={() => setShowCustomizer(false)}></div>

      <nav
        className={`${(showCustomizer && 'ltr:right-0! rtl:left-0!') || ''
          } fixed top-0 bottom-0 z-51 w-full max-w-100 bg-white p-4 shadow-[5px_0_25px_0_rgba(94,92,154,0.1)] transition-[right] duration-300 ltr:-right-100 rtl:-left-100 dark:bg-black`}
      >
        <button
          type="button"
          className="absolute top-0 bottom-0 my-auto flex h-10 w-12 cursor-pointer items-center justify-center bg-primary text-white ltr:-left-12 ltr:rounded-tl-full ltr:rounded-bl-full rtl:-right-12 rtl:rounded-tr-full rtl:rounded-br-full"
          onClick={() => setShowCustomizer(!showCustomizer)}
        >
          <Settings size={16} className="animate-[spin_3s_linear_infinite]" />
        </button>

        <div className="perfect-scrollbar h-full overflow-x-hidden overflow-y-auto">
          <div className="relative pb-5 text-center">
            <button type="button" className="absolute top-0 opacity-30 hover:opacity-100 ltr:right-0 rtl:left-0 dark:text-white" onClick={() => setShowCustomizer(false)}>
              <X size={16} />
            </button>

            <h4 className="mb-1 dark:text-white">TEMPLATE CUSTOMIZER</h4>
            <p className="text-white-dark">Set preferences that will be cookied for your live preview demonstration.</p>
          </div>

          <div className="mb-3 rounded-md border border-dashed border-white-light p-3 dark:border-[#1b2e4b]">
            <h5 className="mb-1 text-base leading-none dark:text-white">Color Scheme</h5>
            <p className="text-xs text-white-dark">Overall light or dark presentation.</p>
            <div className="mt-3 grid grid-cols-3 gap-2">
              <button type="button" className={`${themeConfig.theme === 'light' ? 'btn-primary' : 'btn-outline-primary'} btn`} onClick={() => dispatch(toggleTheme('light'))}>
                <Sun size={16} className="ltr:mr-2 rtl:ml-2" />
                Light
              </button>

              <button type="button" className={`${themeConfig.theme === 'dark' ? 'btn-primary' : 'btn-outline-primary'} btn`} onClick={() => dispatch(toggleTheme('dark'))}>
                <Moon size={16} className="ltr:mr-2 rtl:ml-2" />
                Dark
              </button>

              <button type="button" className={`${themeConfig.theme === 'system' ? 'btn-primary' : 'btn-outline-primary'} btn`} onClick={() => dispatch(toggleTheme('system'))}>
                <Laptop size={16} className="ltr:mr-2 rtl:ml-2" />
                System
              </button>
            </div>
          </div>

          <div className="mb-3 rounded-md border border-dashed border-white-light p-3 dark:border-[#1b2e4b]">
            <h5 className="mb-1 text-base leading-none dark:text-white">Navbar Type</h5>
            <p className="text-xs text-white-dark">Sticky or Floating.</p>
            <div className="mt-3 flex items-center gap-3 text-primary">
              <label className="mb-0 inline-flex">
                <input type="radio" checked={themeConfig.navbar === 'navbar-sticky'} value="navbar-sticky" className="form-radio" onChange={() => dispatch(toggleNavbar('navbar-sticky'))} />
                <span>Sticky</span>
              </label>
              <label className="mb-0 inline-flex">
                <input type="radio" checked={themeConfig.navbar === 'navbar-floating'} value="navbar-floating" className="form-radio" onChange={() => dispatch(toggleNavbar('navbar-floating'))} />
                <span>Floating</span>
              </label>
              <label className="mb-0 inline-flex">
                <input type="radio" checked={themeConfig.navbar === 'navbar-static'} value="navbar-static" className="form-radio" onChange={() => dispatch(toggleNavbar('navbar-static'))} />
                <span>Static</span>
              </label>
            </div>
          </div>

          <div className="mb-3 rounded-md border border-dashed border-white-light p-3 dark:border-[#1b2e4b]">
            <h5 className="mb-1 text-base leading-none dark:text-white">Router Transition</h5>
            <p className="text-xs text-white-dark">Animation of main content.</p>
            <div className="mt-3">
              <select id={useId()} className="form-select border-primary text-primary" value={themeConfig.animation} onChange={(e) => dispatch(toggleAnimation(e.target.value))}>
                <option value=" ">None</option>
                <option value="animate__fadeIn">Fade</option>
                <option value="animate__fadeInDown">Fade Down</option>
                <option value="animate__fadeInUp">Fade Up</option>
                <option value="animate__fadeInLeft">Fade Left</option>
                <option value="animate__fadeInRight">Fade Right</option>
                <option value="animate__slideInDown">Slide Down</option>
                <option value="animate__slideInLeft">Slide Left</option>
                <option value="animate__slideInRight">Slide Right</option>
                <option value="animate__zoomIn">Zoom In</option>
              </select>
            </div>
          </div>
        </div>
      </nav>
    </div>
  )
}

