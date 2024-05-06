'use client'

import * as Yup from 'yup';
import { Field, Form, Formik } from 'formik';
import Swal from 'sweetalert2';
import Input from '@/components/input';
import IconMail from '@/components/icon/icon-mail';
import IconAirplay from '@/components/icon/icon-airplay';
import Checkbox from '@/components/checkbox';

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

const SubmittedForm = Yup.object().shape({
  address: Yup.string().required()
});

const TestForm = () => {
  return (
    <Formik
      initialValues={{
        address: '',
        test: false
      }}
      validationSchema={SubmittedForm}
      onSubmit={(v) => { alert(JSON.stringify(v)) }}
    >
      {({ values }) => (
        <Form className="space-y-5">
          <Input
            name='address'
            label='Address'
            placeholder='Enter Address'
            prefixElm={<IconMail fill={true} />}
            postfixElm={<IconAirplay fill={true} />}
          />
          <p>{values.address}</p>
          <Checkbox
            name='test'
            label='test'
            variant={'secondary_outline'}
          />
          <p>{values.test ? 'yes' : 'no'}</p>
          <button
            type="submit"
            className="btn btn-primary !mt-6"
          >
            Submit Form
          </button>
        </Form>
      )}
    </Formik>
  )
}

export default TestForm
