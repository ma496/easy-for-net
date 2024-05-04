import createMiddleware from 'next-intl/middleware';
import { NextRequest, NextResponse } from 'next/server';

const nextIntelMiddleware = createMiddleware({
  // A list of all locales that are supported
  locales: ['en', 'ur'],

  // Used when no locale matches
  defaultLocale: 'en'
});

export default function (req: NextRequest): NextResponse {
  return nextIntelMiddleware(req);
}

export const config = {
  // Match only internationalized pathnames
  matcher: ['/', '/(ur|en)/:path*']
};
