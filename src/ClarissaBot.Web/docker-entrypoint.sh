#!/bin/sh
set -e

# Substitute API_URL environment variable in nginx config
if [ -n "$API_URL" ]; then
    sed -i "s|API_URL_PLACEHOLDER|$API_URL|g" /etc/nginx/nginx.conf
else
    # Default to the API container app URL pattern
    sed -i "s|API_URL_PLACEHOLDER|http://clarissabot-api-dev|g" /etc/nginx/nginx.conf
fi

# Execute the main command
exec "$@"

