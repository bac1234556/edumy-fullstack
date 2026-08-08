import { forwardRef, useEffect, useImperativeHandle, useState } from 'react';
import { FilePlus2, GripVertical, Plus, Save, Trash2 } from 'lucide-react';
import { toast } from 'react-hot-toast';
import api from '../api/axiosConfig';
import ConfirmModal from './ConfirmModal';

const emptyLesson = { title: '', resourceType: 'Video', duration: '', orderIndex: 1, isPreview: false, isDraft: false };
const lessonStatus = lesson => lesson.isDraft ? 'Draft' : 'Normal';
const withStatus = (lesson, status) => ({ ...lesson, isPreview: false, isDraft: status === 'Draft' });
const lessonPayload = lesson => ({
  title: lesson.title.trim(), resourceType: lesson.resourceType || 'File',
  duration: lesson.duration === '' ? 0 : Number(lesson.duration), orderIndex: Math.max(1, Number(lesson.orderIndex) || 1),
  isPreview: false, isDraft: Boolean(lesson.isDraft), fileUrl: lesson.fileUrl,
  originalFileName: lesson.originalFileName, contentType: lesson.contentType, fileSizeBytes: lesson.fileSizeBytes
});

function StatusFields({ lesson, onChange, name }) {
  return <fieldset className="border-0 p-0 m-0">
    <legend className="form-label fs-6 mb-1">Trạng thái bài học</legend>
    <div className="d-flex flex-wrap gap-3" role="radiogroup">
      {['Normal', 'Draft'].map(status => <label key={status} className="d-flex gap-1 align-items-center">
        <input type="radio" name={name} value={status} checked={lessonStatus(lesson) === status} onChange={() => onChange(status)} /> {status}
      </label>)}
    </div>
    <small className="text-muted">Normal: học viên đã đăng ký xem được; Draft: chưa hiển thị cho học viên.</small>
  </fieldset>;
}

