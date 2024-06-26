import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Metadata } from "next"
import { useTranslations } from "next-intl"
import { LoginForm } from "./login-form"

export const metadata: Metadata = {
  title: 'Login'
}

export default function Login() {
  const t = useTranslations()

  return (
    <div className="flex justify-center items-center h-screen bg-muted/60">
      <Card className="w-full max-w-sm shadow-2xl">
        <CardHeader>
          <CardTitle className="flex justify-between">
            <span className="text-2xl">{t('Login')}</span>
          </CardTitle>
          <CardDescription>
            {t('LoginDescription')}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <LoginForm />
        </CardContent>
      </Card>
    </div>
  )
}
