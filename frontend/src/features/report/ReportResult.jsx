import { useEffect, useState } from 'react';
import { Link } from 'react-router';
import { Button, Card, Col, Flex, Row, Steps, Typography } from 'antd';
import { CheckOutlined, CopyOutlined, DownloadOutlined, SearchOutlined, ShareAltOutlined } from '@ant-design/icons';
import { formatDateTime } from '@/lib/datetime';
/** Kết quả sau khi gửi: mã tra cứu lớn, sao chép, chia sẻ, theo dõi (FE-08 bước 7). */
export function ReportResult({ report, onNewReport }) {
  const [copied, setCopied] = useState(false);
  const [installEvent, setInstallEvent] = useState(null);
  // Gợi ý "Thêm vào màn hình chính" sau lần gửi đầu tiên (FE-11).
  useEffect(() => {
    const onBeforeInstall = (e) => {
      e.preventDefault();
      setInstallEvent(e);
    };
    window.addEventListener('beforeinstallprompt', onBeforeInstall);
    return () => window.removeEventListener('beforeinstallprompt', onBeforeInstall);
  }, []);
  const canShare = typeof navigator !== 'undefined' && typeof navigator.share === 'function';
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(report.trackingCode);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2500);
    } catch {
      setCopied(false);
    }
  };
  const share = async () => {
    try {
      await navigator.share({
        title: 'Phản ánh Đồng Nai GridSmart',
        text: `Mã tra cứu phản ánh của tôi: ${report.trackingCode}`,
      });
    } catch {
      // Người dùng hủy chia sẻ hoặc trình duyệt từ chối: không cần báo lỗi.
    }
  };
  return (
    <Flex vertical gap={24}>
      <Steps
        size="small"
        current={3}
        items={[{ title: 'Chụp ảnh' }, { title: 'Xác nhận' }, { title: 'Gửi đi' }]}
      />

      <section aria-labelledby="result-title" style={{ textAlign: 'center' }}>
        <Flex vertical align="center" gap={12}>
          <Flex
            align="center"
            justify="center"
            style={{ width: 64, height: 64, borderRadius: '50%', background: 'rgba(33, 166, 145, 0.15)', color: '#21A691' }}
          >
            <CheckOutlined style={{ fontSize: 36 }} aria-hidden="true" />
          </Flex>
          <Typography.Title level={2} id="result-title" style={{ margin: 0 }}>
            Đã gửi phản ánh thành công
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ maxWidth: 384, margin: 0 }}>
            Cảm ơn bạn. Hãy lưu mã bên dưới để theo dõi tiến độ xử lý.
          </Typography.Paragraph>
        </Flex>
      </section>

      <Card styles={{ body: { textAlign: 'center' } }}>
        <Flex vertical gap={8}>
          <Typography.Text type="secondary">Mã tra cứu của bạn</Typography.Text>
          <Typography.Text
            strong
            data-testid="tracking-code"
            style={{ fontFamily: 'monospace', fontSize: 30, letterSpacing: '0.05em', wordBreak: 'break-all' }}
          >
            {report.trackingCode}
          </Typography.Text>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Gửi lúc {formatDateTime(report.createdAt)}
          </Typography.Text>
          <Row gutter={12} style={{ paddingTop: 8 }}>
            <Col xs={canShare ? 12 : 24} sm={canShare ? 12 : 24}>
              <Button block onClick={() => void copy()}>
                {copied ? <CheckOutlined aria-hidden="true" /> : <CopyOutlined aria-hidden="true" />}
                {copied ? 'Đã sao chép' : 'Sao chép mã'}
              </Button>
            </Col>
            {canShare && (
              <Col xs={12} sm={12}>
                <Button block onClick={() => void share()}>
                  <ShareAltOutlined aria-hidden="true" /> Chia sẻ
                </Button>
              </Col>
            )}
          </Row>
        </Flex>
      </Card>

      <Link to={`/track/${encodeURIComponent(report.trackingCode)}`}>
        <Button type="primary" size="large" block>
          <SearchOutlined aria-hidden="true" /> Theo dõi tiến độ
        </Button>
      </Link>

      {installEvent && (
        <Button
          block
          onClick={() => {
            void installEvent.prompt();
            setInstallEvent(null);
          }}
        >
          <DownloadOutlined aria-hidden="true" /> Thêm vào màn hình chính
        </Button>
      )}

      <div style={{ textAlign: 'center' }}>
        <Button type="link" onClick={onNewReport}>
          Gửi thêm một phản ánh khác
        </Button>
      </div>
    </Flex>
  );
}