const CurriculumBuilder = forwardRef(function CurriculumBuilder({ courseId, onChanged }, ref) {
  const [sections, setSections] = useState([]);
  const [sectionTitle, setSectionTitle] = useState('');
  const [lessonDrafts, setLessonDrafts] = useState({});
  const [files, setFiles] = useState({});
  const [busy, setBusy] = useState(false);
  const [confirmation, setConfirmation] = useState(null);

  const load = async () => { const { data } = await api.get(`/courses/${courseId}/curriculum`); setSections(data || []); };
  useEffect(() => { if (courseId) load().catch(() => toast.error('Không thể tải curriculum.')); }, [courseId]);

  const savePending = async () => {
    setBusy(true);
    try {
      await Promise.all(sections.flatMap(section => [
        api.put(`/sections/${section.sectionId}`, { title: section.title.trim(), orderIndex: Math.max(1, Number(section.orderIndex) || 1) }),
        ...(section.lessons || []).map(lesson => api.put(`/lessons/${lesson.lessonId}`, lessonPayload(lesson)))
      ]));
      await load();
    } finally { setBusy(false); }
  };
  useImperativeHandle(ref, () => ({ savePending }), [sections]);

  const run = async (action, success) => {
    setBusy(true);
    try { await action(); if (success) toast.success(success); await load(); onChanged?.(); }
    catch (error) { toast.error(error.response?.data?.message || 'Không thể cập nhật curriculum.'); }
    finally { setBusy(false); }
  };
  const confirmAction = async () => { const action = confirmation?.action; if (action) await action(); setConfirmation(null); };
  const updateSectionLocal = (id, field, value) => { setSections(value0 => value0.map(section => section.sectionId === id ? { ...section, [field]: value } : section)); onChanged?.(); };
  const updateLessonLocal = (sectionId, lessonId, change) => {
    setSections(value => value.map(section => section.sectionId !== sectionId ? section : {
      ...section, lessons: section.lessons.map(lesson => lesson.lessonId === lessonId ? { ...lesson, ...change } : lesson)
    }));
    onChanged?.();
  };
  const updateDraft = (sectionId, change) => setLessonDrafts(value => ({ ...value, [sectionId]: { ...(value[sectionId] || emptyLesson), ...change } }));

  const addSection = event => {
    event.preventDefault();
    const title = sectionTitle.trim();
    if (!title) return toast.error('Tiêu đề chương là bắt buộc.');
    run(() => api.post(`/courses/${courseId}/sections`, { title, orderIndex: sections.length + 1 }), 'Đã thêm chương.');
    setSectionTitle('');
  };
  const saveLesson = async sectionId => {
    const draft = lessonDrafts[sectionId] || emptyLesson;
    if (!draft.title.trim()) return toast.error('Tiêu đề bài học là bắt buộc.');
    setBusy(true);
    try {
      let metadata = {};
      const file = files[sectionId];
      if (file) {
        const form = new FormData(); form.append('file', file);
        const { data } = await api.post('/media/upload', form, { headers: { 'Content-Type': undefined } });
        metadata = { fileUrl: data.url, originalFileName: data.originalFileName, contentType: data.contentType, fileSizeBytes: data.fileSizeBytes, resourceType: data.resourceType || draft.resourceType };
      }
      await api.post(`/sections/${sectionId}/lessons`, { ...lessonPayload(draft), ...metadata });
      setLessonDrafts(value => ({ ...value, [sectionId]: { ...emptyLesson } }));
      setFiles(value => ({ ...value, [sectionId]: null }));
      toast.success('Đã thêm bài học.'); await load(); onChanged?.();
    } catch (error) { toast.error(error.response?.data?.message || 'Upload hoặc lưu bài học thất bại.'); }
    finally { setBusy(false); }
  };
  const replaceFile = async (lesson, file) => {
    setBusy(true);
    try {
      const form = new FormData(); form.append('file', file);
      const { data } = await api.post('/media/upload', form, { headers: { 'Content-Type': undefined } });
      await api.put(`/lessons/${lesson.lessonId}`, { ...lessonPayload(lesson), fileUrl: data.url, originalFileName: data.originalFileName, contentType: data.contentType, fileSizeBytes: data.fileSizeBytes, resourceType: data.resourceType });
      toast.success('Đã thay file.'); await load(); onChanged?.();
    } catch (error) { toast.error(error.response?.data?.message || 'Không thể thay file.'); }
    finally { setBusy(false); }
  };

  const getAcceptAttribute = (resourceType) => {
    switch (resourceType) {
      case 'Video': return 'video/*';
      case 'Image': return 'image/*';
      case 'Pdf': return '.pdf';
      case 'PowerPoint': return '.ppt,.pptx';
      case 'Document': return '.doc,.docx,.xls,.xlsx,.ppt,.pptx,.txt,.csv,.odt,.ods,.odp';
      default: return undefined;
    }
  };

  return <section className="curriculum-builder mt-5">
    <div className="d-flex justify-content-between align-items-center gap-2 flex-wrap">
      <div><h2>Curriculum Builder</h2><p className="text-muted">Quản lý chương, bài học, tài nguyên và quyền hiển thị.</p></div>
      <button className="btn btn-outline-secondary" type="button" disabled={busy} onClick={() => setConfirmation({ title: 'Tạo curriculum mẫu?', message: 'Dữ liệu mẫu chỉ được tạo khi khóa học chưa có curriculum.', confirmLabel: 'Tạo dữ liệu mẫu', action: () => run(() => api.post(`/courses/${courseId}/curriculum/sample`), 'Đã tạo dữ liệu mẫu.') })}>Tạo dữ liệu mẫu</button>
    </div>
    <form className="mb-4" onSubmit={addSection}>
      <label htmlFor="new-section-title" className="form-label">Tiêu đề chương</label>
      <div className="d-flex gap-2"><input id="new-section-title" className="form-control" maxLength="200" value={sectionTitle} onChange={event => setSectionTitle(event.target.value)} placeholder="Ví dụ: Chương 1 - Tổng quan" />
        <button className="btn btn-primary" disabled={busy} aria-label="Thêm chương"><Plus size={17} aria-hidden="true" /> Thêm chương</button></div>
      <small className="text-muted">Tên ngắn gọn mô tả nhóm bài học trong chương.</small>
    </form>
 
    {sections.length === 0 ? <div className="alert alert-light border">Chưa có chương nào.</div> : sections.map(section => <article className="card border mb-3" key={section.sectionId}>
      <div className="card-header bg-light row g-2 align-items-end">
        <div className="col-auto"><GripVertical size={18} aria-hidden="true" /></div>
        <div className="col"><label className="form-label" htmlFor={`section-title-${section.sectionId}`}>Tiêu đề chương</label><input id={`section-title-${section.sectionId}`} className="form-control" value={section.title} onChange={event => updateSectionLocal(section.sectionId, 'title', event.target.value)} /></div>
        <div className="col-md-2"><label className="form-label" htmlFor={`section-order-${section.sectionId}`}>Thứ tự chương</label><input id={`section-order-${section.sectionId}`} className="form-control" type="number" min="1" value={section.orderIndex} onChange={event => updateSectionLocal(section.sectionId, 'orderIndex', event.target.value)} /></div>
        <div className="col-auto d-flex gap-2"><button type="button" title="Lưu chương" aria-label={`Lưu chương ${section.title}`} className="btn btn-outline-primary" disabled={busy} onClick={() => run(() => api.put(`/sections/${section.sectionId}`, { title: section.title.trim(), orderIndex: Math.max(1, Number(section.orderIndex) || 1) }), 'Đã lưu chương.')}><Save size={17} aria-hidden="true" /></button>
          <button type="button" title="Xóa chương" aria-label={`Xóa chương ${section.title}`} className="btn btn-outline-danger" disabled={busy} onClick={() => setConfirmation({ title: 'Xóa chương?', message: `Chương “${section.title}” và toàn bộ bài học bên trong sẽ bị xóa.`, confirmLabel: 'Xóa chương', danger: true, action: () => run(() => api.delete(`/sections/${section.sectionId}`), 'Đã xóa chương.') })}><Trash2 size={17} aria-hidden="true" /></button></div>
      </div>
      <div className="card-body">
        {(section.lessons || []).map(lesson => <div className="border rounded p-3 mb-3" key={lesson.lessonId}>
          <div className="row g-3">
            <div className="col-md-5"><label className="form-label">Tiêu đề bài học</label><input className="form-control" value={lesson.title} onChange={event => updateLessonLocal(section.sectionId, lesson.lessonId, { title: event.target.value })} /><small className="text-muted">Tên nội dung học viên sẽ nhìn thấy.</small></div>
            <div className="col-md-2"><label className="form-label">Loại tài nguyên</label><select className="form-select" value={lesson.resourceType || 'File'} onChange={event => updateLessonLocal(section.sectionId, lesson.lessonId, { resourceType: event.target.value })}><option value="Video">Video</option><option value="Image">Ảnh (Image)</option><option value="Pdf">PDF</option><option value="PowerPoint">PowerPoint</option><option value="Document">Tài liệu</option><option value="File">File khác</option><option value="None">Không file</option></select></div>
            <div className="col-md-2"><label className="form-label">Thời lượng</label><input className="form-control" type="number" min="0" placeholder="Giây" value={lesson.duration ?? ''} onChange={event => updateLessonLocal(section.sectionId, lesson.lessonId, { duration: event.target.value })} /></div>
            <div className="col-md-2"><label className="form-label">Thứ tự bài học</label><input className="form-control" type="number" min="1" value={lesson.orderIndex} onChange={event => updateLessonLocal(section.sectionId, lesson.lessonId, { orderIndex: event.target.value })} /></div>
          </div>
          <div className="mt-3"><StatusFields name={`lesson-status-${lesson.lessonId}`} lesson={lesson} onChange={status => updateLessonLocal(section.sectionId, lesson.lessonId, withStatus(lesson, status))} /></div>
          <div className="row g-2 align-items-end mt-2"><div className="col"><label className="form-label">Thay file bài học</label><input className="form-control" type="file" accept={getAcceptAttribute(lesson.resourceType)} onChange={event => event.target.files?.[0] && replaceFile(lesson, event.target.files[0])} />
            {lesson.fileUrl || lesson.originalFileName ? (
              <div className="mt-1 small">
                <span className="text-muted">File hiện tại: {lesson.originalFileName || lesson.fileUrl}</span>{' '}
                {lesson.resourceExists !== false ? (
                  <span className="badge bg-success ms-1">✓ Trạng thái: Sẵn sàng</span>
                ) : (
                  <span className="badge bg-danger ms-1">⚠️ Trạng thái: Tệp không còn tồn tại. Vui lòng tải lại tài nguyên.</span>
                )}
              </div>
            ) : (
              <small className="text-muted">File hiện tại: Chưa có file</small>
            )}
          </div>
            <div className="col-auto d-flex gap-2"><button type="button" className="btn btn-sm btn-outline-primary" disabled={busy} onClick={() => run(() => api.put(`/lessons/${lesson.lessonId}`, lessonPayload(lesson)), 'Đã lưu bài học.')}><Save size={15} aria-hidden="true" /> Lưu</button>
              <button type="button" title="Xóa bài học" aria-label={`Xóa bài học ${lesson.title}`} className="btn btn-sm btn-outline-danger" disabled={busy} onClick={() => setConfirmation({ title: 'Xóa bài học?', message: `Bài “${lesson.title}” sẽ bị xóa khỏi curriculum.`, confirmLabel: 'Xóa bài học', danger: true, action: () => run(() => api.delete(`/sections/${section.sectionId}`), 'Đã xóa bài học.') })}><Trash2 size={15} aria-hidden="true" /></button></div></div>
        </div>)}

        {(() => { const draft = lessonDrafts[section.sectionId] || { ...emptyLesson, orderIndex: (section.lessons?.length || 0) + 1 }; return <div className="bg-light rounded p-3 mt-3">
          <h3 className="h6"><FilePlus2 size={17} aria-hidden="true" /> Thêm bài học</h3><p className="small text-muted">Nhập thông tin cơ bản; có thể tải file ngay hoặc bổ sung sau.</p>
          <div className="row g-3">
            <div className="col-md-4"><label className="form-label">Tiêu đề bài học</label><input className="form-control" placeholder="Ví dụ: Cài đặt môi trường" value={draft.title} onChange={event => updateDraft(section.sectionId, { title: event.target.value })} /></div>
            <div className="col-md-2"><label className="form-label">Loại tài nguyên</label><select className="form-select" value={draft.resourceType} onChange={event => updateDraft(section.sectionId, { resourceType: event.target.value })}><option value="Video">Video</option><option value="Image">Ảnh (Image)</option><option value="Pdf">PDF</option><option value="PowerPoint">PowerPoint</option><option value="Document">Tài liệu</option><option value="File">File khác</option><option value="None">Không file</option></select></div>
            <div className="col-md-2"><label className="form-label">Thời lượng</label><input className="form-control" type="number" min="0" placeholder="Giây" value={draft.duration} onChange={event => updateDraft(section.sectionId, { duration: event.target.value })} /></div>
            <div className="col-md-2"><label className="form-label">Thứ tự bài học</label><input className="form-control" type="number" min="1" value={draft.orderIndex} onChange={event => updateDraft(section.sectionId, { orderIndex: event.target.value })} /></div>
            <div className="col-12"><StatusFields name={`new-lesson-status-${section.sectionId}`} lesson={draft} onChange={status => updateDraft(section.sectionId, withStatus(draft, status))} /></div>
            <div className="col"><label className="form-label">File bài học</label><input className="form-control" type="file" accept={getAcceptAttribute(draft.resourceType)} onChange={event => setFiles(value => ({ ...value, [section.sectionId]: event.target.files?.[0] || null }))} /><small className="text-muted">Video, tài liệu hoặc file bổ trợ; có thể để trống.</small></div>
            <div className="col-auto align-self-end"><button type="button" className="btn btn-primary" disabled={busy} onClick={() => saveLesson(section.sectionId)}><Plus size={17} aria-hidden="true" /> Thêm bài học</button></div>
          </div>
        </div>; })()}
      </div>
    </article>)}
    <ConfirmModal open={Boolean(confirmation)} title={confirmation?.title} message={confirmation?.message} confirmLabel={confirmation?.confirmLabel} danger={confirmation?.danger} loading={busy} onCancel={() => !busy && setConfirmation(null)} onConfirm={confirmAction} />
  </section>;
});

export default CurriculumBuilder;
