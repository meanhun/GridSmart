/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url';
import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';
// FE-01: proxy /api, /hubs, /uploads, /health sang backend để trình duyệt thấy cùng origin.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  const target = env.VITE_API_PROXY_TARGET ?? 'http://localhost:5282';
  const proxy = { target, changeOrigin: false, secure: false };
  // Thẻ OG trong index.html cần URL tuyệt đối để Facebook/Zalo tải được ảnh xem trước.
  // Bản triển khai đặt VITE_SITE_URL (ví dụ https://gridsmart.dongnai.gov.vn); để trống thì dùng đường dẫn tương đối.
  const siteUrl = (env.VITE_SITE_URL ?? '').replace(/\/+$/, '');
  return {
    plugins: [
      react(),
      { name: 'site-url', transformIndexHtml: (html) => html.replaceAll('__SITE_URL__', siteUrl) },
      // FE-11: PWA. Chỉ cache vỏ ứng dụng và danh mục công khai. Không cache dữ liệu sự cố hay API cá nhân.
      VitePWA({
        registerType: 'autoUpdate',
        includeAssets: ['icon.svg', 'favicon.svg', 'apple-touch-icon.png'],
        manifest: {
          name: 'Đồng Nai GridSmart',
          short_name: 'GridSmart',
          description: 'Phản ánh hiện trường và tra cứu tiến độ xử lý.',
          // Màu chính của thương hiệu (src/app/theme.js), khớp meta theme-color trong index.html
          theme_color: '#21A691',
          background_color: '#f8fafc',
          display: 'standalone',
          start_url: '/',
          lang: 'vi',
          icons: [{ src: 'icon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' }],
        },
        workbox: {
          globPatterns: ['**/*.{js,css,html,svg}'],
          navigateFallback: '/index.html',
          navigateFallbackDenylist: [/^\/api\//, /^\/hubs\//, /^\/uploads\//, /^\/health/],
          runtimeCaching: [
            {
              urlPattern: /\/api\/public\/categories/,
              handler: 'StaleWhileRevalidate',
              options: { cacheName: 'public-categories', expiration: { maxEntries: 1, maxAgeSeconds: 86_400 } },
            },
          ],
        },
        devOptions: { enabled: false },
      }),
    ],
    resolve: {
      alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
    },
    build: {
      // Tách thư viện dùng chung thành chunk riêng để trình duyệt cache tốt hơn (FE-26).
      rolldownOptions: {
        output: {
          manualChunks(id) {
            if (!id.includes('node_modules')) return undefined;
            if (/[\\/]node_modules[\\/](react|react-dom|react-router|scheduler)[\\/]/.test(id)) return 'vendor-react';
            if (/[\\/]node_modules[\\/](@microsoft[\\/]signalr|@tanstack)[\\/]/.test(id)) return 'vendor-data';
            if (/[\\/]node_modules[\\/](leaflet|react-leaflet|leaflet\.heat|@geoman-io)[\\/]/.test(id)) return 'vendor-map';
            if (/[\\/]node_modules[\\/](recharts|d3-|victory-vendor)/.test(id)) return 'vendor-charts';
            return undefined;
          },
        },
      },
    },
    server: {
      port: 5173,
      proxy: {
        '/api': proxy,
        '/hubs': { ...proxy, ws: true },
        '/uploads': proxy,
        '/health': proxy,
      },
    },
    test: {
      environment: 'jsdom',
      globals: false,
      setupFiles: ['./src/test/setup.js'],
      include: ['src/**/*.test.{js,jsx}'],
    },
  };
});
