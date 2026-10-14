import { zodResolver } from '@hookform/resolvers/zod';
import { Button, Card, Form, Input, Typography } from 'antd';
import { Controller, useForm } from 'react-hook-form';
import { useNavigate } from 'react-router-dom';
import { z } from 'zod';

/** III.1.1 Sign In. Gọi API đăng nhập ở task "Đăng nhập" của Sprint 0. */
const signInSchema = z.object({
  email: z.string().min(1, 'The * field is required.').email('The email is not in the correct format.'),
  password: z.string().min(1, 'The * field is required.'),
});

type SignInValues = z.infer<typeof signInSchema>;

export function SignInPage() {
  const navigate = useNavigate();
  const {
    control,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<SignInValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { email: '', password: '' },
  });

  const onSubmit = async (values: SignInValues) => {
    // TODO(Sprint 0 – Đăng nhập): POST /api/auth/login, lưu token, điều hướng theo vai trò.
    console.info('sign in', values.email);
    navigate('/');
  };

  return (
    <div style={{ minHeight: '100vh', display: 'grid', placeItems: 'center', background: '#f5f7fa' }}>
      <Card style={{ width: 380 }}>
        <Typography.Title level={3}>Đăng nhập ConTrack</Typography.Title>
        <Form layout="vertical" onFinish={handleSubmit(onSubmit)}>
          <Form.Item label="Email" validateStatus={errors.email ? 'error' : ''} help={errors.email?.message}>
            <Controller name="email" control={control} render={({ field }) => <Input {...field} autoComplete="email" />} />
          </Form.Item>
          <Form.Item label="Mật khẩu" validateStatus={errors.password ? 'error' : ''} help={errors.password?.message}>
            <Controller
              name="password"
              control={control}
              render={({ field }) => <Input.Password {...field} autoComplete="current-password" />}
            />
          </Form.Item>
          <Button type="primary" htmlType="submit" block loading={isSubmitting}>
            Đăng nhập
          </Button>
        </Form>
      </Card>
    </div>
  );
}
