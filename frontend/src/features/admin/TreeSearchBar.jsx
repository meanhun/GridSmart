import { Button, Flex, Input, Typography } from 'antd';
import { NodeCollapseOutlined, NodeExpandOutlined, SearchOutlined } from '@ant-design/icons';
/** Ô tìm kiếm và nút mở rộng/thu gọn dùng chung cho cây đơn vị hành chính và bảng ô lưới. */
export function TreeSearchBar({ query, onQueryChange, placeholder, label, summary, onExpandAll, onCollapseAll }) {
  return (
    <Flex vertical gap={6}>
      <Input
        allowClear
        value={query}
        prefix={<SearchOutlined aria-hidden="true" />}
        placeholder={placeholder}
        aria-label={label}
        onChange={(e) => onQueryChange(e.target.value)}
      />
      <Flex wrap gap={4} align="center" justify="space-between">
        <Typography.Text type="secondary" style={{ fontSize: 12 }} role="status" aria-live="polite">
          {summary}
        </Typography.Text>
        <Flex gap={4}>
          <Button size="small" type="text" icon={<NodeExpandOutlined aria-hidden="true" />} onClick={onExpandAll}>
            Mở rộng tất cả
          </Button>
          <Button size="small" type="text" icon={<NodeCollapseOutlined aria-hidden="true" />} onClick={onCollapseAll}>
            Thu gọn tất cả
          </Button>
        </Flex>
      </Flex>
    </Flex>
  );
}
