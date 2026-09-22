#!/bin/sh

API_BASE_URL="${VITE_API_BASE_URL:-http://localhost:8080/api}"

cat > /usr/share/nginx/html/config.json <<EOF
{
  "API_BASE_URL": "$API_BASE_URL"
}
EOF

exec nginx -g "daemon off;"
