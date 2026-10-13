# Đồng Nai GridSmart – Frontend

React 19 + Vite + TypeScript. Ba khu vực trong một ứng dụng:

- `/` cổng công dân (phản ánh, tra cứu, đánh giá)
- `/officer` cổng cán bộ ô lưới
- `/command` trung tâm điều hành và quản trị

## Chạy môi trường dev

1. Chạy backend (mặc định `http://localhost:5282`, profile `http`).
2. `npm install`
3. `npm run dev` rồi mở `http://localhost:5173`.

Vite proxy `/api`, `/hubs`, `/uploads`, `/health` sang backend. Muốn đổi cổng backend:
`VITE_API_PROXY_TARGET=http://localhost:xxxx npm run dev`.

## Sinh client API từ OpenAPI

```
npm run api:gen
```

Lệnh đọc `/openapi/v1.json` của backend đang chạy và ghi vào `src/api/generated/`. Thư mục này do máy sinh ra, không sửa tay.
Sau khi sinh, `scripts/fix-orval-signal.mjs` (hook của orval) sửa một lỗi kiểu của orval 7.13 với tham số `signal`.

## Kiểm thử

- `npx vitest run`: unit test (quyền theo phạm vi, mã lỗi, thời gian, SLA, hình học, lưu trữ cục bộ).
- `npm run typecheck`: kiểm tra kiểu TypeScript.
- `npm run build`: build production.

## Quy ước

- Access token chỉ giữ trong bộ nhớ (`src/api/session.ts`). Refresh token nằm trong cookie httpOnly do backend đặt.
- Mọi lời gọi API đi qua `src/api/mutator/customInstance.ts`: gắn token, tự refresh khi gặp 401 (một lần cho nhiều request), đổi ProblemDetails thành `ApiError` có `code`.
- Chuỗi hiển thị nằm trong `src/locales/vi.ts`. Mã lỗi được ánh xạ trong `src/lib/error-messages.ts`.
- Hiển thị theo quyền: `usePermission`, `<Can>`, `RequirePermission`. Backend vẫn là nơi chặn cuối cùng.
