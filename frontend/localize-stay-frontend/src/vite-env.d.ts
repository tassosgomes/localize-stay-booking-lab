/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_CATALOG_URL: string;
  readonly VITE_BOOKING_URL: string;
  readonly VITE_PAYMENT_URL: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
