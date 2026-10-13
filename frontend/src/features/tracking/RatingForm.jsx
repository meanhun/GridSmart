import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { Alert, Button, Card, Flex, Input, Rate, Typography } from 'antd';
import { postApiPublicIncidentsCodeRating } from '@/api/generated/endpoints/public/public';
import { errorMessage } from '@/lib/error-messages';
/** Đánh giá kết quả xử lý (FE-10): 1 đến 5 sao, góp ý tùy chọn. Gửi xong thì khóa form. */
export function RatingForm({ trackingCode, phone }) {
  const queryClient = useQueryClient();
  const [rating, setRating] = useState(0);
  const [feedback, setFeedback] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState(null);
  const submit = async () => {
    if (rating < 1) {
      setError('Hãy chọn số sao đánh giá.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await postApiPublicIncidentsCodeRating(trackingCode, { phone, rating, feedback: feedback.trim() });
      setDone(true);
      await queryClient.invalidateQueries({ queryKey: ['/api/public/track'] });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };
  if (done) {
    return <Alert type="success" showIcon role="status" message={`Cảm ơn bạn đã đánh giá ${rating}/5 sao.`} />;
  }
  return (
    <Card size="small" title="Đánh giá kết quả xử lý" aria-labelledby="rating-title">
      <Flex vertical gap={12}>
        <Rate value={rating} onChange={setRating} aria-label="Số sao" style={{ fontSize: 28 }} />
        <div>
          <label htmlFor="feedback">
            <Typography.Text>Góp ý (tùy chọn)</Typography.Text>
          </label>
          <Input.TextArea
            id="feedback"
            rows={2}
            maxLength={500}
            value={feedback}
            onChange={(e) => setFeedback(e.target.value)}
          />
        </div>
        {error && <Alert type="error" showIcon role="alert" message={error} />}
        <div>
          <Button type="primary" loading={submitting} disabled={submitting} onClick={() => void submit()}>
            {submitting ? 'Đang gửi…' : 'Gửi đánh giá'}
          </Button>
        </div>
      </Flex>
    </Card>
  );
}
