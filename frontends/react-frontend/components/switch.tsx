import React, { InputHTMLAttributes } from 'react'
import { cn } from '@/utils/commonUtils'
import { cva, type VariantProps } from "class-variance-authority"
import { Field } from 'formik'

const switchVariants = cva(
  "",
  {
    variants: {
      variant: {
        solid: {
          default: "bg-[#ebedf2] dark:bg-dark block h-full rounded-full before:absolute before:left-1 before:bg-white dark:before:bg-white-dark dark:peer-checked:before:bg-white before:bottom-1 before:w-4 before:h-4 before:rounded-full peer-checked:before:left-7 before:transition-all before:duration-300",
          primary: "peer-checked:bg-primary",
          info: "peer-checked:bg-info",
          success: "peer-checked:bg-success",
          warning: "peer-checked:bg-warning",
          danger: "peer-checked:bg-danger",
          secondary: "peer-checked:bg-secondary",
          dark: "peer-checked:bg-dark",
          primary_outline: "peer-checked:border-primary peer-checked:before:bg-primary"
        }
      },
    },
    defaultVariants: {
      variant: "solid",
    },
  }
)

interface SwitchProps extends InputHTMLAttributes<HTMLInputElement>, VariantProps<typeof switchVariants> {
  name: string
}

const Switch: React.FC<SwitchProps> = ({
  className,
  variant,
  ...props
}) => {
  return (
    <label className="w-12 h-6 relative">
      <Field
        type="checkbox"
        className="custom_switch absolute w-full h-full opacity-0 z-10 cursor-pointer peer"
        {...props}
        id={props.id ? props.id : props.name}
      />
      <span className={cn(switchVariants({ variant, className }))}></span>
    </label>
  )
}

export default Switch
