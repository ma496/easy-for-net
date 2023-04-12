import {useForm} from '@mantine/form';
import {Anchor, Button, Divider, Group, Paper, PaperProps, PasswordInput, Stack, TextInput,} from '@mantine/core';
import {useNavigate} from "react-router-dom";
import {email, notEmpty, stringLength} from "../../validators";
import {useMutation} from "react-query";
import {apiClient} from "../../apiClientInstance";
import {successNotification} from "../../notifications";
import {useMainStore} from "../../store/mainStore";
import {LoginUserInput} from "../../generatedClient";

export function LoginPage(props: PaperProps) {
  const navigate = useNavigate();
  const setToken = useMainStore(s => s.setToken);
  const setUser = useMainStore(s => s.setUser);

  const mutation = useMutation((values: LoginUserInput) => apiClient.auth.postAuthLogin(values), {
    onSuccess: async (data, values) => {
      successNotification({
        title: "Login user",
        message: "Login user successfully"
      });
      setToken(data.token);
      localStorage.setItem('token', data.token ?? '');
      const user = await apiClient.user.getUserGetByEmail(values.email);
      localStorage.setItem('uid', user.id?.toString() ?? '');
      setUser({
        id: user.id ?? 0,
        name: user.name ?? '',
        email: user.email ?? '',
        username: user.username ?? ''
      });
      navigate('/');
    }
  })

  const form = useForm({
    initialValues: {
      email: '',
      password: '',
    },

    validate: {
      email: v => notEmpty(v) ?? email(v),
      password: v => notEmpty(v) ?? stringLength(v, {min: 6}),
    },
  });

  return (
    <Stack justify={"center"} align={"center"} style={{height:'100vh', backgroundColor:'#F8F9FA'}}>
      <Paper shadow={'xl'} radius="md" p="xl" style={{width:400}} withBorder {...props}>
        <Divider label="continue with email" labelPosition="center" my="lg" />

        <form onSubmit={form.onSubmit((values) => mutation.mutate(values))}>
          <Stack>
            <TextInput
              withAsterisk
              label="Email"
              placeholder="your@email.com"
              {...form.getInputProps('email')}
            />

            <PasswordInput
              withAsterisk
              label="Password"
              placeholder="Your password"
              {...form.getInputProps('password')}
            />
          </Stack>

          <Group position="apart" mt="xl">
            <Anchor
              component="button"
              type="button"
              onClick={() => navigate('/register')}
              color="dimmed"
              size="xs"
            >
              Don't have an account? Register
            </Anchor>
            <Button type="submit" loading={mutation.isLoading}>Login</Button>
          </Group>
        </form>
      </Paper>
    </Stack>
  );
}
