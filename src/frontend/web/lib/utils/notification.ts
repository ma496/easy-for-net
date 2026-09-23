import { getTranslation } from '@/i18n'
import { SweetAlertOptions, SweetAlertResult } from 'sweetalert2'

const Swal = (await import('sweetalert2')).default

/**
 * Centres the toast over the page header and places it 2px below the header's bottom edge,
 * measured at open time so it follows the sidebar width, the menu layout and a scrolled-away header.
 */
function placeToastBelowHeader(): void {
  const container = Swal.getContainer()
  const header = document.querySelector('header')
  if (!container || !header) return
  const rect = header.getBoundingClientRect()
  container.style.top = `${Math.max(rect.bottom, 0)}px`
  container.style.paddingTop = '2px'
  container.style.left = `${rect.left + rect.width / 2}px`
}

/** SweetAlert2 mixin configured as a generic, top-center toast with a 3s timer. */
export const toast = Swal.mixin({
  toast: true,
  position: 'top',
  willOpen: placeToastBelowHeader,
  timer: 3000,
  timerProgressBar: true,
  showConfirmButton: false,
  showCloseButton: true,
})
/** SweetAlert2 mixin for success toasts (top-center, success icon, 5s timer). */
export const successToast = Swal.mixin({
  toast: true,
  position: 'top',
  willOpen: placeToastBelowHeader,
  timer: 5000,
  icon: 'success',
  timerProgressBar: true,
  showConfirmButton: false,
  showCloseButton: true,
})
/** SweetAlert2 mixin for error toasts (top-center, error icon, 7s timer). */
export const errorToast = Swal.mixin({
  toast: true,
  position: 'top',
  willOpen: placeToastBelowHeader,
  timer: 7000,
  icon: 'error',
  timerProgressBar: true,
  showConfirmButton: false,
  showCloseButton: true,
})

/**
 * Wraps SweetAlert2.fire, applying localized default button labels
 * (common.ok, common.cancel) and theme colors when the caller does not
 * supply them. Returns the SweetAlert2 result promise.
 */
export async function sweetAlert(params: SweetAlertOptions): Promise<SweetAlertResult<unknown>> {
  const { t } = getTranslation()
  const result = await Swal.fire({
    ...params,
    confirmButtonText: params.confirmButtonText ? params.confirmButtonText : t('common.ok'),
    cancelButtonText: params.cancelButtonText ? params.cancelButtonText : t('common.cancel'),
    confirmButtonColor: params.confirmButtonColor ? params.confirmButtonColor : '#4361ee',
    cancelButtonColor: params.cancelButtonColor ? params.cancelButtonColor : '#805dca',
  })
  return result
}

/** Pre-configured sweetAlert call with success title/icon defaults. */
export async function successAlert(params: SweetAlertOptions): Promise<SweetAlertResult<unknown>> {
  const { t } = getTranslation()
  if (!params.title) params.title = t('common.success')
  if (!params.icon) params.icon = 'success'
  return sweetAlert(params)
}

/** Pre-configured sweetAlert call with error title/icon defaults. */
export async function errorAlert(params: SweetAlertOptions): Promise<SweetAlertResult<unknown>> {
  const { t } = getTranslation()
  if (!params.title) params.title = t('common.error')
  if (!params.icon) params.icon = 'error'
  return sweetAlert(params)
}

/** Pre-configured sweetAlert call with warning title/icon defaults. */
export async function warningAlert(params: SweetAlertOptions): Promise<SweetAlertResult<unknown>> {
  const { t } = getTranslation()
  if (!params.title) params.title = t('common.warning')
  if (!params.icon) params.icon = 'warning'
  return sweetAlert(params)
}

/** Pre-configured sweetAlert call with info title/icon defaults. */
export async function infoAlert(params: SweetAlertOptions): Promise<SweetAlertResult<unknown>> {
  const { t } = getTranslation()
  if (!params.title) params.title = t('common.info')
  if (!params.icon) params.icon = 'info'
  return sweetAlert(params)
}

/** Pre-configured sweetAlert call for confirmation dialogs (question icon, default confirm/cancel labels and cancel button). */
export async function confirmAlert(params: SweetAlertOptions): Promise<SweetAlertResult<unknown>> {
  const { t } = getTranslation()
  if (!params.icon) params.icon = 'question'
  if (params.showCancelButton === undefined) params.showCancelButton = true
  if (!params.confirmButtonText) params.confirmButtonText = t('common.confirm')
  if (!params.cancelButtonText) params.cancelButtonText = t('common.cancel')
  return sweetAlert(params)
}

/** Pre-configured sweetAlert call for destructive confirmations (warning icon, red confirm button, localized delete/cancel labels). */
export async function confirmDeleteAlert(params: SweetAlertOptions): Promise<SweetAlertResult<unknown>> {
  const { t } = getTranslation()
  if (!params.confirmButtonColor) params.confirmButtonColor = '#d33'
  if (!params.cancelButtonColor) params.cancelButtonColor = '#4361ee'
  if (!params.icon) params.icon = 'warning'
  if (params.showCancelButton === undefined) params.showCancelButton = true
  if (!params.confirmButtonText) params.confirmButtonText = t('common.deleteConfirm')
  if (!params.cancelButtonText) params.cancelButtonText = t('common.cancel')

  return sweetAlert(params)
}
