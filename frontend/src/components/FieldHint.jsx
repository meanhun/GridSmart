import { Typography } from 'antd';

/** Gợi ý và ví dụ dưới một ô nhập, để người dùng nhập đúng định dạng ngay từ lần đầu. */
export function FieldHint({ children }) {
  return (
    <Typography.Text type="secondary" style={{ display: 'block', fontSize: 12, lineHeight: 1.4 }}>
      {children}
    </Typography.Text>
  );
}

/** Ví dụ có định dạng mã (font đều, dễ sao chép). */
export function Example({ children }) {
  return <Typography.Text code>{children}</Typography.Text>;
}
