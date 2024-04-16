"use client"

import { zodResolver } from "@hookform/resolvers/zod"
import { useForm } from "react-hook-form"
import { z } from "zod"
import {
  Form,
  FormControl,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Checkbox } from "@/components/ui/checkbox"
import { Input } from "@/components/ui/input"
import { SignInDto, useSignInMutation } from "@/redux/api/authApi"
import { useLocalStorage } from "@/lib/hooks"
import { constants } from "@/lib/constants"
import { useRouter } from "next/navigation"

const FormSchema = z.object({
  username: z.string().min(1, "Username is required."),
  password: z.string().min(6, "Password must be at least 6 characters."),
  remember: z.boolean()
})

// const FormSchema = z.object({
//   username: z.string(),
//   password: z.string(),
//   remember: z.boolean()
// })

export default function SignIn() {
  const form = useForm<z.infer<typeof FormSchema>>({
    resolver: zodResolver(FormSchema),
    defaultValues: {
      username: "",
      password: "",
      remember: false
    },
  })

  const [signIn, { isLoading: signInWaiting }] = useSignInMutation()
  const [, setSignInInfo] = useLocalStorage<SignInDto>(constants.localStorage.signIn)
  const router = useRouter()

  async function onSubmit(data: z.infer<typeof FormSchema>) {
    let res = await signIn(data)
    // if ('error' in res) {
    //   alert(JSON.stringify(res.error))
    // }
    if ('data' in res) {
      setSignInInfo(res.data)
      router.push('/')
    }
  }

  return (
    <div className="relative flex flex-col justify-center items-center min-h-screen overflow-hidden bg-secondary-background">
      <div className="w-full m-auto bg-white lg:max-w-lg">
        <Card>
          <CardHeader className="space-y-1">
            <CardTitle className="text-2xl text-center">Sign in</CardTitle>
            <CardDescription className="text-center">
              Enter your username and password to sign-in
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4">
            <Form {...form}>
              <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
                <FormField
                  control={form.control}
                  name="username"
                  render={({ field }) => (
                    <FormItem>
                      <FormLabel>Username</FormLabel>
                      <FormControl>
                        <Input {...field} />
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
                      <FormLabel>Password</FormLabel>
                      <FormControl>
                        <Input type="password" {...field} />
                      </FormControl>
                      <FormMessage />
                    </FormItem>
                  )}
                />
                <FormField
                  control={form.control}
                  name="remember"
                  render={({ field }) => (
                    <FormItem className="flex flex-row items-start space-x-3 space-y-0">
                      <FormControl>
                        <Checkbox
                          checked={field.value}
                          onCheckedChange={field.onChange}
                        />
                      </FormControl>
                      <div className="space-y-1 leading-none">
                        <FormLabel>
                          Remember me
                        </FormLabel>
                      </div>
                    </FormItem>
                  )}
                />
                <Button type="submit" className="w-full">
                  {signInWaiting ? "Wait..." : "SignIn"}
                </Button>
              </form>
            </Form>
          </CardContent>
        </Card>
      </div>
    </div>
  )
}
