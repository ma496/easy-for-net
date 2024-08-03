"use client"
import { Button } from "@/components/ui/button";
import { Form, FormControl, FormField, FormItem, FormLabel, FormMessage } from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { zodResolver } from "@hookform/resolvers/zod"
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form"
import { z } from "zod"
import { useI18nZodErrors } from '@/lib/useI18nZodErrors'
import { SignInDto, useSignInMutation } from "@/store/api/authApi";
import { localeStorageConst } from "@/lib/constants";
import { useLocalStorage } from "@/hooks/useLocalStorage";

const formSchema = z.object({
  username: z.string().email(),
  password: z.string().min(6)
})

export function LoginForm() {
  useI18nZodErrors()
  const t = useTranslations()

  const form = useForm<z.infer<typeof formSchema>>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      email: '',
      password: ''
    },
  })

  const [signIn, isLoading] = useSignInMutation()
  const [, setSignInInfo] = useLocalStorage<SignInDto>(localeStorageConst.signIn)

  const onSubmit = async (values: z.infer<typeof formSchema>) => {
    let res = await signIn(values)
    // if ('error' in res) {
    //   alert(JSON.stringify(res.error))
    // }
    if ('data' in res) {
      setSignInInfo(res.data)
      router.push('/')
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8">
        <FormField
          control={form.control}
          name="email"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Email')}</FormLabel>
              <FormControl>
                <Input placeholder="m@example.com" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <FormField
          control={form.control}
          name="password"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{t('Password')}</FormLabel>
              <FormControl>
                <Input type="password" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
        <Button type="submit" className="w-full">{t('Signin')}</Button>
      </form>
    </Form>
  )
}
