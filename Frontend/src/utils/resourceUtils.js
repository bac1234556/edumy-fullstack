const VIDEO_EXTENSIONS = new Set(['.mp4', '.webm', '.ogg', '.mov', '.m4v']);
const IMAGE_EXTENSIONS = new Set(['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg', '.bmp']);
const PDF_EXTENSIONS = new Set(['.pdf']);
const POWERPOINT_EXTENSIONS = new Set(['.ppt', '.pptx']);
const DOCUMENT_EXTENSIONS = new Set([
  '.doc', '.docx', '.xls', '.xlsx',
  '.txt', '.csv', '.odt', '.ods', '.odp'
]);

/**
 * Extracts a normalized lowercased extension from a URL or filename.
 * Removes query parameters and hash fragments safely.
 */
export function getFileExtension(pathOrUrl) {
  if (!pathOrUrl || typeof pathOrUrl !== 'string') return '';
  try {
    const cleanPath = pathOrUrl.split('?')[0].split('#')[0];
    const lastDotIndex = cleanPath.lastIndexOf('.');
    if (lastDotIndex === -1 || lastDotIndex === cleanPath.length - 1) return '';
    return cleanPath.slice(lastDotIndex).toLowerCase();
  } catch {
    return '';
  }
}

/**
 * Normalizes and determines the lesson resource type based on backend metadata and file extension/MIME.
 * Returns one of: 'video' | 'image' | 'pdf' | 'powerpoint' | 'document' | 'file' | 'none'
 */
export function detectLessonResourceType(options) {
  const { resourceType, contentType, resourceUrl, fileName } = options || {};
  const rawType = (resourceType || '').toString().trim().toLowerCase();

  // Explicit confirmation of no resource
  if (rawType === 'none' || rawType === 'reading' || rawType === 'no-file') {
    return 'none';
  }

  // 1. Explicit specific resourceTypes
  if (rawType === 'video') return 'video';
  if (rawType === 'image' || rawType === 'picture' || rawType === 'photo' || rawType === 'ảnh') return 'image';
  if (rawType === 'pdf') return 'pdf';
  if (rawType === 'powerpoint' || rawType === 'ppt' || rawType === 'pptx') return 'powerpoint';
  if (rawType === 'document' || rawType === 'doc' || rawType === 'tài liệu') return 'document';

  const url = (resourceUrl || '').toString().trim();
  const file = (fileName || '').toString().trim();
  const ext = getFileExtension(file) || getFileExtension(url);

  // 2. Check ContentType / MIME type
  const mime = (contentType || '').toString().trim().toLowerCase();
  if (mime.startsWith('video/')) return 'video';
  if (mime.startsWith('image/')) return 'image';
  if (mime === 'application/pdf') return 'pdf';
  if (mime.includes('presentation') || mime.includes('powerpoint')) return 'powerpoint';
  if (
    mime.includes('word') ||
    mime.includes('excel') ||
    mime.includes('spreadsheet') ||
    mime.includes('officedocument') ||
    mime.startsWith('text/')
  ) {
    return 'document';
  }

  // 3. Check direct file extension matches
  if (VIDEO_EXTENSIONS.has(ext)) return 'video';
  if (IMAGE_EXTENSIONS.has(ext)) return 'image';
  if (PDF_EXTENSIONS.has(ext)) return 'pdf';
  if (POWERPOINT_EXTENSIONS.has(ext)) return 'powerpoint';
  if (DOCUMENT_EXTENSIONS.has(ext)) return 'document';

  // 4. Generic 'file' resource type
  if (rawType === 'file') return 'file';

  // 5. Default for any remaining file with a valid URL or filename
  if (url || file) {
    return 'file';
  }

  return 'none';
}

/**
 * Normalizes lesson resource DTO from backend API into a unified object.
 */
export function normalizeLessonResource(lesson) {
  if (!lesson) return null;

  const fileUrl = lesson.fileUrl || lesson.videoUrl || lesson.resourceUrl || '';
  const originalFileName = lesson.originalFileName || lesson.fileName || lesson.title || '';
  const rawEndpoint = lesson.resourceEndpoint || (lesson.lessonId ? `/api/learning/lessons/${lesson.lessonId}/resource` : '');
  const resourceEndpoint = rawEndpoint ? rawEndpoint.replace(/^\/?api\//, '/') : '';

  const resourceType = detectLessonResourceType({
    resourceType: lesson.resourceType,
    contentType: lesson.contentType,
    resourceUrl: fileUrl,
    fileName: originalFileName
  });

  let hasResource;
  if (lesson.hasResource === false) {
    hasResource = false;
  } else if (lesson.hasResource === true || lesson.resourceExists === true) {
    hasResource = resourceType !== 'none';
  } else {
    hasResource = resourceType !== 'none' && (
      Boolean(fileUrl) ||
      Boolean(resourceEndpoint)
    );
  }

  return {
    lessonId: lesson.lessonId || lesson.id,
    sectionId: lesson.sectionId,
    title: lesson.title,
    duration: lesson.duration,
    orderIndex: lesson.orderIndex,
    resourceType,
    fileUrl,
    videoUrl: lesson.videoUrl || fileUrl,
    originalFileName,
    contentType: lesson.contentType || '',
    fileSizeBytes: lesson.fileSizeBytes || lesson.fileSize || null,
    hasResource,
    resourceExists: lesson.resourceExists !== false,
    resourceEndpoint,
    isCompleted: Boolean(lesson.isCompleted),
    isDraft: Boolean(lesson.isDraft),
    isPreview: Boolean(lesson.isPreview)
  };
}

/**
 * Resolves static resource relative URLs (/uploads/...) to absolute accessible URLs.
 */
export function resolveAssetUrl(resourceUrl) {
  if (!resourceUrl || typeof resourceUrl !== 'string') return '';
  const url = resourceUrl.trim();
  if (!url) return '';
  if (url.startsWith('http://') || url.startsWith('https://') || url.startsWith('blob:') || url.startsWith('data:')) {
    return url;
  }

  const apiUrl = (typeof import.meta !== 'undefined' && import.meta.env?.VITE_API_URL) ? import.meta.env.VITE_API_URL : '/api';
  const backendBase = apiUrl.replace(/\/api\/?$/, '');
  const cleanPath = url.startsWith('/') ? url : `/${url}`;
  
  return `${backendBase}${cleanPath}`;
}

/**
 * Helper to format bytes into human readable file size (e.g. 1.5 MB).
 */
export function formatBytes(bytes, decimals = 1) {
  if (!bytes || isNaN(bytes) || bytes <= 0) return '';
  const k = 1024;
  const dm = decimals < 0 ? 0 : decimals;
  const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`;
}
