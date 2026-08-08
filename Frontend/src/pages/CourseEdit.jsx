import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { toast } from 'react-hot-toast';
import api from '../api/axiosConfig';
import ConfirmModal from '../components/ConfirmModal';
import CurriculumBuilder from '../components/CurriculumBuilder';
import CourseBasicInfoForm, { validateCourseForm } from '../components/CourseBasicInfoForm';
import './CourseCreate.css';
import { useCategories } from '../context/CategoryContext';
import { validateCourseImage } from '../components/CourseThumbnail';
import CourseQuizEditor from '../components/CourseQuizEditor';

const emptyForm = { title: '', description: '', price: 0, categoryIds: [], thumbnailUrl: '', status: 'Draft' };
const normalize = value => JSON.stringify({
  title: value.title?.trim() || '',
  description: value.description?.trim() || '',
  price: Number(value.price) || 0,
  categoryIds: (value.categoryIds || []).map(Number).sort(),
  thumbnailUrl: value.thumbnailUrl || '',
  status: value.status || 'Draft'
});

const extractErrors = error => {
  const payload = error.response?.data;
  if (Array.isArray(payload?.errors)) return payload.errors.map(String);
  if (payload?.errors && typeof payload.errors === 'object') return Object.values(payload.errors).flat().map(String);
  if (Array.isArray(payload)) return payload.map(String);
  return [payload?.message || error.message || 'Không thể lưu khóa học.'];
};

