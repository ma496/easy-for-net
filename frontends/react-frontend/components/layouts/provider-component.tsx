'use client';
import App from '@/App';
import store from '@/store';
import { Provider } from 'react-redux';
import React, { ReactNode, Suspense } from 'react';
import Loading from '@/components/layouts/loading';

interface IProps {
  children?: ReactNode;
}

const ProviderComponent = ({ children }: IProps) => {
  return (
    <Provider store={store}>
      <Suspense fallback={<Loading />}>
        <App>{children} </App>
      </Suspense>
    </Provider>
  );
};

export default ProviderComponent;
