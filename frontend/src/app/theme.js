// Bảng màu thương hiệu: màu chính là xanh lá teal #21A691, màu phụ là nền trắng #FFFFFF.
// Các mã còn lại là màu hỗ trợ cho chữ, viền và nhấn.
export const palette = {
  primary: '#21A691',
  secondary: '#FFFFFF',
  highlight: '#87DF2C',
  deep: '#27403E',
  muted: '#B4B4B2',
  // Không có trong bảng màu thương hiệu: giữ để biểu thị cảnh báo, quá hạn (chữ và biểu tượng vẫn đi kèm).
  danger: '#b91c1c',
  warning: '#b45309',
};

// Chủ đề Ant Design dùng bảng màu trên.
export const antdTheme = {
  token: {
    colorPrimary: palette.primary,
    colorSuccess: palette.primary,
    colorWarning: palette.warning,
    colorError: palette.danger,
    colorInfo: palette.deep,
    colorTextBase: palette.deep,
    colorTextSecondary: palette.deep,
    colorBorder: palette.muted,
    colorBgLayout: palette.secondary,
    borderRadius: 8,
    controlHeight: 40,
    controlHeightLG: 48,
  },
  components: {
    Button: { primaryShadow: 'rgba(33, 166, 145, 0.25) 0 1px 2px' },
  },
};
