import { MiddlewareAPI, isRejected, isRejectedWithValue } from '@reduxjs/toolkit';
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

const isError = (action: any, status: number): boolean => {

  let payloadStatus = action.payload?.status

  const ignoreStatuses = action.payload?.meta?.ignoreStatuses
  if (payloadStatus === status && !ignoreStatuses) {
    return true
  }
  if (payloadStatus === status && ignoreStatuses) {
    const ignoreStatus = ignoreStatuses.find((is: any) => is === status)
    return !ignoreStatus
  }

  return false
}

export const rtkErrorHandler = (api: MiddlewareAPI) => (next: any) => (action: any) => {
  if (isRejectedWithValue(action)) {
    debugger;
    if (isError(action, 400) && action.payload.data.errors) {
      api.dispatch(setError({ title: 'Error', message: getValidationMessage(action.payload.data.errors) }))
    } else if (isError(action, 401)) {
      api.dispatch(setError({ title: 'Error', message: 'Please login!' }))
    } else if (isError(action, 403)) {
      api.dispatch(setError({ title: 'Error', message: 'You are not allowed to access this resource.' }))
    } else if (isError(action, 404)) {
      api.dispatch(setError({ title: 'Error', message: 'Not Found!' }))
    } else if (isError(action, 500)) {
      api.dispatch(setError({ title: 'Error', message: 'Internal Server Error!' }))
    }
  }

  return next(action);
};
