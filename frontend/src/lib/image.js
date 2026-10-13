import imageCompression from 'browser-image-compression';
/** Giới hạn gửi ảnh (FE-08 bước 6): tối đa 2 MB, cạnh dài 2048 px. */
export const MAX_IMAGE_MB = 2;
export const MAX_IMAGE_EDGE_PX = 2048;
export async function compressForUpload(file) {
  return imageCompression(file, {
    maxSizeMB: MAX_IMAGE_MB,
    maxWidthOrHeight: MAX_IMAGE_EDGE_PX,
    useWebWorker: true,
    initialQuality: 0.85,
  });
}
