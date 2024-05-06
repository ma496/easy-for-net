'use client';
import IconLockDots from '@/components/icon/icon-lock-dots';
import IconMail from '@/components/icon/icon-mail';
import { useRouter } from 'next/navigation';
import React from 'react';
import * as Yup from 'yup';
import { Form, Formik } from 'formik';
import Swal from 'sweetalert2';
import { setValidationTranslations } from '@/utils/validationUtils';
import { useTranslations } from 'next-intl';
import Input from '@/components/input';
import Button from '@/components/button';
import Checkbox from '@/components/checkbox';

const SignInForm = () => {
  const router = useRouter()
  const t = useTranslations()
  setValidationTranslations(t)
  const submitForm = () => {
    const toast = Swal.mixin({
      toast: true,
      position: 'top',
      showConfirmButton: false,
      timer: 3000,
    });
    toast.fire({
      icon: 'success',
      title: 'Form submitted successfully',
      padding: '10px 20px',
    });
  };
  const schema = Yup.object().shape({
    email: Yup.string().required().email(),
    password: Yup.string().required(),
  });

  return (
    <Formik
      initialValues={{
        email: '',
        password: '',
        rememberMe: false
      }}
      validationSchema={schema}
      onSubmit={submitForm}>
      {() => (
        <Form className="space-y-5" noValidate={true}>
          <Input
            name='email'
            type='email'
            label={t('email')}
            placeholder={t('enter_email')}
            prefixElm={<IconMail fill={true} />}
          />
          <Input
            name='password'
            type='password'
            label={t('password')}
            placeholder={t('enter_password')}
            prefixElm={<IconLockDots fill={true} />}
          />
          <Checkbox
            name='rememberMe'
            label={t('remember_me')}
          />
          <Button type="submit" className="btn btn-gradient !mt-6 w-full border-0 uppercase shadow-[0_10px_20px_-10px_rgba(67,97,238,0.44)]">
            {t('sign_in')}
          </Button>
        </Form>
      )}
    </Formik>
  );
};

export default SignInForm;
