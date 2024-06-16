'use client';
import { store } from '@/store';
import { Provider } from 'react-redux';
import React from 'react';

interface IProps {
  children: React.ReactNode
}

const StoreProvider = ({ children }: IProps) => {
  return (
    <Provider store={store}>
      {children}
    </Provider>
  )
}

export default StoreProvider
