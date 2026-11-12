import { TypedUseSelectorHook, useDispatch, useSelector } from 'react-redux'
import type { RootState, AppDispatch } from './index'

/**
 * Typed wrapper around react-redux's useDispatch pre-bound to the app's
 * AppDispatch type, so dispatched thunks/actions are strongly typed.
 */
export const useAppDispatch = () => useDispatch<AppDispatch>()
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector
