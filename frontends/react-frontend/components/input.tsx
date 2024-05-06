import React, { InputHTMLAttributes, ReactNode } from 'react';
import { useField, useFormikContext } from 'formik';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  name: string
  prefixElm?: ReactNode
  postfixElm?: ReactNode
}

const Input: React.FC<InputProps> = ({ label, prefixElm, postfixElm, ...props }) => {
  const [field, meta] = useField(props)
  const { submitCount } = useFormikContext();

  return (
    <div className={submitCount || meta.touched ? (meta.error ? 'has-error' : 'has-success') : ''}>
      <label htmlFor={props.name}>{label}</label>
      <div className="relative text-white-dark">
        {
          prefixElm && (
            <span className="absolute start-4 top-1/2 -translate-y-1/2">
              {prefixElm}
            </span>
          )}
        <input
          {...field}
          {...props}
          id={props.id ? props.id : props.name}
          className={`form-input ps-10 placeholder:text-white-dark`}
        />
        {
          postfixElm && ( // Check if postfixElm is provided
            <span className="absolute end-4 top-1/2 -translate-y-1/2"> {/* Adjusted positioning */}
              {postfixElm}
            </span>
          )}
      </div>
      {(submitCount || meta.touched) && meta.error ? (
        <div className="text-danger mt-1">{meta.error}</div>
      ) : null}
    </div>
  )
}

export default Input
