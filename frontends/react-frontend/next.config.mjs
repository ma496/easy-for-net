/**
 * @type {import('next').NextConfig}
 */
const nextConfig = {
  basePath: "/react-frontend",
  async redirects() {
    return [
      {
        source: '/',
        destination: '/react-frontend',
        basePath: false,
        permanent: false
      }
    ]
  },
  images: {
    unoptimized: true,
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'static.justboil.me',
      },
    ],
  },
}

export default nextConfig