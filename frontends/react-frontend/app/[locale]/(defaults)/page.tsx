import Button from '@/components/button';
import IconAirplay from '@/components/icon/icon-airplay';
import IconSettings from '@/components/icon/icon-settings';
import { Metadata } from 'next';
import React from 'react';

export const metadata: Metadata = {
  title: 'Home',
};

const Home = () => {
  return (
    <div className='flex gap-4'>
      <Button>Test</Button>
      <Button prefixElm={<IconSettings />}>Test</Button>
      <Button variant={'secondary_outline'}>Test</Button>
      <Button variant={'secondary'} size={'lg'} prefixElm={<IconAirplay />}>Test</Button>
      <Button prefixElm={<IconSettings />} size={"sm"} rounded={'yes'} />
    </div>
  )
};

export default Home
