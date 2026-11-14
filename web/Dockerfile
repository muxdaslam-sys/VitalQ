# ==============================================================================
# STAGE 1: Compile Angular 19 SPA
# ==============================================================================
FROM node:20-alpine AS build
WORKDIR /app

# Copy package definition files
COPY package*.json ./

# Install dependencies cleanly
RUN npm ci

# Copy Angular source code
COPY . .

# Build production bundle
RUN npm run build -- --configuration=production

# ==============================================================================
# STAGE 2: Ultra-Fast Nginx Production Web Server (< 25MB)
# ==============================================================================
FROM nginx:alpine AS final

# Copy custom Nginx configuration
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Copy compiled static assets from build stage
COPY --from=build /app/dist/vitalq-web/browser /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
