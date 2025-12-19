#!/bin/sh
set -e

# Substitute API_URL environment variable in nginx config
if [ -n "$API_URL" ]; then
    sed -i "s|API_URL_PLACEHOLDER|$API_URL|g" /etc/nginx/nginx.conf
else
    # Default to the API container app URL pattern
    sed -i "s|API_URL_PLACEHOLDER|http://clarissabot-api-dev|g" /etc/nginx/nginx.conf
fi

# Substitute API_KEY environment variable in nginx config
if [ -n "$API_KEY" ]; then
    sed -i "s|API_KEY_PLACEHOLDER|$API_KEY|g" /etc/nginx/nginx.conf
else
    # Remove the API key header line if not set
    sed -i "s|proxy_set_header X-API-Key API_KEY_PLACEHOLDER;||g" /etc/nginx/nginx.conf
fi

# Execute the main command
exec "$@"

