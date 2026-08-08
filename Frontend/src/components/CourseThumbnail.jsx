import { useEffect, useMemo, useState } from 'react';

const API_ORIGIN = (import.meta.env.VITE_API_URL || '/api').replace(/\/api\/?$/, '');
const assetByCategory = [
  [/photo/i, 'photography'], [/business/i, 'business'], [/design/i, 'design'],
  [/marketing/i, 'marketing'], [/office/i, 'office'], [/(machine learning|data science)/i, 'data-science'],
  [/cloud/i, 'cloud'], [/(cyber|security)/i, 'security'], [/(mobile|web|development)/i, 'development'],
  [/(it|software)/i, 'it-software'], [/personal/i, 'personal-development']
];

export function categoryPlaceholder(categoryName = '') {
  const asset = assetByCategory.find(([pattern]) => pattern.test(categoryName))?.[1] || 'default';
  return `/images/course-placeholders/${asset}.svg`;
}

export function resolveCourseImage(src, categoryName) {
  if (!src || src === 'null') return categoryPlaceholder(categoryName);
  if (/^(https?:|blob:|data:)/i.test(src) || src.startsWith('/images/')) return src;
  return `${API_ORIGIN}${src.startsWith('/') ? '' : '/'}${src}`;
}

export function validateCourseImage(file) {
  if (!file) return '';
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) return 'Ảnh phải có định dạng JPG, PNG hoặc WebP.';
  if (file.size > 5 * 1024 * 1024) return 'Ảnh đại diện không được vượt quá 5 MB.';
  return '';
}

export default function CourseThumbnail({ src, categoryName, file, alt = 'Ảnh khóa học', className = '', style, ...props }) {
  const preview = useMemo(() => file ? URL.createObjectURL(file) : '', [file]);
  const fallback = categoryPlaceholder(categoryName);
  const preferred = preview || resolveCourseImage(src, categoryName);
  const [current, setCurrent] = useState(preferred);
  useEffect(() => { setCurrent(preferred); }, [preferred]);
  useEffect(() => () => { if (preview) URL.revokeObjectURL(preview); }, [preview]);
  return <img {...props} src={current} alt={alt} loading="lazy" className={className} style={{ aspectRatio: '16 / 9', objectFit: 'cover', ...style }} onError={() => current !== fallback && setCurrent(fallback)} />;
}
