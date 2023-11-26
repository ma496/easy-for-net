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

export const rtkErrorHandler = (api: MiddlewareAPI) => (next: any) => (action: any) => {
  if (isRejectedWithValue(action)) {
    if (action.payload.status === 401 || action.payload.status === 403 || action.payload.status === 404
      || action.payload.status === 422 || action.payload.status === 500) {
      api.dispatch(setError({ title: action.payload.data.title, message: action.payload.data.detail }))
    } else if (action.payload.status === 400) {
      if (action.payload.data.errors) {
        api.dispatch(setError({ title: action.payload.data.title, message: getValidationMessage(action.payload.data.errors) }))
      }
    }
  }

  return next(action);
};
