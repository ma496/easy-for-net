import React, { InputHTMLAttributes } from 'react'
import { cn } from '@/utils/commonUtils'
import { cva, type VariantProps } from "class-variance-authority"
import { Field } from 'formik'

const checkboxVariants = cva(
  "form-checkbox",
  {
    variants: {
      variant: {
        primary: "text-primary",
        info: "text-info",
        success: "text-success",
        warning: "text-warning",
        danger: "text-danger",
        secondary: "text-secondary",
        dark: "text-dark",
      },
    },
    defaultVariants: {
      variant: "primary",
    },
  }
)

interface CheckboxProps extends InputHTMLAttributes<HTMLInputElement>, VariantProps<typeof checkboxVariants> {
  name: string
  label: string
}

const Checkbox: React.FC<CheckboxProps> = ({
  label,
  className,
  variant,
  ...props
}) => {
  return (
    <label className="flex cursor-pointer items-center">
      <Field
        type="checkbox"
        className={cn(checkboxVariants({ variant, className }))}
        {...props}
      />
      <span className="text-white-dark">{label}</span>
    </label>
  )
}

export default Checkbox
