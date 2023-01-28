import {useForm} from '@mantine/form';
import {
  Anchor,
  Button,
  Checkbox,
  Divider,
  Group,
  Paper,
  PaperProps,
  PasswordInput,
  Stack,
  TextInput,
} from '@mantine/core';
import {useNavigate} from "react-router-dom";
import {apiClient} from "../../apiClientInstance";
import {email, notEmpty, notFalse, stringLength} from "../../validators";
import {useMutation} from "react-query";
import {successNotification} from "../../notifications";

export function RegisterPage(props: PaperProps) {
  const navigate = useNavigate();
  const mutation = useMutation((values: any) => apiClient.auth.postAuthRegister(values), {
    onSuccess: () => {
      successNotification({
        title: 'Register user',
        message: 'Registered user successfully',
      });
      navigate('/login');
    }
  });

  const form = useForm({
    initialValues: {
      email: '',
      username: '',
      name: '',
      password: '',
      terms: false,
    },

    validate: {
      name: v => notEmpty(v),
      email: v => notEmpty(v) ?? email(v),
      username: v => notEmpty(v),
      password: v => notEmpty(v) ?? stringLength(v, {min: 6}),
      terms: v => notFalse(v, 'Please accept terms and conditions'),
    },
  });

  return (
    <Stack justify={"center"} align={"center"} style={{height:'100vh', backgroundColor:'#F8F9FA'}}>
      <Paper shadow={'xl'} radius="md" p="xl" style={{width:400}} withBorder {...props}>
        <Divider label="continue with email" labelPosition="center" my="lg" />
        <form onSubmit={form.onSubmit(values => mutation.mutate(values))}>
          <Stack>
            <TextInput
              label="Name"
              withAsterisk
              placeholder="Your name"
              {...form.getInputProps('name')}
            />

            <TextInput
              label="Email"
              withAsterisk
              placeholder="your@email.com"
              {...form.getInputProps('email')}
            />

            <TextInput
              label="Username"
              withAsterisk
              placeholder="your username"
              {...form.getInputProps('username')}
            />

            <PasswordInput
              label="Password"
              withAsterisk
              placeholder="Your password"
              {...form.getInputProps('password')}
            />

            <Checkbox
              label="I accept terms and conditions"
              {...form.getInputProps('terms')}
            />
          </Stack>

          <Group position="apart" mt="xl">
            <Anchor
              component="button"
              type="button"
              onClick={() => navigate('/login')}
              color="dimmed"
              size="xs"
            >
              Already have an account? Login
            </Anchor>
            <Button type="submit" loading={mutation.isLoading}>Register</Button>
          </Group>
        </form>
      </Paper>
    </Stack>
  );
}
