// Hậu xử lý sau khi orval sinh code (cấu hình hooks.afterAllFilesWrite).
// Bản orval 7.13 truyền AbortSignal thẳng vào tham số RequestInit của hàm query,
// nên TypeScript báo lỗi. Script này bọc signal thành { signal } để đúng kiểu.
// Chỉ đổi dòng queryFn: không đụng logic sinh tự động khác.
import { readdirSync, readFileSync, writeFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../src/api/generated/', import.meta.url));

// getX(params, signal) -> getX(params, { signal })
const withParams = /(\(\{ signal \}\) => \w+\([^()]*?), signal\)/g;
// getX(signal) -> getX({ signal })
const noParams = /(\(\{ signal \}\) => \w+\()signal\)/g;

function walk(dir) {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) walk(full);
    else if (full.endsWith('.ts')) patch(full);
  }
}

function patch(file) {
  const source = readFileSync(file, 'utf8');
  const next = source.replace(withParams, '$1, { signal })').replace(noParams, '$1{ signal })');
  if (next !== source) writeFileSync(file, next, 'utf8');
}

walk(root);
