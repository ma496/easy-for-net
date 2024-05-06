import { Metadata } from 'next';
import React from 'react';
import TestForm from './_form'

export const metadata: Metadata = {
  title: 'Home',
};

const Home = () => {
  return (
    <TestForm />
  )
};

export default Home
