import { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Flex, Form, Input, Select, Spin, Steps, Tag, Typography } from 'antd';
import { BulbOutlined, CameraOutlined, CheckOutlined, EnvironmentOutlined, HeartOutlined, PictureOutlined, ReloadOutlined, SendOutlined, ThunderboltOutlined } from '@ant-design/icons';
import { Marker, useMapEvents } from 'react-leaflet';
import { getApiPublicLocate, useGetApiPublicCategories } from '@/api/generated/endpoints/public/public';
import { postApiPublicClassifyPreview, postApiPublicIncidents } from '@/api/generated/endpoints/public/public';
import { BaseMap, BIEN_HOA_CENTER, pinIcon } from '@/components/map/BaseMap';
import { useAuth } from '@/features/auth/useAuth';
import { errorMessage } from '@/lib/error-messages';
import { readGpsFromExif } from '@/lib/exif';
import { getCurrentFix } from '@/lib/geo';
import { compressForUpload } from '@/lib/image';
import { saveLocalReport } from '@/lib/local-reports';
import { ReportResult } from './ReportResult';
const PHONE_PATTERN = /^\+?[0-9]{9,15}$/;
const DESCRIPTION_MAX = 1000;
const STEPS = ['Chụp ảnh', 'Xác nhận', 'Gửi đi'];
const STEP_ITEMS = STEPS.map((title) => ({ title }));
/**
 * Gửi phản ánh một chạm (FE-08). Luồng: chụp ảnh → xác nhận vị trí → gửi.
 * Vị trí được lấy ngay khi vào màn hình, không chờ tới khi chọn ảnh.
 */
