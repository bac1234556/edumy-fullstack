import { useCallback, useEffect, useState } from 'react';
import { ArrowLeft, MessageCircle, Plus, Send, X } from 'lucide-react';
import { toast } from 'react-hot-toast';
import api from '../api/axiosConfig';
import ConfirmModal from './ConfirmModal';

const hasRole = (user, role) => Array.isArray(user?.role)
  ? user.role.some(value => String(value).toLowerCase() === role.toLowerCase())
  : String(user?.role || '').toLowerCase() === role.toLowerCase();
const apiMessage = (error, fallback) => {
  const payload = error.response?.data;
  if (payload?.code === 'NOT_ENROLLED') return 'Bạn cần đăng ký khóa học để sử dụng chức năng này.';
  if (Array.isArray(payload?.errors)) return payload.errors.join(' ');
  if (payload?.errors && typeof payload.errors === 'object') return Object.values(payload.errors).flat().join(' ');
  return payload?.message || fallback;
};

export default function CourseDiscussionDrawer({ courseId, open, onClose, user, initialThreadId, initialMessageId }) {
  const [threads, setThreads] = useState([]);
  const [selectedThread, setSelectedThread] = useState(null);
  const [showCreate, setShowCreate] = useState(false);
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [reply, setReply] = useState('');
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [formError, setFormError] = useState('');
  const [statusConfirm, setStatusConfirm] = useState(false);

  const loadThreads = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const { data } = await api.get(`/courses/${courseId}/discussions`);
      setThreads(data.items || []);
    } catch (requestError) {
      setError(apiMessage(requestError, 'Không thể tải danh sách hỏi đáp.'));
    } finally { setLoading(false); }
  }, [courseId]);

  const openThread = useCallback(async (threadId, targetMsgId) => {
    setLoading(true);
    setError('');
    try {
      const { data } = await api.get(`/discussions/${threadId}`);
      setSelectedThread(data);
      setReply('');
      if (targetMsgId) {
        setTimeout(() => {
          const el = document.getElementById(`qa-message-${targetMsgId}`);
          if (el) {
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            el.classList.add('highlight-target');
            setTimeout(() => el.classList.remove('highlight-target'), 2500);
          }
        }, 150);
      }
    } catch (requestError) {
      setError(apiMessage(requestError, 'Không thể tải thảo luận.'));
    } finally { setLoading(false); }
  }, []);

  useEffect(() => {
    if (!open) return;
    setSelectedThread(null);
    setShowCreate(false);
    loadThreads();
    if (initialThreadId) {
      openThread(initialThreadId, initialMessageId);
    }
  }, [loadThreads, open, initialThreadId, initialMessageId, openThread]);

  const createThread = async event => {
    event.preventDefault();
    if (submitting) return;
    const cleanTitle = title.trim();
    const cleanContent = content.trim();
    if (cleanTitle.length < 5 || cleanTitle.length > 200) return setFormError('Tiêu đề phải có từ 5 đến 200 ký tự.');
    if (cleanContent.length < 10 || cleanContent.length > 4000) return setFormError('Nội dung phải có từ 10 đến 4000 ký tự.');
    setSubmitting(true);
    setFormError('');
    try {
      const { data } = await api.post(`/courses/${courseId}/discussions`, { title: cleanTitle, content: cleanContent });
      setTitle('');
      setContent('');
      setShowCreate(false);
      await loadThreads();
      await openThread(data.id);
      toast.success('Đã gửi câu hỏi thành công.');
    } catch (requestError) {
      setFormError(apiMessage(requestError, 'Không thể tạo câu hỏi.'));
    } finally { setSubmitting(false); }
  };

  const sendReply = async event => {
    event.preventDefault();
    if (submitting) return;
    const cleanReply = reply.trim();
    if (cleanReply.length < 2 || cleanReply.length > 4000) return setFormError('Phản hồi phải có từ 2 đến 4000 ký tự.');
    setSubmitting(true);
    setFormError('');
    try {
      await api.post(`/discussions/${selectedThread.id}/messages`, { content: cleanReply });
      setReply('');
      await openThread(selectedThread.id);
      await loadThreads();
      toast.success('Đã gửi phản hồi.');
    } catch (requestError) {
      setFormError(apiMessage(requestError, 'Không thể gửi phản hồi.'));
    } finally { setSubmitting(false); }
  };

  const updateStatus = async () => {
    if (!selectedThread || submitting) return;
    setSubmitting(true);
    try {
      await api.put(`/discussions/${selectedThread.id}/status`, { isClosed: !selectedThread.isClosed });
      setStatusConfirm(false);
      await openThread(selectedThread.id);
      await loadThreads();
      toast.success(selectedThread.isClosed ? 'Đã mở lại thảo luận.' : 'Đã đóng thảo luận.');
    } catch (requestError) {
      setFormError(apiMessage(requestError, 'Bạn không có quyền đổi trạng thái thảo luận.'));
    } finally { setSubmitting(false); }
  };

  if (!open) return null;
  const canModerate = hasRole(user, 'Instructor') || hasRole(user, 'Admin');
  const startCreate = () => { setSelectedThread(null); setShowCreate(true); setFormError(''); };
  return <aside className="qa-drawer" aria-label="Hỏi đáp khóa học">
    <header className="qa-drawer-header">
      <div><h2><MessageCircle size={22} aria-hidden="true" /> Hỏi đáp khóa học</h2><p>Trao đổi với giảng viên và các học viên</p></div>
      <button type="button" className="qa-close-button" onClick={onClose} aria-label="Đóng hỏi đáp"><X /></button>
    </header>
    <div className="qa-drawer-body">
      {error && <div className="qa-inline-error" role="alert">{error}<button type="button" className="btn btn-sm btn-outline-danger" onClick={selectedThread ? () => openThread(selectedThread.id) : loadThreads}>Thử lại</button></div>}
      {loading && <div className="qa-loading" role="status"><span className="spinner-border spinner-border-sm" /> Đang tải hỏi đáp...</div>}

      {!loading && selectedThread && <section className="qa-thread-detail">
        <button type="button" className="qa-back-button" onClick={() => { setSelectedThread(null); setFormError(''); }}><ArrowLeft size={17} /> Danh sách câu hỏi</button>
        <div className="qa-thread-heading">
          <div><h3>{selectedThread.title}</h3><p>Đặt bởi <strong>{selectedThread.createdBy?.fullName}</strong> <span className="qa-role">{selectedThread.createdBy?.role}</span> · {new Date(selectedThread.createdAt).toLocaleString('vi-VN')}</p></div>
          <span className={`qa-status ${selectedThread.isClosed ? 'closed' : 'open'}`}>{selectedThread.isClosed ? 'Đã đóng' : 'Đang mở'}</span>
        </div>
        {canModerate && <button type="button" className="btn btn-sm btn-outline-secondary mb-3" disabled={submitting} onClick={() => setStatusConfirm(true)}>{selectedThread.isClosed ? 'Mở lại thread' : 'Đóng thread'}</button>}
        <div className="qa-message-list">
          {(selectedThread.messages || []).map(item => <article className={`qa-message ${item.isInstructorMessage ? 'instructor' : ''}`} id={`qa-message-${item.id}`} key={item.id}>
            <div className="qa-message-meta"><strong>{item.user?.fullName}</strong><span className="qa-role">{item.user?.role || (item.isInstructorMessage ? 'Instructor' : 'Student')}</span><time>{new Date(item.createdAt).toLocaleString('vi-VN')}</time></div>
            <p>{item.content}</p>
          </article>)}
        </div>
        {selectedThread.isClosed ? <div className="qa-closed-notice">Thảo luận đã đóng. Bạn vẫn có thể xem toàn bộ nội dung nhưng không thể gửi phản hồi mới.</div> : <form className="qa-reply-form" onSubmit={sendReply}>
          <label htmlFor="qa-reply">Phản hồi thảo luận</label>
          <textarea id="qa-reply" value={reply} onChange={event => { setReply(event.target.value); setFormError(''); }} minLength="2" maxLength="4000" rows="4" placeholder="Nhập phản hồi của bạn..." />
          {formError && <p className="qa-form-error" role="alert">{formError}</p>}
          <button className="btn btn-primary" disabled={submitting || reply.trim().length < 2}>{submitting ? 'Đang gửi...' : <><Send size={16} /> Gửi trả lời</>}</button>
        </form>}
      </section>}

      {!loading && !selectedThread && <section>
        <div className="qa-list-actions"><div><h3>Các câu hỏi gần đây</h3><p>Sắp xếp theo lần cập nhật mới nhất</p></div><button type="button" className="btn btn-primary" onClick={startCreate}><Plus size={17} /> Đặt câu hỏi</button></div>
        {showCreate && <form className="qa-new-thread" onSubmit={createThread}>
          <h3>Đặt câu hỏi mới</h3>
          <label htmlFor="qa-title">Tiêu đề câu hỏi</label><input id="qa-title" value={title} onChange={event => { setTitle(event.target.value); setFormError(''); }} minLength="5" maxLength="200" placeholder="Ví dụ: Làm sao hoàn thành bài tập chương 2?" />
          <label htmlFor="qa-content">Nội dung câu hỏi</label><textarea id="qa-content" value={content} onChange={event => { setContent(event.target.value); setFormError(''); }} minLength="10" maxLength="4000" rows="5" placeholder="Mô tả chi tiết phần bạn cần hỗ trợ..." />
          {formError && <p className="qa-form-error" role="alert">{formError}</p>}
          <div className="qa-form-actions"><button type="button" className="btn btn-outline-secondary" disabled={submitting} onClick={() => { setShowCreate(false); setFormError(''); }}>Hủy</button><button className="btn btn-primary" disabled={submitting || title.trim().length < 5 || content.trim().length < 10}>{submitting ? 'Đang gửi...' : 'Gửi câu hỏi'}</button></div>
        </form>}
        {!showCreate && threads.length === 0 ? <div className="qa-empty-state"><MessageCircle size={42} aria-hidden="true" /><h3>Chưa có câu hỏi nào</h3><p>Hãy là người đầu tiên đặt câu hỏi.</p><button type="button" className="btn btn-primary" onClick={startCreate}>Đặt câu hỏi</button></div> : !showCreate && <div className="qa-thread-list">{threads.map(thread => <button type="button" className="qa-thread-card" onClick={() => openThread(thread.id)} key={thread.id}>
          <span className="qa-thread-card-top"><strong>{thread.title}</strong><span className={`qa-status ${thread.isClosed ? 'closed' : 'open'}`}>{thread.isClosed ? 'Đã đóng' : 'Đang mở'}</span></span>
          <span className="qa-excerpt">{thread.excerpt || 'Chưa có nội dung'}</span>
          <span className="qa-thread-meta">{thread.createdBy?.fullName} · {thread.createdBy?.role} · {thread.answerCount ?? Math.max(0, (thread.messageCount || 1) - 1)} phản hồi · cập nhật {new Date(thread.updatedAt).toLocaleString('vi-VN')}</span>
        </button>)}</div>}
      </section>}
    </div>
    <ConfirmModal open={statusConfirm} title={selectedThread?.isClosed ? 'Mở lại thảo luận?' : 'Đóng thảo luận?'} message={selectedThread?.isClosed ? 'Học viên sẽ có thể gửi phản hồi mới.' : 'Mọi người vẫn xem được nội dung nhưng không thể gửi phản hồi mới.'} confirmLabel={selectedThread?.isClosed ? 'Mở lại' : 'Đóng thread'} danger={!selectedThread?.isClosed} loading={submitting} onCancel={() => !submitting && setStatusConfirm(false)} onConfirm={updateStatus} />
  </aside>;
}
