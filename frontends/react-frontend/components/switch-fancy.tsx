import React, { InputHTMLAttributes } from 'react'
import { cn } from '@/utils/commonUtils'
import { cva, type VariantProps } from "class-variance-authority"
import { Field } from 'formik'

const switchFancyVariants = cva(
  "outline_checkbox bg-icon border-2 border-[#ebedf2] dark:border-white-dark block h-full rounded-full before:absolute before:left-1 before:bg-[#ebedf2] dark:before:bg-white-dark before:bottom-1 before:w-4 before:h-4 before:rounded-full before:bg-[url(/assets/images/close.svg)] before:bg-no-repeat before:bg-center peer-checked:before:left-7 peer-checked:before:bg-[url(/assets/images/checked.svg)] before:transition-all before:duration-300",
  {
    variants: {
      variant: {
        primary: "peer-checked:border-primary peer-checked:before:bg-primary",
        info: "peer-checked:border-info peer-checked:before:bg-info",
        success: "peer-checked:border-success peer-checked:before:bg-success",
        warning: "peer-checked:border-warning peer-checked:before:bg-warning",
        danger: "peer-checked:border-danger peer-checked:before:bg-danger",
        secondary: "peer-checked:border-secondary peer-checked:before:bg-secondary",
        dark: "peer-checked:border-dark peer-checked:before:bg-dark",
      },
    },
    defaultVariants: {
      variant: "primary",
    },
  }
)

interface SwitchFancyProps extends InputHTMLAttributes<HTMLInputElement>, VariantProps<typeof switchFancyVariants> {
  name: string
}

const SwitchFancy: React.FC<SwitchFancyProps> = ({
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
      <span className={cn(switchFancyVariants({ variant, className }))}></span>
    </label>
  )
}

export default SwitchFancy
