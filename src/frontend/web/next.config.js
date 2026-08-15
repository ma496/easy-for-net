/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  
  // Enable Typed Routes (stable in Next.js 15.5)
  typedRoutes: true,

  async rewrites() {
    return [{
      source: '/api/:path*',
      destination: 'http://localhost:5000/api/:path*',
    }]
  },
  
  experimental: {
    // Optional: Enable Turbopack for development (beta)
    // turbo: {
    //   rules: {
    //     // Custom Turbopack rules if needed
    //   }
    // }
  },
};

module.exports = nextConfig;
