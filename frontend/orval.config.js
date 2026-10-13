import { defineConfig } from 'orval';
// Nguồn: OpenAPI của backend đang chạy (dev) hoặc file đã lưu khi build.
const input = process.env.OPENAPI_INPUT ?? 'http://localhost:5282/openapi/v1.json';
export default defineConfig({
  gridsmart: {
    input: { target: input },
    output: {
      mode: 'tags-split',
      target: 'src/api/generated/endpoints',
      schemas: 'src/api/generated/model',
      client: 'react-query',
      httpClient: 'fetch',
      clean: true,
      prettier: false,
      override: {
        mutator: {
          path: 'src/api/mutator/customInstance.js',
          name: 'customInstance',
        },
      },
    },
    hooks: {
      afterAllFilesWrite: 'node scripts/fix-orval-signal.mjs',
    },
  },
});
