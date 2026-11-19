#!/bin/sh
set -e

# Default backend URL if not provided by environment
# In Docker Compose: http://vitalq-api:8080
# In Azure Container Apps: https://ca-vitalq-api.politeisland-88851c55.centralindia.azurecontainerapps.io
BACKEND_URL="${BACKEND_URL:-http://vitalq-api:8080}"

echo "Configuring VitalQ Web Nginx with BACKEND_URL=${BACKEND_URL}"

sed -i "s|__BACKEND_URL__|${BACKEND_URL}|g" /etc/nginx/conf.d/default.conf

exec "$@"
