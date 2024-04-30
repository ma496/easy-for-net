'use client';
import IconLockDots from '@/components/icon/icon-lock-dots';
import IconMail from '@/components/icon/icon-mail';
import { useRouter } from 'next/navigation';
import React from 'react';
import * as Yup from 'yup';
import { Field, Form, Formik } from 'formik';
import Swal from 'sweetalert2';

const SignInForm = () => {
  const router = useRouter();
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
  const validations = Yup.object().shape({
    email: Yup.string().required().email(),
    password: Yup.string().required(),
  });


  return (
    <Formik
      initialValues={{
        email: '',
        password: '',
      }}
      validationSchema={validations}
      onSubmit={submitForm}>
      {({ errors, submitCount, touched, values }) => (
        <Form className="space-y-5">
          <div className={submitCount ? (errors.email ? 'has-error' : 'has-success') : ''}>
            <label htmlFor="email">Email</label>
            <div className="relative text-white-dark">
              <Field name="email" type="text" id="email" placeholder="Enter Email" className="form-input ps-10 placeholder:text-white-dark" />
              <span className="absolute start-4 top-1/2 -translate-y-1/2">
                <IconMail fill={true} />
              </span>
            </div>
            {submitCount ? errors.email ? <div className="text-danger mt-1">{errors.email}</div> : <div className="text-success mt-1">Looks Good!</div> : ''}
          </div>
          <div className={submitCount ? (errors.password ? 'has-error' : 'has-success') : ''}>
            <label htmlFor="password">Password</label>
            <div className="relative text-white-dark">
              <Field name="password" type="password" id="password" placeholder="Enter Password" className="form-input ps-10 placeholder:text-white-dark" />
              <span className="absolute start-4 top-1/2 -translate-y-1/2">
                <IconLockDots fill={true} />
              </span>
            </div>
            {submitCount ? errors.password ? <div className="text-danger mt-1">{errors.password}</div> : <div className="text-success mt-1">Looks Good!</div> : ''}
          </div>
          <div>
            <label className="flex cursor-pointer items-center">
              <input type="checkbox" className="form-checkbox bg-white dark:bg-black" />
              <span className="text-white-dark">Remember me</span>
            </label>
          </div>
          <button type="submit" className="btn btn-gradient !mt-6 w-full border-0 uppercase shadow-[0_10px_20px_-10px_rgba(67,97,238,0.44)]">
            Sign in
          </button>
        </Form>
      )}
    </Formik>
  );
};

export default SignInForm;
