export function notEmpty(value: string, error: string = ''): null | string {
  if (!value) {
    return error ? error : 'Field is required';
  }
  return null;
}

export function email(value: string, error: string = ''): null | string {
  return /^\S+@\S+$/.test(value) ? null : (error ? error : 'Invalid email');
}

export function stringLength(value: string, length: number | { min?: number, max?: number }, error: string = ''): null | string {
  if (typeof length === 'number') {
    if (!value || value.length !== length) {
      return error ? error : `Value must have exactly ${length} characters`;
    }
  } else {
    if (length.min && length.max) {
      if (!value || value.length < length.min || value.length > length.max) {
        return error ? error : `Value must have ${length.min}-${length.max} characters`;
      }
    } else if (length.min) {
      if (!value || value.length < length.min) {
        return error ? error : `Value must have ${length.min}  or more characters`;
      }
    } else if (length.max) {
      if (!value || value.length > length.max) {
        return error ? error : `Value must have ${length.max} or less characters`;
      }
    }
  }
  return null;
}

export function inRange(value: number, range: { min?: number, max?: number }, error: string = ''): null | string {
  if (range.min && range.max) {
    if (!value || value < range.min || value > range.max) {
      return error ? error : `Value must be between ${range.min} and ${range.max}`;
    }
  } else if (range.min) {
    if (!value || value < range.min) {
      return error ? error : `Value must be ${range.min} or more`;
    }
  } else if (range.max) {
    if (!value || value > range.max) {
      return error ? error : `Value must be ${range.max} or less`;
    }
  }
  return null;
}

export function notFalse(value: boolean, error: string = ''): null | string {
  if (!value) {
    return error ? error : 'Value must be true';
  }
  return null;
}

export function notTrue(value: boolean, error: string = ''): null | string {
  if (value) {
    return error ? error : 'Value must be false';
  }
  return null;
}
