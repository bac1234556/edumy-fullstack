import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import './MyLearning.css';
import CourseThumbnail from '../components/CourseThumbnail';

export default function MyLearning() {
  const [courses, setCourses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [filter, setFilter] = useState('All'); // All, NotStarted, InProgress, Completed
  const navigate = useNavigate();

  const load = () => api.get('/courses/enrolled').then(({ data }) => { setCourses(data || []); setError(''); })
    .catch(() => setError('Không thể tải các khóa học của bạn.')).finally(() => setLoading(false));

  useEffect(() => {
    load();
    window.addEventListener('focus', load);
    return () => window.removeEventListener('focus', load);
  }, []);

  const handleContinueLearning = async (courseId) => {
    try {
      const { data } = await api.get(`/my-courses/${courseId}/continue-lesson`);
      if (data.lessonId) {
        navigate(`/my-courses/${courseId}/learn?lessonId=${data.lessonId}`);
      } else {
        navigate(`/my-courses/${courseId}/learn`);
      }
    } catch {
      navigate(`/my-courses/${courseId}/learn`);
    }
  };

  if (loading) return <div className="container text-center py-5">Đang tải...</div>;
  if (error) return <div className="container text-center py-5 text-danger">{error}</div>;

  const filteredCourses = courses.filter(item => {
    const percent = item.progressPercentage || 0;
    if (filter === 'NotStarted') return percent === 0;
    if (filter === 'InProgress') return percent > 0 && percent < 100;
    if (filter === 'Completed') return percent === 100;
    return true;
  });

  return <div className="my-learning-page">
    <header className="my-learning-header"><div className="container">
      <h1>My learning</h1>
      <div className="learning-tabs gap-2 d-flex flex-wrap">
        <button className={`btn btn-sm ${filter === 'All' ? 'btn-primary' : 'btn-outline-secondary border-0'}`} onClick={() => setFilter('All')}>Tất cả</button>
        <button className={`btn btn-sm ${filter === 'NotStarted' ? 'btn-primary' : 'btn-outline-secondary border-0'}`} onClick={() => setFilter('NotStarted')}>Chưa học</button>
        <button className={`btn btn-sm ${filter === 'InProgress' ? 'btn-primary' : 'btn-outline-secondary border-0'}`} onClick={() => setFilter('InProgress')}>Đang học</button>
        <button className={`btn btn-sm ${filter === 'Completed' ? 'btn-primary' : 'btn-outline-secondary border-0'}`} onClick={() => setFilter('Completed')}>Đã hoàn thành</button>
      </div>
    </div></header>
    <div className="container mt-4 mb-5">
      {filteredCourses.length === 0 ? <div className="empty-learning text-center p-5 border rounded bg-white">
        <h3>Không tìm thấy khóa học nào phù hợp.</h3><Link to="/courses" className="btn btn-primary mt-3">Khám phá khóa học</Link>
      </div> : <div className="enrolled-grid">{filteredCourses.map(item => {
        return <article className="enrolled-card hover-3d" key={item.courseId}>
          <div className="card-img-wrapper">
            <div style={{ cursor: 'pointer' }} onClick={() => handleContinueLearning(item.courseId)} aria-label={`Vào học ${item.title}`}>
              <CourseThumbnail src={item.thumbnailUrl} categoryName={item.categoryName} alt={item.title} className="card-img-top" /><div className="play-overlay-small">▶</div>
            </div>
          </div>
          <div className="card-body">
            <h5 className="card-title" style={{ cursor: 'pointer' }} onClick={() => handleContinueLearning(item.courseId)}>{item.title}</h5>
            <p className="instructor-name">{item.instructorName || item.fullName || 'Giảng viên'}</p>
            <div className="progress-container"><div className="progress-bar" style={{ width: `${item.progressPercentage || 0}%` }} /></div>
            <span className="progress-text">{item.completedLessons || 0}/{item.totalLessons || 0} bài giảng • {item.progressPercentage || 0}% hoàn thành</span>
            <div className="learning-card-actions">
              <button className="btn btn-primary btn-sm" onClick={() => handleContinueLearning(item.courseId)}>Tiếp tục học</button>
              <Link className="btn btn-outline-primary btn-sm" to={`/courses/${item.courseId}#reviews`}>Đánh giá</Link>
            </div>
          </div>
        </article>;
      })}</div>}
    </div>
  </div>;
}
