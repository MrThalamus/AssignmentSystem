import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Emits a self-contained server bundle under .next/standalone, which is what the
  // Dockerfile copies instead of the whole node_modules tree.
  output: "standalone",
};

export default nextConfig;
