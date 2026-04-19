#!/bin/bash

echo "Starting Vite dev server..."
npm run dev -- --host 0.0.0.0 --port ${VITE_PORT:-8080}
