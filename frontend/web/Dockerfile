# Multi-stage production build for Heimdall Nuxt 4 Web Frontend & Nitro BFF
FROM oven/bun:1.4 AS build

WORKDIR /app

# Copy dependency specifications
COPY package.json bun.lock* ./

# Install production and build dependencies
RUN bun install --frozen-lockfile || bun install

# Copy application source code
COPY . .

# Generate Nuxt types and build production bundle
RUN bun x nuxt prepare && bun run build

# Production runtime image
FROM oven/bun:1.4-slim AS final

WORKDIR /app

# Create unprivileged runtime user
RUN groupadd -g 10003 webuser && \
    useradd -u 10003 -g webuser -m -s /bin/false webuser

# Copy compiled standalone Nitro bundle
COPY --from=build --chown=webuser:webuser /app/.output ./.output

USER webuser

EXPOSE 3000

ENV NODE_ENV=production
ENV PORT=3000
ENV HOST=0.0.0.0

CMD ["bun", ".output/server/index.mjs"]
