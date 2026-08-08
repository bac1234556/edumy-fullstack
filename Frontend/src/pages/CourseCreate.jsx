import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'react-hot-toast';
import api from '../api/axiosConfig';
import CourseBasicInfoForm, { validateCourseForm } from '../components/CourseBasicInfoForm';
import './CourseCreate.css';
import { useCategories } from '../context/CategoryContext';
import { validateCourseImage } from '../components/CourseThumbnail';

const initialForm = { title: '', description: '', price: 0, categoryIds: [], thumbnailUrl: '' };

export default function CourseCreate() {
  const navigate = useNavigate();
  const [form, setForm] = useState(initialForm);
  const { categories, error: categoryError } = useCategories();
  const [thumbnail, setThumbnail] = useState(null);
  const [saving, setSaving] = useState(false);
  const [aiLoading, setAiLoading] = useState(false);
  const [errors, setErrors] = useState([]);

  const change = event => setForm(previous => ({ ...previous, [event.target.name]: event.target.value }));
  const selectFile = event => {
    const file = event.target.files?.[0] || null;
    const imageError = validateCourseImage(file);
    if (imageError) { setErrors([imageError]); event.target.value = ''; return; }
    setThumbnail(file);
  };

  const suggest = async () => {
    setAiLoading(true);
    try {
      const { data } = await api.post('/courses/ai-suggest', { title: form.title, description: form.description });
      const category = data.recommendedCategory;
      if (category) {
        setForm(previous => ({ ...previous, categoryIds: [Number(category.categoryId)] }));
        toast.success(`Đã chọn danh mục ${category.name} (${Math.round(category.confidence * 100)}%).`);
      } else {
        toast.error(data.source === 'unavailable' ? 'Dịch vụ gợi ý hiện không khả dụng. Danh mục hiện tại được giữ nguyên.' : 'Chưa đủ độ tin cậy để tự chọn danh mục.');
      }
    } catch { toast.error('Không thể lấy gợi ý danh mục.'); }
    finally { setAiLoading(false); }
  };

  const submit = async event => {
    event.preventDefault();
    if (saving) return;
    const validation = validateCourseForm(form);
    if (validation.length) { setErrors(validation); return; }
    setSaving(true); setErrors([]);
    try {
      let thumbnailUrl = form.thumbnailUrl;
      if (thumbnail) {
        const body = new FormData(); body.append('file', thumbnail);
        const { data } = await api.post('/media/upload', body, { headers: { 'Content-Type': undefined } });
        thumbnailUrl = data.url;
      }
      const { data } = await api.post('/courses', { ...form, title: form.title.trim(), description: form.description.trim(), categoryIds: (form.categoryIds || []).map(Number), price: Number(form.price), thumbnailUrl });
      toast.success('Đã tạo bản nháp. Tiếp theo, hãy xây dựng curriculum và xuất bản khi sẵn sàng.');
      navigate(`/instructor/courses/${data.courseId}/edit`, { replace: true });
    } catch (error) {
      const responseErrors = error.response?.data?.errors;
      setErrors(Array.isArray(responseErrors) ? responseErrors : [error.response?.data?.message || 'Không thể tạo khóa học.']);
    } finally { setSaving(false); }
  };

  return <div className="course-create-container container"><div className="form-wrapper">
    <h1>Tạo khóa học mới</h1><p className="text-muted">Bước 1: lưu thông tin cơ bản dưới dạng Draft. Curriculum và xuất bản được thực hiện ở trang kế tiếp.</p>
    {errors.length > 0 && <div className="error-alert"><ul className="mb-0">{errors.map(error => <li key={error}>{error}</li>)}</ul></div>}
    {categoryError && <div className="error-alert">{categoryError}</div>}
    <form onSubmit={submit}><CourseBasicInfoForm form={form} categories={categories} thumbnailFile={thumbnail} onChange={change} onFileChange={selectFile} onAiSuggest={suggest} aiLoading={aiLoading} />
      <div className="form-actions"><button type="button" className="btn btn-secondary" onClick={() => navigate('/instructor')}>Hủy</button><button className="btn btn-primary" disabled={saving}>{saving ? 'Đang tạo...' : 'Lưu Draft và tiếp tục'}</button></div>
    </form>
  </div></div>;
}
