import { useContext, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams, useLocation } from 'react-router-dom';
import { FileText, Monitor, PlayCircle, Star, MessageSquare } from 'lucide-react';
import { toast } from 'react-hot-toast';
import api from '../api/axiosConfig';
import { AuthContext } from '../context/AuthContext';
import { formatCurrencyVN } from '../utils/format';
import './CourseDetail.css';
import ConfirmModal from '../components/ConfirmModal';
import CourseThumbnail from '../components/CourseThumbnail';

const durationText = seconds => seconds ? `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}` : '—';

export default function CourseDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const { user, isStudent, isInstructor, isAdmin } = useContext(AuthContext);
  const [course, setCourse] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [isEnrolled, setIsEnrolled] = useState(false);
  const [inWishlist, setInWishlist] = useState(false);
  const [busy, setBusy] = useState(false);
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState('');
  const [replyText, setReplyText] = useState({});
  const [courseComment, setCourseComment] = useState('');
  const [confirmUnpublic, setConfirmUnpublic] = useState(false);
  const [similarCourses, setSimilarCourses] = useState([]);
  const [bundleCourses, setBundleCourses] = useState(null);

  const load = async () => {
    try {
      const { data } = await api.get(`/courses/${id}`);
      setCourse(data);
      setError('');
      
      // Fetch recommendations
      api.get(`/courses/${id}/similar`).then(res => setSimilarCourses(res.data)).catch(() => {});
      api.get(`/courses/${id}/bundle`).then(res => setBundleCourses(res.data)).catch(() => {});
    }
    catch { setError('Không thể tải khóa học này.'); }
    finally { setLoading(false); }
  };
  useEffect(() => { setLoading(true); load(); }, [id]);

  useEffect(() => {
    if (!course || !location.hash) return;
    const targetId = location.hash.replace('#', '');
    const timer = setTimeout(() => {
      const el = document.getElementById(targetId);
      if (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.classList.add('highlight-target');
        setTimeout(() => el.classList.remove('highlight-target'), 2500);
      }
    }, 150);
    return () => clearTimeout(timer);
  }, [course, location.hash]);

  useEffect(() => {
    if (!user || !isStudent) return;
    Promise.allSettled([api.get('/courses/enrolled'), api.get(`/wishlist/check/${id}`)]).then(([enrolled, wish]) => {
      if (enrolled.status === 'fulfilled') setIsEnrolled((enrolled.value.data || []).some(c => Number(c.courseId) === Number(id)));
      if (wish.status === 'fulfilled') setInWishlist(Boolean(wish.value.data?.inWishlist));
    });
  }, [id, user, isStudent]);

  const stats = useMemo(() => {
    const lessons = (course?.sections || []).flatMap(s => s.lessons || []);
    const videos = lessons.filter(l => (l.resourceType || '').toLowerCase() === 'video');
    return { sections: course?.sections?.length || 0, lessons: lessons.length, videos: videos.length, documents: lessons.length - videos.length, seconds: videos.reduce((sum, l) => sum + (l.duration || 0), 0) };
  }, [course]);
  const isOwnerInstructor = isInstructor && Number(user?.id) === Number(course?.instructorId);
  const canRespond = isAdmin || isOwnerInstructor;

  const addToCart = async (buyNow = false) => {
    if (!user) {
      toast.info('Vui lòng đăng nhập để mua khóa học');
      return navigate(`/login?returnUrl=${encodeURIComponent(location.pathname + location.search)}`);
    }
    if (!isStudent) return toast.error('Chỉ học viên mới có thể mua khóa học.');
    setBusy(true);
    try { await api.post(`/cart/add/${id}`); if (buyNow) { const { data } = await api.post('/orders/checkout', { couponCode: null }); navigate(data.paymentUrl); } else toast.success('Đã thêm vào giỏ hàng.'); }
    catch (e) { toast.error(e.response?.data?.message || 'Không thể thêm khóa học.'); }
    finally { setBusy(false); }
  };
  const toggleWishlist = async () => {
    if (!user) {
      toast.info('Vui lòng đăng nhập để thêm khóa học vào Wishlist');
      return navigate(`/login?returnUrl=${encodeURIComponent(location.pathname + location.search)}`);
    }
    if (busy) return;
    setBusy(true);
    try {
      if (inWishlist) {
        await api.delete(`/wishlist/remove/${id}`);
        setInWishlist(false);
        toast.success('Đã xóa khỏi Wishlist.');
      } else {
        await api.post(`/wishlist/add/${id}`);
        setInWishlist(true);
        toast.success('Đã thêm vào Wishlist.');
      }
    } catch (e) {
      toast.error(e.response?.data?.message || 'Không thể cập nhật Wishlist.');
    } finally {
      setBusy(false);
    }
  };
  const submitReview = async e => {
    e.preventDefault(); setBusy(true);
    try { await api.post(`/courses/${id}/reviews`, { rating, comment: comment.trim() }); setComment(''); toast.success('Đã gửi đánh giá.'); await load(); }
    catch (e2) { toast.error(e2.response?.data?.message || 'Không thể gửi đánh giá.'); } finally { setBusy(false); }
  };
  const submitReply = async reviewId => {
    const content = replyText[reviewId]?.trim(); if (!content) return;
    setBusy(true); try { await api.post(`/reviews/${reviewId}/replies`, { content }); setReplyText(v => ({...v,[reviewId]:''})); await load(); }
    catch (e) { toast.error(e.response?.data?.message || 'Không thể phản hồi.'); } finally { setBusy(false); }
  };
  const submitCourseComment = async e => {
    e.preventDefault(); setBusy(true); try { await api.post(`/courses/${id}/comments`, { content: courseComment.trim() }); setCourseComment(''); await load(); }
    catch (err) { toast.error(err.response?.data?.message || 'Không thể gửi bình luận.'); } finally { setBusy(false); }
  };
  const unpublic = async () => {
    setBusy(true); try { await api.put(`/admin/courses/${id}/status`, JSON.stringify('Draft'), { headers:{'Content-Type':'application/json'} }); toast.success('Đã unpublic khóa học.'); await load(); }
    catch (e) { toast.error(e.response?.data?.message || 'Không thể cập nhật trạng thái.'); } finally { setBusy(false); }
    setConfirmUnpublic(false);
  };

  if (loading) return <div className="container py-5 text-center">Đang tải...</div>;
  if (error || !course) return <div className="container py-5 text-center text-danger">{error}</div>;

  return <div className="course-detail-page">
    <header className="course-header-dark"><div className="container header-container"><div className="header-content">
      <div className="breadcrumbs">{course.category?.name || 'Khóa học'}</div><h1 className="course-title-main">{course.title}</h1>
      <p className="course-subtitle">{course.description}</p>
      <div className="course-meta"><span className="rating-score">{course.averageRating || 0}</span><Star size={15} fill="#eb8a2f" color="#eb8a2f"/><a className="ratings-link" href="#reviews">({course.reviews?.length || 0} đánh giá)</a><span>{course.enrollmentCount || course.studentCount || 0} học viên</span></div>
      <div className="instructor-meta">Created by <Link className="fw-bold text-white" to={`/users/${course.instructorId}`}>{course.instructor?.fullName || 'Giảng viên'}</Link></div>
      <div className="lang-meta">Cập nhật {new Date(course.updatedAt).toLocaleDateString('vi-VN')}</div>
      {isAdmin && course.status === 'Published' && <button className="btn btn-danger btn-sm mt-3" disabled={busy} onClick={()=>setConfirmUnpublic(true)}>Unpublic</button>}
    </div></div></header>

    <div className="container main-content-grid"><main className="left-column">
      {course.categories && course.categories.length > 0 && (
        <section className="course-categories-section mt-4 mb-4 p-4 bg-light rounded">
          <h3 className="h5 mb-3" style={{ fontWeight: 600 }}>Chủ đề</h3>
          <div className="d-flex flex-wrap gap-2">
            {course.categories.map(cat => (
              <Link 
                key={cat.categoryId} 
                to={`/courses?categoryId=${cat.categoryId}`}
                className="btn btn-sm btn-outline-primary rounded-pill px-3"
              >
                {cat.name}
              </Link>
            ))}
          </div>
        </section>
      )}
      <section className="course-description-section mt-4 mb-5 p-4 bg-light rounded"><h2>Mô tả khóa học</h2><p style={{whiteSpace:'pre-line'}}>{course.description || 'Chưa có mô tả.'}</p></section>
      <section className="course-curriculum"><h2>Course content</h2>
        {stats.sections === 0 ? <p>Chưa có nội dung cho khóa học này</p> : <>
          <div className="curriculum-meta">{stats.sections} chương • {stats.lessons} bài/file{stats.seconds ? ` • ${durationText(stats.seconds)}` : ''}</div>
          <div className="accordion-list">{course.sections.map(section => <div className="accordion-item-udemy" key={section.sectionId}>
            <div className="accordion-header-udemy"><strong>{section.title}</strong><span>{section.lessons?.length || 0} bài</span></div>
            <div className="accordion-body-udemy">{(section.lessons || []).length === 0 ? <span>Chương chưa có nội dung</span> : section.lessons.map(lesson => <div className="lesson-item" key={lesson.lessonId}>
              {lesson.resourceType === 'Video' ? <PlayCircle size={14}/> : <FileText size={14}/>}<span className="lesson-title">{lesson.title}</span>
              {lesson.isDraft && <span className="badge bg-warning text-dark">Bản nháp</span>}<span className="lesson-duration">{lesson.duration ? durationText(lesson.duration) : lesson.resourceType}</span>
            </div>)}</div>
          </div>)}</div>
        </>}
      </section>

      {/* Similar Courses Section */}
      {similarCourses && similarCourses.length > 0 && (
        <section className="similar-courses-section mt-5 mb-5 p-4 bg-light rounded">
          <h2 className="mb-4" style={{ fontWeight: 600 }}>Khóa học tương tự gợi ý bởi AI</h2>
          <div className="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3">
            {similarCourses.map(c => (
              <div className="col" key={c.courseId}>
                <div className="card h-100 shadow-sm border-0 transition-hover">
                  <Link to={`/courses/${c.courseId}`} className="text-decoration-none text-dark">
                    <div style={{ height: '140px', overflow: 'hidden' }}>
                      <CourseThumbnail src={c.thumbnailUrl} alt={c.title} />
                    </div>
                    <div className="card-body p-3">
                      <h3 className="card-title h6 text-truncate mb-1" title={c.title}>{c.title}</h3>
                      <p className="text-muted small mb-2">{c.instructorName}</p>
                      <div className="d-flex align-items-center gap-1 mb-2">
                        <span className="text-warning fw-bold small">{c.averageRating || 0}</span>
                        <Star size={12} fill="#eb8a2f" color="#eb8a2f"/>
                      </div>
                      <div className="fw-bold">{formatCurrencyVN(c.price)}</div>
                    </div>
                  </Link>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      {/* Bundle Recommendation Section */}
      {bundleCourses && bundleCourses.items && bundleCourses.items.length > 0 && (
        <section className="bundle-courses-section mt-5 mb-5 p-4 bg-light rounded border border-primary border-opacity-25">
          <div className="d-flex align-items-center gap-2 mb-3">
            <span className="badge bg-primary">AI Bundle Package</span>
            <h2 className="h4 mb-0" style={{ fontWeight: 700 }}>Học viên mua khóa học này cũng thường mua</h2>
          </div>
          <p className="text-muted small mb-4">Gói combo khóa học đề xuất tự động dựa trên hành vi đăng ký.</p>
          <div className="row row-cols-1 row-cols-md-3 g-3">
            {bundleCourses.items.map(c => (
              <div className="col" key={c.courseId}>
                <div className="card h-100 shadow-sm border-0">
                  <Link to={`/courses/${c.courseId}`} className="text-decoration-none text-dark">
                    <div style={{ height: '100px', overflow: 'hidden' }}>
                      <CourseThumbnail src={c.thumbnailUrl} alt={c.title} />
                    </div>
                    <div className="card-body p-3">
                      <h3 className="card-title h6 text-truncate mb-1" title={c.title}>{c.title}</h3>
                      <p className="text-muted small mb-2">{c.instructorName}</p>
                      <div className="fw-bold">{formatCurrencyVN(c.price)}</div>
                    </div>
                  </Link>
                </div>
              </div>
            ))}
          </div>
        </section>
      )}

      <section className="course-reviews mt-5" id="reviews"><h2>Feedback & comments</h2>
        <div className="reviews-list mt-4">{(course.reviews || []).length === 0 && (course.comments || []).length === 0 ? <p>Chưa có đánh giá hoặc bình luận.</p> : <>
          {(course.reviews || []).map(review => <article className="review-card mb-4 p-3 border rounded" id={`review-${review.reviewId}`} key={review.reviewId}>
            <div className="d-flex justify-content-between"><Link className="d-flex gap-2 align-items-center fw-bold" to={`/users/${review.userId}`}>{review.user?.avatarUrl && <img src={review.user.avatarUrl} width="32" height="32" className="rounded-circle"/>}{review.user?.fullName || 'Người dùng'}</Link><small>{new Date(review.createdAt).toLocaleDateString('vi-VN')}</small></div>
            <div className="stars my-2 d-flex align-items-center gap-2">
              <span>{'★'.repeat(review.rating)}{'☆'.repeat(5-review.rating)}</span>
              {review.sentimentLabel && (
                <span className={`badge ${review.sentimentLabel === 'Positive' ? 'bg-success' : review.sentimentLabel === 'Negative' ? 'bg-danger' : 'bg-warning text-dark'}`} style={{fontSize: '11px'}}>
                  {review.sentimentLabel === 'Positive' ? 'Tích cực' : review.sentimentLabel === 'Negative' ? 'Tiêu cực' : 'Trung lập'}
                </span>
              )}
            </div>
            <p>{review.comment}</p>
            {(review.replies || []).map(reply => <div className="ms-4 mt-2 p-2 bg-light rounded" id={`reply-${reply.reviewReplyId}`} key={reply.reviewReplyId}><Link to={`/users/${reply.userId}`} className="fw-bold">{reply.user?.fullName}</Link> <span className="badge bg-secondary">{reply.user?.role}</span><p className="mb-0">{reply.content}</p></div>)}
            {canRespond && <div className="d-flex gap-2 mt-3"><input className="form-control" maxLength="2000" placeholder="Phản hồi" value={replyText[review.reviewId] || ''} onChange={e=>setReplyText(v=>({...v,[review.reviewId]:e.target.value}))}/><button className="btn btn-outline-primary" disabled={busy} onClick={()=>submitReply(review.reviewId)}>Gửi</button></div>}
          </article>)}
          {(course.comments || []).map(item => <article className="review-card mb-3 p-3 border rounded" key={`comment-${item.courseCommentId}`}><Link className="fw-bold" to={`/users/${item.userId}`}>{item.user?.fullName}</Link> <span className="badge bg-secondary">{item.user?.role}</span><p className="mb-0 mt-2">{item.content}</p></article>)}
        </>}</div>
        {isEnrolled && <form className="leave-review-form mt-4 p-4 bg-light rounded" onSubmit={submitReview}><h4>Đánh giá khóa học</h4><select className="form-select mb-2" value={rating} onChange={e=>setRating(Number(e.target.value))}>{[5,4,3,2,1].map(v=><option value={v} key={v}>{v} sao</option>)}</select><textarea className="form-control mb-2" required maxLength="2000" value={comment} onChange={e=>setComment(e.target.value)}/><button className="btn btn-primary" disabled={busy}>Gửi đánh giá</button></form>}
        {canRespond && <form className="leave-review-form mt-4 p-4 bg-light rounded" onSubmit={submitCourseComment}><h4>Bình luận với vai trò {isAdmin ? 'Admin' : 'Instructor'}</h4><textarea className="form-control mb-2" required maxLength="2000" value={courseComment} onChange={e=>setCourseComment(e.target.value)}/><button className="btn btn-primary" disabled={busy}>Gửi bình luận</button></form>}
      </section>
    </main>

    <aside className="right-column"><div className="sidebar-card"><div className="sidebar-video"><CourseThumbnail src={course.thumbnailUrl} categoryName={course.category?.name} alt={course.title}/></div><div className="sidebar-content">
      <div className="price-big">{formatCurrencyVN(course.price)}</div>
      {isEnrolled ? (
        <button className="btn-udemy w-100" onClick={() => navigate(`/my-courses/${id}/learn`)}>Vào học</button>
      ) : isOwnerInstructor ? (
        <div className="d-flex flex-column gap-2">
          <button className="btn btn-primary w-100" onClick={() => navigate(`/instructor/courses/${id}/edit`)}>Chỉnh sửa khóa học</button>
          <button className="btn btn-outline-primary w-100" onClick={() => navigate(`/instructor/courses/${id}/preview`)}>Xem trước nội dung</button>
          <button className="btn btn-outline-info w-100 d-flex align-items-center justify-content-center gap-2" onClick={() => navigate(`/instructor/courses/${id}/preview?tab=discussions`)}><MessageSquare size={16}/> Quản lý hỏi đáp</button>
        </div>
      ) : isAdmin ? (
        <div className="d-flex flex-column gap-2">
          <button className="btn btn-primary w-100" onClick={() => navigate(`/admin/courses/${id}/preview`)}>Kiểm tra nội dung</button>
        </div>
      ) : isInstructor ? (
        <div className="text-muted small text-center py-2">Bạn đang đăng nhập bằng tài khoản Giảng viên.</div>
      ) : (
        <>
          <button className="btn-udemy-primary w-100 mb-2" disabled={busy} onClick={() => addToCart(false)}>Thêm vào giỏ</button>
          <button className="btn-udemy-outline w-100 mb-2" disabled={busy} onClick={() => addToCart(true)}>Mua ngay</button>
          <button type="button" className="btn btn-outline-secondary w-100" disabled={busy} onClick={toggleWishlist} aria-label={inWishlist ? 'Xóa khỏi Wishlist' : 'Thêm vào Wishlist'}>{busy ? 'Đang xử lý...' : inWishlist ? 'Đã có trong Wishlist' : 'Thêm vào Wishlist'}</button>
        </>
      )}
      <div className="includes-list mt-3"><h4>This course includes:</h4>{stats.lessons === 0 ? <p>Chưa có dữ liệu nội dung</p> : <ul><li><Monitor size={14}/> {stats.sections} chương</li><li><PlayCircle size={14}/> {stats.videos} video{stats.seconds ? ` (${durationText(stats.seconds)})` : ''}</li><li><FileText size={14}/> {stats.documents} tài liệu/file</li></ul>}</div>
    </div></div></aside></div>
    <ConfirmModal open={confirmUnpublic} title="Chuyển khóa học về Draft?" message="Khóa học sẽ không còn xuất hiện trong danh sách Published." confirmLabel="Chuyển về Draft" danger loading={busy} onCancel={()=>!busy&&setConfirmUnpublic(false)} onConfirm={unpublic}/>
  </div>;
}
