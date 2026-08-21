/// <reference types="vite/client" />

declare global {
  interface Window {
    __CES_CONFIG__?: {
      VITE_DEV_AUTH_BYPASS?: string;
    };
  }
}

export {};
