import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  // A self-contained server bundle for the Docker image.
  output: 'standalone',
  poweredByHeader: false,
};

export default nextConfig;
