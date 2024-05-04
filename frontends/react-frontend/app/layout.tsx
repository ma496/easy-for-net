import ProviderComponent from '@/components/layouts/provider-component';
import 'react-perfect-scrollbar/dist/css/styles.css';
import '../styles/tailwind.css';
import { Metadata } from 'next';
import { Nunito } from 'next/font/google';
import React from 'react';
import { NextIntlClientProvider, useMessages } from 'next-intl';

export const metadata: Metadata = {
  title: {
    template: '%s | EasyForNet - Full Stack Template',
    default: 'EasyForNet - Full Stack Template',
  },
};
const nunito = Nunito({
  weight: ['400', '500', '600', '700', '800'],
  subsets: ['latin'],
  display: 'swap',
  variable: '--font-nunito',
});

type Props = {
  children: React.ReactNode
  params: {
    locale: "en" | "ur"
  }
}

export default function RootLayout({ children, params: {locale} }: Props) {
  const messages = useMessages();

  return (
    <html lang={locale}>
      <body className={nunito.variable}>
        <NextIntlClientProvider messages={messages}>
          <ProviderComponent>{children}</ProviderComponent>
        </NextIntlClientProvider>
      </body>
    </html>
  );
}
