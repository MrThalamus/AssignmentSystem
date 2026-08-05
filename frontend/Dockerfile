# ---------------------------------------------------------------- deps ---
FROM node:24-alpine AS deps
WORKDIR /app

# Copied on their own so the install layer is reused whenever only source changes.
COPY package.json package-lock.json ./
RUN npm ci

# --------------------------------------------------------------- build ---
FROM node:24-alpine AS build
WORKDIR /app

COPY --from=deps /app/node_modules ./node_modules
COPY . .

# NEXT_PUBLIC_* is inlined into the client bundle at build time, so the API URL
# has to be known here rather than at container start.
ARG NEXT_PUBLIC_API_BASE_URL=http://localhost:5080
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL}
ENV NEXT_TELEMETRY_DISABLED=1

# Switches next.config.ts to the standalone output this image depends on.
ENV DOCKER_BUILD=1

RUN npm run build

# ------------------------------------------------------------- runtime ---
FROM node:24-alpine AS runtime
WORKDIR /app

ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
ENV PORT=3000

RUN addgroup -S nodejs && adduser -S -G nodejs nextjs

# `output: "standalone"` in next.config.ts produces a self-contained server with
# only the packages it actually needs, so node_modules is not copied wholesale.
COPY --from=build --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=build --chown=nextjs:nodejs /app/.next/static ./.next/static
COPY --from=build --chown=nextjs:nodejs /app/public ./public

USER nextjs
EXPOSE 3000

CMD ["node", "server.js"]
