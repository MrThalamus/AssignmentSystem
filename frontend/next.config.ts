import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // The standalone bundle exists for the Docker image, which copies
  // .next/standalone instead of the whole node_modules tree. It is redundant on a
  // managed Next.js host, so it is switched on only by the Dockerfile.
  output: process.env.DOCKER_BUILD ? "standalone" : undefined,
};

export default nextConfig;