export function ReportWizard() {
  const { user } = useAuth();
  const [step, setStep] = useState('photo');
  const [photo, setPhoto] = useState(null);
  const [position, setPosition] = useState(null);
  const [needsPin, setNeedsPin] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [categoryId, setCategoryId] = useState('');
  const [description, setDescription] = useState('');
  const [phone, setPhone] = useState(user?.phone ?? '');
  const [error, setError] = useState(null);
  const [result, setResult] = useState(null);
  const fixRef = useRef(null);
  const categories = useGetApiPublicCategories();
  // Bắt đầu lấy GPS ngay khi người dùng vào màn hình gửi phản ánh.
  useEffect(() => {
    fixRef.current = getCurrentFix().catch(() => null);
  }, []);
  useEffect(() => {
    if (user?.phone && phone === '') setPhone(user.phone);
    // Chỉ điền một lần khi nạp người dùng.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user?.phone]);
  const onPhotoSelected = async (file) => {
    setError(null);
    setPhoto(file);
    setSuggestions([]);
    setCategoryId('');
    setStep('locating');
    void previewCategories(file, setSuggestions);
    const fix = fixRef.current ? await fixRef.current : null;
    let next = fix ? { latitude: fix.latitude, longitude: fix.longitude, source: 'gps' } : null;
    if (!next) {
      const exif = await readGpsFromExif(file);
      if (exif) next = { ...exif, source: 'exif' };
    }
    if (!next) {
      next = { latitude: BIEN_HOA_CENTER[0], longitude: BIEN_HOA_CENTER[1], source: 'manual' };
    }
    setNeedsPin(next.source === 'manual');
    setPosition(next);
    setStep('confirm');
  };
  const onSubmit = async () => {
    if (!photo || !position) return;
    const trimmedPhone = phone.trim();
    if (!PHONE_PATTERN.test(trimmedPhone)) {
      setError('Số điện thoại chưa đúng định dạng: chỉ gồm 9 đến 15 chữ số, ví dụ 0912345678.');
      return;
    }
    if (needsPin) {
      setError('Chưa xác định được vị trí. Hãy chạm lên bản đồ để đánh dấu đúng điểm sự cố.');
      return;
    }
    setError(null);
    setStep('sending');
    let image;
    try {
      image = await compressForUpload(photo);
    } catch {
      setError('Không đọc được ảnh này. Hãy chụp lại hoặc chọn ảnh JPEG/PNG khác.');
      setStep('confirm');
      return;
    }
    try {
      const res = await postApiPublicIncidents({
        Image: image,
        Latitude: position.latitude,
        Longitude: position.longitude,
        Description: description.trim() || undefined,
        CategoryId: categoryId ? Number(categoryId) : undefined,
        ReporterPhone: trimmedPhone,
        AddressText: addressText || undefined,
      });
      const trackingCode = res.data.trackingCode;
      const categoryName = categories.data?.data.find((c) => String(c.id) === categoryId)?.name;
      const report = {
        trackingCode,
        phone: trimmedPhone,
        createdAt: new Date().toISOString(),
        categoryName,
        addressText: addressText || undefined,
      };
      saveLocalReport(report);
      setResult(report);
      setStep('done');
    } catch (err) {
      setError(errorMessage(err));
      setStep('confirm');
    }
  };
  const reset = () => {
    setStep('photo');
    setPhoto(null);
    setPosition(null);
    setDescription('');
    setCategoryId('');
    setError(null);
    setResult(null);
    fixRef.current = getCurrentFix().catch(() => null);
  };
  const retake = () => {
    setPhoto(null);
    setPosition(null);
    setSuggestions([]);
    setError(null);
    setStep('photo');
  };
  const locate = useQuery({
    queryKey: ['public-locate', position?.latitude, position?.longitude],
    queryFn: async () => (await getApiPublicLocate({ lat: position.latitude, lon: position.longitude })).data,
    enabled: position !== null && step !== 'done',
    staleTime: 60_000,
  });
  const addressText = locate.data
    ? [locate.data.hamletName, locate.data.communeName, locate.data.provinceName].filter(Boolean).join(', ')
    : '';
  if (step === 'done' && result) {
    return <ReportResult report={result} onNewReport={reset} />;
  }
  if (step === 'photo') {
    return <PhotoStep onSelect={(f) => void onPhotoSelected(f)} />;
  }
  if (step === 'locating') {
    return (
      <Flex vertical gap={20}>
        <Steps size="small" current={0} items={STEP_ITEMS} />
        <Card styles={{ body: { textAlign: 'center', padding: '40px 16px' } }}>
          <Flex vertical align="center" gap={12}>
            <Spin size="large" />
            <Typography.Text strong role="status" style={{ fontSize: 18 }}>
              Đang xác định vị trí…
            </Typography.Text>
            <Typography.Text type="secondary" style={{ maxWidth: 320 }}>
              Nếu trình duyệt hỏi quyền truy cập vị trí, hãy chọn “Cho phép” để việc gửi nhanh hơn.
            </Typography.Text>
          </Flex>
        </Card>
      </Flex>
    );
  }
  if (step === 'sending') {
    return (
      <Flex vertical gap={20}>
        <Steps size="small" current={2} items={STEP_ITEMS} />
        <Card styles={{ body: { textAlign: 'center', padding: '40px 16px' } }}>
          <Flex vertical align="center" gap={12}>
            <Spin size="large" />
            <Typography.Text strong role="status" style={{ fontSize: 18 }}>
              Đang gửi phản ánh…
            </Typography.Text>
            <Typography.Text type="secondary" style={{ maxWidth: 320 }}>
              Ảnh đang được nén và gửi lên hệ thống. Vui lòng giữ màn hình mở.
            </Typography.Text>
          </Flex>
        </Card>
      </Flex>
    );
  }
  const categoryOptions = categories.data?.data.filter((c) => c.isActive) ?? [];
  const sourceLabel = {
    gps: { text: 'Vị trí từ GPS điện thoại', color: 'success' },
    exif: { text: 'Vị trí lấy từ ảnh', color: 'processing' },
    manual: { text: 'Cần chạm bản đồ để chọn vị trí', color: 'warning' },
  }[position?.source ?? 'manual'];
  const phoneError = error && error.includes('điện thoại') ? error : null;
  return (
    <Flex vertical gap={20}>
      <Steps size="small" current={1} items={STEP_ITEMS} />

      <header>
        <Typography.Title level={2} id="confirm-title" style={{ margin: 0 }}>
          Xác nhận phản ánh
        </Typography.Title>
        <Typography.Text type="secondary">Kiểm tra vị trí và thông tin rồi bấm gửi.</Typography.Text>
      </header>

      <Card size="small">
        <Flex vertical gap={12}>
          {photo && <PhotoPreview file={photo} />}
          <Flex align="center" justify="space-between" gap={12}>
            <Typography.Text type="secondary">Ảnh đã chụp</Typography.Text>
            <Button type="text" size="small" onClick={retake}>
              <ReloadOutlined aria-hidden="true" /> Đổi ảnh
            </Button>
          </Flex>
        </Flex>
      </Card>

      {position && (
        <Card
          title={
            <Flex align="center" gap={8}>
              <EnvironmentOutlined aria-hidden="true" /> Vị trí sự cố
            </Flex>
          }
          extra={<Tag color={sourceLabel.color}>{sourceLabel.text}</Tag>}
        >
          <Flex vertical gap={12}>
            <BaseMap label="Bản đồ xác nhận vị trí" height="240px" center={[position.latitude, position.longitude]} zoom={16}>
              <PinPlacer
                position={position}
                onPlace={(lat, lon) => {
                  setPosition({ latitude: lat, longitude: lon, source: 'manual' });
                  setNeedsPin(false);
                }}
              />
            </BaseMap>
            {needsPin && <Alert type="warning" message="Chạm lên bản đồ hoặc kéo ghim xanh đến đúng vị trí sự cố." />}
            {addressText && (
              <Typography.Text>
                Địa chỉ: <Typography.Text strong>{addressText}</Typography.Text>
              </Typography.Text>
            )}
            {locate.data && !locate.data.insideGrid && (
              <Typography.Text type="secondary">
                Vị trí này chưa thuộc khu vực quản lý, nhưng phản ánh vẫn được ghi nhận.
              </Typography.Text>
            )}
          </Flex>
        </Card>
      )}

      <Card>
        <Form layout="vertical" component="div">
          {suggestions.length > 0 && (
            <fieldset style={{ border: 0, padding: 0, margin: '0 0 16px' }}>
              <legend style={{ marginBottom: 8, fontWeight: 600 }}>
                <Flex align="center" gap={8}>
                  <ThunderboltOutlined aria-hidden="true" /> Loại phản ánh gợi ý từ ảnh
                </Flex>
              </legend>
              <Flex wrap gap={8}>
                {suggestions.map((s) => (
                  <Chip key={s.categoryId} active={categoryId === String(s.categoryId)} onClick={() => setCategoryId(String(s.categoryId))}>
                    {s.name}
                  </Chip>
                ))}
              </Flex>
            </fieldset>
          )}

          <Form.Item label="Danh mục" htmlFor="category" extra="Không chắc thì để hệ thống tự phân loại.">
            <Select
              id="category"
              value={categoryId}
              onChange={(v) => setCategoryId(v)}
              options={[
                { value: '', label: 'Để hệ thống tự phân loại' },
                ...categoryOptions.map((c) => ({ value: String(c.id), label: c.name })),
              ]}
            />
          </Form.Item>

          <Form.Item
            label="Mô tả thêm (không bắt buộc)"
            htmlFor="description"
            extra={
              <Flex justify="space-between" gap={8}>
                <span>Ví dụ: ổ gà rộng khoảng 1 m, nằm giữa đường trước cổng trường.</span>
                <span style={{ flexShrink: 0 }}>
                  {description.length}/{DESCRIPTION_MAX}
                </span>
              </Flex>
            }
          >
            <Input.TextArea
              id="description"
              rows={3}
              maxLength={DESCRIPTION_MAX}
              value={description}
              placeholder="Mô tả ngắn gọn để cán bộ dễ xử lý"
              onChange={(e) => setDescription(e.target.value)}
            />
          </Form.Item>

          <Form.Item
            label="Số điện thoại liên hệ"
            htmlFor="phone"
            style={{ marginBottom: 0 }}
            help={phoneError ?? 'Dùng số này để tra cứu tiến độ. Ví dụ: 0912345678.'}
            validateStatus={phoneError ? 'error' : undefined}
          >
            <Input
              id="phone"
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              value={phone}
              placeholder="0912345678"
              onChange={(e) => setPhone(e.target.value)}
            />
          </Form.Item>
        </Form>
      </Card>

      {error && !phoneError && <Alert type="error" showIcon message={error} />}

      {/* Dính ở đáy vùng cuộn (ngay trên footer cố định), luôn bấm được mà không che nội dung */}
      <div
        style={{
          position: 'sticky',
          bottom: 0,
          zIndex: 20,
          margin: '0 -16px -16px',
          padding: 16,
          background: 'rgba(255, 255, 255, 0.95)',
          borderTop: '1px solid rgba(5, 5, 5, 0.06)',
        }}
      >
        <Button type="primary" size="large" block onClick={() => void onSubmit()}>
          <SendOutlined aria-hidden="true" /> Gửi phản ánh
        </Button>
      </div>
    </Flex>
  );
}
/** Màn hình đầu tiên: chụp hoặc chọn ảnh. Đây là màn hình người dân thấy khi mở trang chủ. */
function PhotoStep({ onSelect }) {
  const cameraRef = useRef(null);
  const libraryRef = useRef(null);
  const handle = (list) => {
    const file = list?.[0];
    if (file) onSelect(file);
  };
  return (
    <Flex vertical gap={24}>
      <Steps size="small" current={0} items={STEP_ITEMS} />

      <section aria-labelledby="photo-title" style={{ textAlign: 'center' }}>
        <Flex vertical align="center" gap={8}>
          <Flex
            align="center"
            justify="center"
            style={{ width: 64, height: 64, borderRadius: 16, background: 'rgba(33, 166, 145, 0.15)', color: '#21A691' }}
          >
            <CameraOutlined style={{ fontSize: 32 }} aria-hidden="true" />
          </Flex>
          <Typography.Title level={2} id="photo-title" style={{ margin: 0 }}>
            Chụp ảnh hiện trường
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ maxWidth: 384, margin: 0 }}>
            Chỉ mất vài giây. Hệ thống tự lấy vị trí của bạn và chuyển ảnh đến đúng cán bộ phụ trách khu vực.
          </Typography.Paragraph>
        </Flex>
      </section>

      <Card>
        <Flex vertical gap={12}>
          <Button type="primary" size="large" block onClick={() => cameraRef.current?.click()}>
            <CameraOutlined style={{ fontSize: 24 }} aria-hidden="true" /> Chụp ảnh
          </Button>
          <Button size="large" block onClick={() => libraryRef.current?.click()}>
            <PictureOutlined style={{ fontSize: 24 }} aria-hidden="true" /> Chọn ảnh có sẵn
          </Button>
          <input
            ref={cameraRef}
            type="file"
            accept="image/*"
            capture="environment"
            hidden
            aria-label="Chụp ảnh bằng camera"
            onChange={(e) => handle(e.target.files)}
          />
          <input
            ref={libraryRef}
            type="file"
            accept="image/*"
            hidden
            aria-label="Chọn ảnh có sẵn"
            onChange={(e) => handle(e.target.files)}
          />
        </Flex>
      </Card>

      <Card
        title={
          <Flex align="center" gap={8}>
            <BulbOutlined aria-hidden="true" /> Chụp ảnh thế nào để xử lý nhanh?
          </Flex>
        }
      >
        <Flex vertical gap={8}>
          <Flex gap={8} align="flex-start">
            <CheckOutlined style={{ marginTop: 3, color: '#21A691' }} aria-hidden="true" />
            <span>Chụp rõ sự cố, đứng cách khoảng 2–3 m.</span>
          </Flex>
          <Flex gap={8} align="flex-start">
            <CheckOutlined style={{ marginTop: 3, color: '#21A691' }} aria-hidden="true" />
            <span>Chụp đủ ánh sáng, tránh ngược sáng.</span>
          </Flex>
          <Flex gap={8} align="flex-start">
            <CheckOutlined style={{ marginTop: 3, color: '#21A691' }} aria-hidden="true" />
            <span>Nếu được, chụp thêm cảnh xung quanh để nhận biết địa điểm.</span>
          </Flex>
        </Flex>
      </Card>

      <Flex align="center" justify="center" gap={6}>
        <HeartOutlined aria-hidden="true" />
        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
          Ảnh chỉ dùng để xử lý phản ánh của bạn.
        </Typography.Text>
      </Flex>
    </Flex>
  );
}
function PhotoPreview({ file }) {
  const [url, setUrl] = useState(null);
  useEffect(() => {
    const objectUrl = URL.createObjectURL(file);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [file]);
  if (!url) return null;
  return (
    <img
      src={url}
      alt="Ảnh phản ánh đã chọn"
      style={{ maxHeight: 288, width: '100%', borderRadius: 8, objectFit: 'cover' }}
    />
  );
}
/** Ghim kéo được và chạm để đặt lại vị trí (FE-08 bước 3). */
function PinPlacer({ position, onPlace }) {
  useMapEvents({
    click(e) {
      onPlace(e.latlng.lat, e.latlng.lng);
    },
  });
  return (
    <Marker
      position={[position.latitude, position.longitude]}
      icon={pinIcon}
      draggable
      eventHandlers={{
        dragend(e) {
          const { lat, lng } = e.target.getLatLng();
          onPlace(lat, lng);
        },
      }}
    />
  );
}
function Chip({ active, onClick, children }) {
  return (
    <Button shape="round" type={active ? 'primary' : 'default'} aria-pressed={active} onClick={onClick}>
      {children}
    </Button>
  );
}
/** Gợi ý danh mục bằng AI. Lỗi thì bỏ qua, người dân vẫn chọn tay được. */
async function previewCategories(file, set) {
  try {
    const res = await postApiPublicClassifyPreview({ Image: file });
    const body = res.data;
    set(Array.isArray(body) ? body : (body.suggestions ?? []));
  } catch {
    set([]);
  }
}
