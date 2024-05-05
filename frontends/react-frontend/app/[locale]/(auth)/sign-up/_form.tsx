'use client';
import IconLockDots from '@/components/icon/icon-lock-dots';
import IconMail from '@/components/icon/icon-mail';
import { useRouter } from 'next/navigation';
import React from 'react';
import * as Yup from 'yup';
import { Field, Form, Formik } from 'formik';
import Swal from 'sweetalert2';
import { setValidationTranslations } from '@/utils/validationUtils';
import { useTranslations } from 'next-intl';
import IconUser from '@/components/icon/icon-user';

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
    name: Yup.string().required(),
    email: Yup.string().required().email(),
    password: Yup.string().required(),
  });

  return (
    <Formik
      initialValues={{
        name: '',
        email: '',
        password: '',
      }}
      validationSchema={schema}
      onSubmit={submitForm}>
      {({ errors, submitCount, touched, values }) => (
        <Form className="space-y-5">
          <div className={submitCount ? (errors.name ? 'has-error' : 'has-success') : ''}>
            <label htmlFor="name">{t('name')}</label>
            <div className="relative text-white-dark">
              <Field name="name" type="text" id="name" placeholder={t('enter_name')} className="form-input ps-10 placeholder:text-white-dark" />
              <span className="absolute start-4 top-1/2 -translate-y-1/2">
                <IconUser fill={true} />
              </span>
            </div>
            {submitCount ? errors.name ? <div className="text-danger mt-1">{errors.name}</div> : <div className="text-success mt-1">{t('looks_good')}</div> : ''}
          </div>
          <div className={submitCount ? (errors.email ? 'has-error' : 'has-success') : ''}>
            <label htmlFor="email">{t('email')}</label>
            <div className="relative text-white-dark">
              <Field name="email" type="text" id="email" placeholder={t('enter_email')} className="form-input ps-10 placeholder:text-white-dark" />
              <span className="absolute start-4 top-1/2 -translate-y-1/2">
                <IconMail fill={true} />
              </span>
            </div>
            {submitCount ? errors.email ? <div className="text-danger mt-1">{errors.email}</div> : <div className="text-success mt-1">{t('looks_good')}</div> : ''}
          </div>
          <div className={submitCount ? (errors.password ? 'has-error' : 'has-success') : ''}>
            <label htmlFor="password">{t('password')}</label>
            <div className="relative text-white-dark">
              <Field name="password" type="password" id="password" placeholder={t('enter_password')} className="form-input ps-10 placeholder:text-white-dark" />
              <span className="absolute start-4 top-1/2 -translate-y-1/2">
                <IconLockDots fill={true} />
              </span>
            </div>
            {submitCount ? errors.password ? <div className="text-danger mt-1">{errors.password}</div> : <div className="text-success mt-1">{t('looks_good')}</div> : ''}
          </div>
          <div>
            <label className="flex cursor-pointer items-center">
              <input type="checkbox" className="form-checkbox bg-white dark:bg-black" />
              <span className="text-white-dark">{t('subscribe_weekly_news_letter')}</span>
            </label>
          </div>
          <button type="submit" className="btn btn-gradient !mt-6 w-full border-0 uppercase shadow-[0_10px_20px_-10px_rgba(67,97,238,0.44)]">
            {t('sign_up')}
          </button>
        </Form>
      )}
    </Formik>
  );
};

export default SignInForm;
