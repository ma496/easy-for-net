import { MiddlewareAPI, isRejectedWithValue } from '@reduxjs/toolkit';
import { setError } from '../slices/errorSlice';

const getValidationMessage = (errors: any) => {
  let message = ''
  for (const field in errors) {
    if (errors.hasOwnProperty(field)) {
      for (const error of errors[field]) {
        message += `${error}\n`
      }
    }
  }
  return message
}

const isSetError = (action: any, ...statuses: number[]): boolean => {
  if (!statuses) {
    return false
  }

  for (let status of statuses) {
    const ignoreStatuses = action.payload?.meta?.ignoreStatuses
    if (action.payload.status === status && !ignoreStatuses) {
      return true
    }
    if (action.payload.status === status && ignoreStatuses) {
      const ignoreStatus = ignoreStatuses.find((s: any) => s === status)
      return !ignoreStatus
    }
  }

  return false
}

export const rtkErrorHandler = (api: MiddlewareAPI) => (next: any) => (action: any) => {
  if (isRejectedWithValue(action)) {
    if (isSetError(action, 401, 403, 404, 500)) {
      api.dispatch(setError({ title: action.payload.data.title, message: action.payload.data.detail }))
    } else if (isSetError(action, 400)) {
      if (action.payload.data.errors) {
        api.dispatch(setError({ title: action.payload.data.title, message: getValidationMessage(action.payload.data.errors) }))
      } else {
        api.dispatch(setError({ title: action.payload.data.title, message: action.payload.data.detail }))
      }
    }
  }

  return next(action);
};