export default function CourseEdit() {
  const { id } = useParams();
  const navigate = useNavigate();
  const draftKey = `edumy:course-edit-draft:${id}`;
  const [form, setForm] = useState(emptyForm);
  const { categories, error: categoryError } = useCategories();
  const [thumbnail, setThumbnail] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [aiLoading, setAiLoading] = useState(false);
  const [hydrated, setHydrated] = useState(false);
  const [curriculumDirty, setCurriculumDirty] = useState(false);
  const [errors, setErrors] = useState([]);
  const [activeTab, setActiveTab] = useState('curriculum'); // 'curriculum' or 'finalQuiz'
  const [pendingDraft, setPendingDraft] = useState(null);
  const [pendingNavigation, setPendingNavigation] = useState(null);
  const initialSnapshotRef = useRef(normalize(emptyForm));
  const allowNavigationRef = useRef(false);
  const errorSummaryRef = useRef(null);
  const curriculumRef = useRef(null);

  const dirty = useMemo(() => hydrated && (Boolean(thumbnail) || curriculumDirty || normalize(form) !== initialSnapshotRef.current), [curriculumDirty, form, hydrated, thumbnail]);

  useEffect(() => {
    api.get(`/courses/${id}`)
      .then(courseResponse => {
        const course = courseResponse.data;
        const loaded = { title: course.title || '', description: course.description || '', price: course.price ?? 0, categoryIds: course.categoryIds || (course.categoryId ? [course.categoryId] : []), thumbnailUrl: course.thumbnailUrl || '', status: course.status || 'Draft' };
        setForm(loaded);
        initialSnapshotRef.current = normalize(loaded);
        try {
          const saved = JSON.parse(localStorage.getItem(draftKey));
          if (saved?.form && normalize(saved.form) !== normalize(loaded)) setPendingDraft(saved);
        } catch { localStorage.removeItem(draftKey); }
        setHydrated(true);
      })
      .catch(error => setErrors([error.response?.data?.message || 'Không thể tải khóa học.']))
      .finally(() => setLoading(false));
  }, [draftKey, id]);

  useEffect(() => {
    if (!dirty) return undefined;
    const timer = window.setTimeout(() => {
      localStorage.setItem(draftKey, JSON.stringify({ version: 1, courseId: Number(id), savedAt: new Date().toISOString(), form }));
    }, 700);
    return () => window.clearTimeout(timer);
  }, [dirty, draftKey, form, id]);

  // Browser refresh/close is handled by the local draft above. Internal links use
  // the in-app navigation guard below.
  useEffect(() => {
    const guardLink = event => {
      if (!dirty || allowNavigationRef.current || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
      const anchor = event.target.closest?.('a[href]');
      if (!anchor || anchor.target === '_blank' || anchor.hasAttribute('download')) return;
      const target = new URL(anchor.href, window.location.href);
      if (target.origin !== window.location.origin || target.href === window.location.href) return;
      event.preventDefault();
      event.stopPropagation();
      setPendingNavigation(`${target.pathname}${target.search}${target.hash}`);
    };
    document.addEventListener('click', guardLink, true);
    return () => document.removeEventListener('click', guardLink, true);
  }, [dirty]);

  const showErrors = messages => {
    setErrors([...new Set(messages.filter(Boolean))]);
    requestAnimationFrame(() => {
      errorSummaryRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      errorSummaryRef.current?.focus();
    });
  };
  const change = event => {
    const { name, value } = event.target;
    setForm(previous => ({ ...previous, [name]: value }));
    setErrors(previous => previous.filter(item => !item.toLowerCase().includes(name === 'categoryId' ? 'danh mục' : name === 'price' ? 'giá' : name === 'title' ? 'tiêu đề' : name === 'description' ? 'mô tả' : 'trạng thái')));
  };
  const selectFile = event => {
    const file = event.target.files?.[0] || null;
    const imageError = validateCourseImage(file);
    if (imageError) { showErrors([imageError]); event.target.value = ''; return; }
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
    const validation = validateCourseForm(form, true);
    if (validation.length) return showErrors(validation);
    setSaving(true);
    setErrors([]);
    try {
      await curriculumRef.current?.savePending();
      let thumbnailUrl = form.thumbnailUrl;
      if (thumbnail) {
        const body = new FormData();
        body.append('file', thumbnail);
        const { data } = await api.post('/media/upload', body, { headers: { 'Content-Type': undefined } });
        thumbnailUrl = data.url;
      }
      const { data } = await api.put(`/courses/${id}`, { ...form, title: form.title.trim(), description: form.description.trim(), categoryIds: (form.categoryIds || []).map(Number), price: Number(form.price), thumbnailUrl });
      const saved = { ...form, ...data, categoryIds: data.categoryIds || (data.categoryId ? [data.categoryId] : []), thumbnailUrl: data.thumbnailUrl || thumbnailUrl };
      initialSnapshotRef.current = normalize(saved);
      allowNavigationRef.current = true;
      setForm(saved);
      setThumbnail(null);
      setCurriculumDirty(false);
      localStorage.removeItem(draftKey);
      toast.success('Lưu thay đổi khóa học thành công.');
      window.setTimeout(() => navigate('/instructor', { replace: true }), 750);
    } catch (error) {
      allowNavigationRef.current = false;
      showErrors(extractErrors(error));
    } finally { setSaving(false); }
  };

  const requestLeave = () => dirty ? setPendingNavigation('/instructor') : navigate('/instructor');
  const confirmLeave = () => {
    const target = pendingNavigation;
    allowNavigationRef.current = true;
    localStorage.removeItem(draftKey);
    setPendingNavigation(null);
    navigate(target || '/instructor');
  };
  const restoreDraft = () => {
    setForm(previous => ({ ...previous, ...pendingDraft.form, categoryIds: pendingDraft.form.categoryIds || (pendingDraft.form.categoryId ? [Number(pendingDraft.form.categoryId)] : []) }));
    setPendingDraft(null);
    toast.success('Đã khôi phục dữ liệu chỉnh sửa chưa lưu.');
  };
  const discardDraft = () => { localStorage.removeItem(draftKey); setPendingDraft(null); };

  if (loading) return <div className="container text-center py-5"><div className="spinner-border text-primary" /><p>Đang tải khóa học...</p></div>;
  return <div className="course-create-container course-edit-page container"><div className="form-wrapper">
    <h1>Chỉnh sửa khóa học: {form.title || 'Chưa có tiêu đề'}</h1>
    <p className="text-muted">Hoàn thiện thông tin và curriculum, sau đó lưu toàn bộ thay đổi ở cuối trang.</p>
    {pendingDraft && <div className="alert alert-info d-flex flex-wrap justify-content-between align-items-center gap-2" role="status">
      <span>Đã tìm thấy dữ liệu chỉnh sửa chưa lưu{pendingDraft.savedAt ? ` từ ${new Date(pendingDraft.savedAt).toLocaleString('vi-VN')}` : ''}.</span>
      <span className="d-flex gap-2"><button type="button" className="btn btn-sm btn-primary" onClick={restoreDraft}>Khôi phục</button><button type="button" className="btn btn-sm btn-outline-secondary" onClick={discardDraft}>Bỏ qua</button></span>
    </div>}
    {errors.length > 0 && <div ref={errorSummaryRef} role="alert" aria-live="assertive" tabIndex={-1} className="course-form-error-summary error-alert">
      <h2>Không thể lưu khóa học</h2><p>Vui lòng kiểm tra các lỗi sau:</p><ul className="mb-0">{errors.map((error, index) => <li key={`${error}-${index}`}>{error}</li>)}</ul>
    </div>}
    <ul className="nav nav-tabs mb-4 border-bottom">
      <li className="nav-item">
        <button type="button" className={`nav-link px-3 py-2 fw-semibold ${activeTab === 'curriculum' ? 'active border-bottom border-primary text-primary' : 'text-muted border-0 bg-transparent'}`} onClick={() => setActiveTab('curriculum')} style={{ borderTop: 0, borderLeft: 0, borderRight: 0 }}>Thông tin & Bài học</button>
      </li>
      <li className="nav-item">
        <button type="button" className={`nav-link px-3 py-2 fw-semibold ${activeTab === 'finalQuiz' ? 'active border-bottom border-primary text-primary' : 'text-muted border-0 bg-transparent'}`} onClick={() => setActiveTab('finalQuiz')} style={{ borderTop: 0, borderLeft: 0, borderRight: 0 }}>Final Quiz</button>
      </li>
    </ul>

    {activeTab === 'curriculum' ? (
      <>
        <form id="course-edit-form" onSubmit={submit} noValidate>
          {categoryError && <div className="error-alert">{categoryError}</div>}
          <CourseBasicInfoForm form={form} categories={categories} thumbnailFile={thumbnail} onChange={change} onFileChange={selectFile} onAiSuggest={suggest} aiLoading={aiLoading} showStatus />
        </form>
        <CurriculumBuilder ref={curriculumRef} courseId={Number(id)} onChanged={() => setCurriculumDirty(true)} />
        <div className="form-actions course-edit-actions mt-4">
          <button type="button" onClick={requestLeave} className="btn btn-outline-secondary">Quay lại dashboard</button>
          <button type="submit" form="course-edit-form" className="btn btn-primary" disabled={saving || !dirty}>
            {saving && <span className="spinner-border spinner-border-sm me-2" aria-hidden="true" />}{saving ? 'Đang lưu...' : 'Lưu thay đổi'}
          </button>
        </div>
      </>
    ) : (
      <div>
        <CourseQuizEditor courseId={Number(id)} />
        <div className="form-actions course-edit-actions mt-4">
          <button type="button" onClick={requestLeave} className="btn btn-outline-secondary">Quay lại dashboard</button>
        </div>
      </div>
    )}

    <ConfirmModal open={Boolean(pendingNavigation)} title="Bạn có thay đổi chưa lưu" message="Các thay đổi chưa được lưu sẽ bị mất. Bạn có chắc chắn muốn rời trang?" confirmLabel="Rời trang" cancelLabel="Tiếp tục chỉnh sửa" danger loading={false} onCancel={()=>setPendingNavigation(null)} onConfirm={confirmLeave}/>
  </div></div>;
}
