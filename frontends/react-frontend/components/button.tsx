import React, { ButtonHTMLAttributes, ReactNode } from 'react';
import { cn } from '@/utils/commonUtils';
import { cva, type VariantProps } from "class-variance-authority"

const buttonVariants = cva(
  "btn",
  {
    variants: {
      variant: {
        primary: "btn-primary",
        info: "btn-info",
        success: "btn-success",
        warning: "btn-warning",
        danger: "btn-danger",
        secondary: "btn-secondary",
        dark: "btn-dark",
        primary_outline: "btn-outline-primary",
        info_outline: "btn-outline-info",
        success_outline: "btn-outline-success",
        warning_outline: "btn-outline-warning",
        danger_outline: "btn-outline-danger",
        secondary_outline: "btn-outline-secondary",
        dark_outline: "btn-outline-dark"
      },
      size: {
        sm: "btn-sm",
        md: "",
        lg: "btn-lg"
      },
      rounded: {
        no: '',
        yes: 'rounded-full'
      }
    },
    defaultVariants: {
      variant: "primary",
      size: "md",
      rounded: 'no'
    },
  }
)

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {
  prefixElm?: ReactNode
}

const Button: React.FC<ButtonProps> = ({
  children,
  className,
  variant,
  size,
  rounded,
  prefixElm,
  ...props }) => {

  return (
    <button
      className={cn(buttonVariants({ variant, size, rounded, className }))}
      {...props}>
      <div className='flex gap-2 justify-center items-center'>
        {prefixElm}
        {children}
      </div>
    </button>
  )
}

export default Button
