export const COURSE_STATUSES = ['Draft', 'Published', 'Unpublished', 'PendingApproval'];

export function validateCourseForm(form, includeStatus = false) {
  const errors = [];
  if (!form.title?.trim()) errors.push('Tiêu đề khóa học là bắt buộc.');
  if (!form.description?.trim()) errors.push('Mô tả khóa học là bắt buộc.');
  if (!form.categoryIds || form.categoryIds.length === 0) errors.push('Bạn phải chọn ít nhất 1 danh mục.');
  if (!Number.isFinite(Number(form.price)) || Number(form.price) < 0) errors.push('Giá khóa học không hợp lệ.');
  if (includeStatus && !COURSE_STATUSES.includes(form.status)) errors.push('Trạng thái khóa học không hợp lệ.');
  return errors;
}

import CourseThumbnail from './CourseThumbnail';

export default function CourseBasicInfoForm({ form, categories, thumbnailFile, onChange, onFileChange, onAiSuggest, aiLoading, showStatus = false }) {
  return <div className="step-content">
    <h2>Thông tin cơ bản</h2>
    <div className="form-group">
      <label><span>Tiêu đề khóa học</span>{onAiSuggest && <button type="button" onClick={onAiSuggest} className="ai-btn-inline" disabled={aiLoading || !form.title}>{aiLoading ? 'Đang phân loại...' : '✨ Gợi ý danh mục bằng AI'}</button>}</label>
      <input name="title" value={form.title} onChange={onChange} required minLength="3" maxLength="200" />
    </div>
    <div className="form-group">
      <label>Mô tả</label>
      <textarea name="description" value={form.description} onChange={onChange} required minLength="10" rows="5" />
    </div>
    <div className="form-group"><label>Giá (VNĐ)</label><input type="number" name="price" value={form.price} onChange={onChange} min="0" step="1000" required /></div>
    
    <div className="form-group">
      <label>Danh mục * (chọn một hoặc nhiều)</label>
      <div className="d-flex flex-wrap gap-2 p-2 border rounded bg-white" style={{ maxHeight: '180px', overflowY: 'auto' }}>
        {categories.map(category => {
          const isChecked = (form.categoryIds || []).map(String).includes(String(category.categoryId));
          return (
            <div key={category.categoryId} className="form-check me-3 mb-1">
              <input
                className="form-check-input"
                type="checkbox"
                id={`cat-chk-${category.categoryId}`}
                checked={isChecked}
                onChange={() => {
                  const currentIds = form.categoryIds || [];
                  const idStr = String(category.categoryId);
                  let newIds;
                  if (currentIds.map(String).includes(idStr)) {
                    newIds = currentIds.filter(id => String(id) !== idStr);
                  } else {
                    newIds = [...currentIds, Number(category.categoryId)];
                  }
                  onChange({
                    target: {
                      name: 'categoryIds',
                      value: newIds
                    }
                  });
                }}
              />
              <label className="form-check-label small" htmlFor={`cat-chk-${category.categoryId}`}>
                {category.name}
              </label>
            </div>
          );
        })}
      </div>
    </div>

    <div className="form-group">
      <label>Ảnh đại diện</label>
      {(form.thumbnailUrl || thumbnailFile) && <CourseThumbnail src={form.thumbnailUrl} file={thumbnailFile} categoryName={categories.find(category => (form.categoryIds || []).map(String).includes(String(category.categoryId)))?.name} alt="Ảnh đại diện hiện tại" className="d-block mb-2" style={{width: 220, borderRadius: 6}} />}
      <input type="file" accept="image/jpeg,image/png,image/webp" onChange={onFileChange} />
      <small className="text-muted">JPG, PNG hoặc WebP; tối đa 5 MB.</small>
    </div>
    {showStatus && <div className="form-group"><label>Trạng thái</label><select name="status" value={form.status} onChange={onChange} required>{COURSE_STATUSES.map(status => <option key={status} value={status}>{status}</option>)}</select></div>}
  </div>;
}
