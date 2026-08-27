# Vite инлайнит import.meta.env.VITE_* на этапе сборки,
# поэтому адрес бэка передаётся build-arg'ом и запекается в бандл.
ARG VITE_API_BASE_URL=http://localhost:8080/api

FROM node:24-alpine AS build
ARG VITE_API_BASE_URL
WORKDIR /app

# Зависимости отдельным слоем для кэширования
COPY package.json package-lock.json ./
RUN npm ci

COPY . .
RUN npm run build

FROM nginx:alpine AS final
ARG VITE_API_BASE_URL
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --retries=3 \
  CMD wget -q --spider http://127.0.0.1/ || exit 1
