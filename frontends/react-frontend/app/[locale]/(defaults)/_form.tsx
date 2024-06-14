'use client'

import * as Yup from 'yup';
import { Field, Form, Formik } from 'formik';
import Swal from 'sweetalert2';
import Input from '@/components/input';
import IconMail from '@/components/icon/icon-mail';
import IconAirplay from '@/components/icon/icon-airplay';
import Checkbox from '@/components/checkbox';
import Button from '@/components/button';
import SwitchFancy from '@/components/switch-fancy';
import Switch from '@/components/switch';

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
    <div className='w-[500px] mx-auto mt-8'>
      <Formik
        initialValues={{
          address: '',
          test: false,
          subscribe: false,
          notification: false
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
            <SwitchFancy name='subscribe' variant={'secondary'} />
            <p>{values.subscribe ? 'yes' : 'no'}</p>
            <Switch name='notification' variant={'solid'} />
            <p>{values.notification ? 'yes' : 'no'}</p>
            <Button
              type="submit"
              className="btn btn-primary !mt-6"
            >
              Submit Form
            </Button>
          </Form>
        )}
      </Formik>
    </div>
  )
}

export default TestForm
