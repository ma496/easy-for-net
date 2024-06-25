import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Metadata } from "next"
import { useTranslations } from "next-intl"

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
        <CardContent className="grid gap-4">
          <div className="grid gap-2">
            <Label htmlFor="email">{t('Email')}</Label>
            <Input id="email" type="email" placeholder="m@example.com" required />
          </div>
          <div className="grid gap-2">
            <Label htmlFor="password">{t('Password')}</Label>
            <Input id="password" type="password" required />
          </div>
        </CardContent>
        <CardFooter className="flex flex-col gap-2">
          <Button className="w-full">{t('Signin')}</Button>
        </CardFooter>
      </Card>
    </div>
  )
}
