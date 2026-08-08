import React, { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { Users, BookOpen, DollarSign, Star, Sparkles, AlertTriangle, PlayCircle, Award, CheckCircle, MessageCircle } from 'lucide-react';
import { formatCurrencyVN } from '../utils/format';
import './InstructorDashboard.css';
import { toast } from 'react-hot-toast';
import ConfirmModal from '../components/ConfirmModal';
import CourseThumbnail from '../components/CourseThumbnail';

const sentimentClass = label => label === 'Positive' ? 'bg-success' : label === 'Negative' ? 'bg-danger' : label === 'Neutral' ? 'bg-warning text-dark' : 'bg-secondary';
const sentimentText = label => label === 'Unknown' ? 'Unknown / AI unavailable' : label;

function InstructorDashboard() {
  const [courses, setCourses] = useState([]);
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [activeTab, setActiveTab] = useState('courses'); // courses, analytics, reviews
  const [recentSales, setRecentSales] = useState([]);
  const [courseToDelete, setCourseToDelete] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const fetchData = async () => {
    setLoading(true);
    setError('');
    
    try {
      const coursesRes = await api.get('/courses/my-courses');
      setCourses(coursesRes.data || []);
    } catch (err) {
      console.error('Failed to load instructor courses', err);
      setCourses([]);
    }
    try { const salesRes = await api.get('/instructor/recent-sales'); setRecentSales(salesRes.data || []); } catch { setRecentSales([]); }

    let mergedStats = {
      totalCourses: 0,
      activeStudents: 0,
      monthlyRevenue: 0,
      aiQualityRating: null,
      recentReviews: [],
      revenueByDate: [],
      enrollmentByDate: [],
      sentimentStats: [],
      averageQualityScore: 0.0,
      recommendations: [],
      unansweredQasCount: 0,
      averageCompletionRate: 0.0,
      finalQuizAverageScore: 0.0,
      perCourseAnalytics: []
    };

    // 1. Get dashboard stats directly
    try {
      const dashboardStatsRes = await api.get('/instructor/dashboard-stats');
      if (dashboardStatsRes.data) {
        mergedStats = {
          ...mergedStats,
          totalCourses: dashboardStatsRes.data.totalCourses || 0,
          activeStudents: dashboardStatsRes.data.activeStudents || 0,
          monthlyRevenue: dashboardStatsRes.data.monthlyRevenue || 0,
          aiQualityRating: dashboardStatsRes.data.aiQualityRating !== undefined ? dashboardStatsRes.data.aiQualityRating : null
        };
      } else {
      }
    } catch (err) {
    }

    // 2. Get supporting stats
    try {
      const statsRes = await api.get('/instructor/stats');
      if (statsRes.data) {
        mergedStats = {
          ...mergedStats,
          recentReviews: statsRes.data.recentReviews || [],
          revenueByDate: statsRes.data.revenueByDate || [],
          enrollmentByDate: statsRes.data.enrollmentByDate || [],
          sentimentStats: statsRes.data.sentimentStats || [],
          averageQualityScore: statsRes.data.averageQualityScore || 0.0,
          recommendations: statsRes.data.recommendations || [],
          unansweredQasCount: statsRes.data.unansweredQasCount || 0,
          averageCompletionRate: statsRes.data.averageCompletionRate || 0.0,
          finalQuizAverageScore: statsRes.data.finalQuizAverageScore || 0.0,
          perCourseAnalytics: statsRes.data.perCourseAnalytics || []
        };
      }
    } catch (err) {
    }

    setStats(mergedStats);
    setLoading(false);
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleTriggerAnalysis = async (courseId) => {
    try {
      setLoading(true);
      await api.post(`/courses/${courseId}/status`, '"Analyzing"', {
        headers: { 'Content-Type': 'application/json' }
      });
      toast.success('AI content analysis triggered successfully!');
      fetchData();
    } catch (err) {
      toast.error('Failed to trigger AI content analysis.');
      setLoading(false);
    }
  };

  const deleteCourse = async () => {
    if (!courseToDelete || deleting) return;
    setDeleting(true);
    try { await api.delete(`/courses/${courseToDelete.courseId}`); toast.success('Đã xóa khóa học.'); setCourseToDelete(null); await fetchData(); }
    catch (err) { toast.error(err.response?.data?.message || 'Không thể xóa khóa học.'); }
    finally { setDeleting(false); }
  };

  if (loading) {
    return (
      <div className="instructor-dashboard container my-5">
        <style>{`
          @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: .5; }
          }
          .skeleton-pulse {
            animation: pulse 1.5s cubic-bezier(0.4, 0, 0.6, 1) infinite;
          }
        `}</style>
        <div className="d-flex justify-content-between align-items-center mb-4 pb-3 border-bottom">
          <div className="skeleton-pulse" style={{ width: '250px', height: '40px', backgroundColor: '#e2e8f0', borderRadius: '8px' }}></div>
          <div className="skeleton-pulse" style={{ width: '150px', height: '40px', backgroundColor: '#e2e8f0', borderRadius: '8px' }}></div>
        </div>
        <div className="row mb-5 g-4">
          {[1, 2, 3, 4].map(i => (
            <div key={i} className="col-md-3">
              <div className="card shadow-sm border-0 h-100 bg-white p-4">
                <div className="d-flex justify-content-between align-items-center mb-3">
                  <div className="skeleton-pulse" style={{ width: '100px', height: '16px', backgroundColor: '#e2e8f0', borderRadius: '4px' }}></div>
                  <div className="skeleton-pulse" style={{ width: '32px', height: '32px', backgroundColor: '#e2e8f0', borderRadius: '6px' }}></div>
                </div>
                <div className="skeleton-pulse" style={{ width: '120px', height: '36px', backgroundColor: '#e2e8f0', borderRadius: '6px' }}></div>
              </div>
            </div>
          ))}
        </div>
        <div className="card shadow-sm border-0 bg-white p-4">
          <div className="skeleton-pulse mb-4" style={{ width: '200px', height: '24px', backgroundColor: '#e2e8f0', borderRadius: '4px' }}></div>
          <div className="skeleton-pulse mb-3" style={{ width: '100%', height: '80px', backgroundColor: '#f1f5f9', borderRadius: '8px' }}></div>
          <div className="skeleton-pulse mb-3" style={{ width: '100%', height: '80px', backgroundColor: '#f1f5f9', borderRadius: '8px' }}></div>
        </div>
      </div>
    );
  }
  if (error) return <div className="error text-danger text-center py-5">{error}</div>;

  return (
    <div className="instructor-dashboard container my-5">
      <div className="d-flex justify-content-between align-items-center mb-4 pb-3 border-bottom">
        <div>
          <h2 className="fw-bold text-primary m-0">Instructor Dashboard</h2>
          <p className="text-muted m-0 small">Manage your lessons, analyze quality scores, and view student feedback.</p>
        </div>
        <div className="d-flex gap-3">
          <div className="btn-group">
            <button 
              className={`btn btn-sm ${activeTab === 'courses' ? 'btn-primary' : 'btn-outline-primary'}`} 
              onClick={() => setActiveTab('courses')}
            >
              My Courses
            </button>
            <button 
              className={`btn btn-sm ${activeTab === 'analytics' ? 'btn-primary' : 'btn-outline-primary'}`} 
              onClick={() => setActiveTab('analytics')}
            >
              Stats & ML Analytics
            </button>
          </div>
          <Link to="/instructor/courses/new" className="btn btn-primary fw-bold">Create New Course</Link>
        </div>
      </div>

      {!stats ? (
        <div className="row mb-5 g-4">
          {[1, 2, 3, 4].map(i => (
            <div key={i} className="col-md-3">
              <div className="card shadow-sm border-0 h-100 bg-white p-4">
                <div className="d-flex justify-content-between align-items-center mb-3">
                  <div className="skeleton-pulse" style={{ width: '100px', height: '16px', backgroundColor: '#e2e8f0', borderRadius: '4px' }}></div>
                  <div className="skeleton-pulse" style={{ width: '32px', height: '32px', backgroundColor: '#e2e8f0', borderRadius: '6px' }}></div>
                </div>
                <div className="skeleton-pulse" style={{ width: '120px', height: '36px', backgroundColor: '#e2e8f0', borderRadius: '6px' }}></div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="row mb-5 g-3">
          <div className="col-6 col-md-4 col-lg-2">
            <div className="card shadow-sm border-0 h-100 bg-white">
              <div className="card-body p-3">
                <div className="d-flex justify-content-between align-items-center mb-1">
                  <h6 className="text-muted mb-0 small uppercase fw-bold" style={{ fontSize: '11px' }}>Total Courses</h6>
                  <div className="bg-primary bg-opacity-10 p-1.5 rounded"><BookOpen size={16} className="text-primary"/></div>
                </div>
                <h4 className="mb-0 fw-bold">{stats.totalCourses}</h4>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-4 col-lg-2">
            <div className="card shadow-sm border-0 h-100 bg-white">
              <div className="card-body p-3">
                <div className="d-flex justify-content-between align-items-center mb-1">
                  <h6 className="text-muted mb-0 small uppercase fw-bold" style={{ fontSize: '11px' }}>Active Students</h6>
                  <div className="bg-success bg-opacity-10 p-1.5 rounded"><Users size={16} className="text-success"/></div>
                </div>
                <h4 className="mb-0 fw-bold">{stats.activeStudents}</h4>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-4 col-lg-2">
            <div className="card shadow-sm border-0 h-100 bg-white">
              <div className="card-body p-3">
                <div className="d-flex justify-content-between align-items-center mb-1">
                  <h6 className="text-muted mb-0 small uppercase fw-bold" style={{ fontSize: '11px' }}>Monthly Revenue</h6>
                  <div className="bg-warning bg-opacity-10 p-1.5 rounded"><DollarSign size={16} className="text-warning"/></div>
                </div>
                <h4 className="mb-0 fw-bold text-truncate" title={formatCurrencyVN(stats.monthlyRevenue)}>{formatCurrencyVN(stats.monthlyRevenue)}</h4>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-4 col-lg-2">
            <div className="card shadow-sm border-0 h-100 bg-white">
              <div className="card-body p-3">
                <div className="d-flex justify-content-between align-items-center mb-1">
                  <h6 className="text-muted mb-0 small uppercase fw-bold" style={{ fontSize: '11px' }}>Unanswered Q&As</h6>
                  <div className="bg-danger bg-opacity-10 p-1.5 rounded"><MessageCircle size={16} className="text-danger"/></div>
                </div>
                <h4 className="mb-0 fw-bold">{stats.unansweredQasCount}</h4>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-4 col-lg-2">
            <div className="card shadow-sm border-0 h-100 bg-white">
              <div className="card-body p-3">
                <div className="d-flex justify-content-between align-items-center mb-1">
                  <h6 className="text-muted mb-0 small uppercase fw-bold" style={{ fontSize: '11px' }}>Avg Progress</h6>
                  <div className="bg-info bg-opacity-10 p-1.5 rounded"><CheckCircle size={16} className="text-info"/></div>
                </div>
                <h4 className="mb-0 fw-bold">{stats.averageCompletionRate}%</h4>
              </div>
            </div>
          </div>
          <div className="col-6 col-md-4 col-lg-2">
            <div className="card shadow-sm border-0 h-100 bg-white">
              <div className="card-body p-3">
                <div className="d-flex justify-content-between align-items-center mb-1">
                  <h6 className="text-muted mb-0 small uppercase fw-bold" style={{ fontSize: '11px' }}>Quiz Avg Score</h6>
                  <div className="bg-secondary bg-opacity-10 p-1.5 rounded"><Award size={16} className="text-secondary"/></div>
                </div>
                <h4 className="mb-0 fw-bold">{stats.finalQuizAverageScore > 0 ? `${stats.finalQuizAverageScore}%` : 'N/A'}</h4>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Warnings & Suggestions Section */}
      {stats && stats.recommendations && stats.recommendations.length > 0 && (
        <div className="alert alert-warning border-0 shadow-sm p-4 mb-5" style={{ borderRadius: '12px' }}>
          <h5 className="fw-bold d-flex align-items-center gap-2 mb-3">
            <AlertTriangle size={20} /> Actionable Improvement Steps
          </h5>
          <ul className="mb-0 ps-3">
            {stats.recommendations.map((rec, i) => (
              <li key={i} className="mb-2 small">{rec}</li>
            ))}
          </ul>
        </div>
      )}

      {/* Tab: Course List */}
      {activeTab === 'courses' && (
        <div>
          <h4 className="mb-3 fw-bold">Course Curriculum</h4>
          <div className="course-list-grid">
            {courses.length === 0 ? (
              <p className="text-muted">You have not created any courses yet.</p>
            ) : (
              courses.map(course => (
                <div key={course.courseId} className="dashboard-course-card border-0 shadow-sm bg-white p-3 d-flex gap-3 mb-3 rounded" style={{ border: '1px solid var(--border-color)' }}>
                  <CourseThumbnail src={course.thumbnailUrl} categoryName={course.category?.name} alt={course.title} className="course-thumb rounded" style={{ width: '180px', height: '110px' }} />
                  <div className="course-info d-flex flex-column flex-grow-1 justify-content-between">
                    <div>
                      <h5 className="fw-bold mb-1">{course.title}</h5>
                      <div className="d-flex align-items-center gap-3">
                        <span className={`badge ${course.status === 'Published' ? 'bg-success' : course.status === 'PendingApproval' ? 'bg-primary' : course.status === 'Unpublished' ? 'bg-secondary' : course.status === 'Draft' ? 'bg-warning text-dark' : 'bg-danger'} rounded-pill`}>
                          {course.status}
                        </span>
                        {course.needsReanalysis && (
                          <span className="badge bg-danger rounded-pill d-flex align-items-center gap-1">
                            <AlertTriangle size={12} /> Content Changed
                          </span>
                        )}
                        <span className="small text-muted">Price: {formatCurrencyVN(course.price)}</span>
                      </div>
                    </div>
                    <div className="d-flex gap-3">
                      <Link to={`/instructor/courses/${course.courseId}/edit`} className="btn btn-sm btn-outline-primary px-3">
                        Edit Course
                      </Link>
                      <Link to={`/courses/${course.courseId}`} className="btn btn-sm btn-outline-secondary px-3">Detail</Link>
                      <button type="button" className="btn btn-sm btn-outline-danger px-3" onClick={() => setCourseToDelete(course)}>Xóa</button>
                      {course.needsReanalysis && (
                        <button 
                          onClick={() => handleTriggerAnalysis(course.courseId)}
                          className="btn btn-sm btn-warning d-flex align-items-center gap-1 px-3"
                        >
                          <Sparkles size={14} /> Run AI Analysis
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
          <div className="card border-0 shadow-sm mt-4"><div className="card-body"><h4 className="fw-bold">Khóa học đã bán gần đây</h4>{recentSales.length===0?<p className="text-muted">Chưa có giao dịch hoàn tất.</p>:<div className="table-responsive"><table className="table"><thead><tr><th>Khóa học</th><th>Người mua</th><th>Giá bán</th><th>Thời gian</th></tr></thead><tbody>{recentSales.map(s=><tr key={s.orderItemId}><td><Link to={`/courses/${s.courseId}`}>{s.courseTitle}</Link></td><td>{s.buyerName}</td><td>{formatCurrencyVN(s.soldPrice)}</td><td>{new Date(s.soldAt).toLocaleString('vi-VN')}</td></tr>)}</tbody></table></div>}</div></div>
        </div>
      )}

      {/* Tab: Analytics & ML Charts */}
      {activeTab === 'analytics' && stats && (
        <div className="row g-4">
          <div className="col-md-6">
            <div className="card border-0 shadow-sm bg-white p-4 h-100">
              <h5 className="fw-bold mb-3">Revenue Breakdown (Last 6 Months)</h5>
              {stats.revenueByDate?.length === 0 ? (
                <p className="text-muted small py-4 text-center">No sales records found.</p>
              ) : (
                <div className="d-flex flex-column gap-3 mt-3">
                  {stats.revenueByDate?.map(rev => (
                    <div key={rev.date} className="d-flex justify-content-between align-items-center border-bottom pb-2">
                      <span className="fw-bold small">{rev.date}</span>
                      <span className="badge bg-success font-monospace">{formatCurrencyVN(rev.revenue)}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className="col-md-6">
            <div className="card border-0 shadow-sm bg-white p-4 h-100">
              <h5 className="fw-bold mb-3">Reviews Sentiment Distribution</h5>
              {stats.sentimentStats?.length === 0 ? (
                <p className="text-muted small py-4 text-center">No review sentiment ratings compiled yet.</p>
              ) : (
                <div className="d-flex flex-column gap-3 mt-3">
                  {stats.sentimentStats?.map(sent => (
                    <div key={sent.label} className="d-flex justify-content-between align-items-center border-bottom pb-2">
                      <span className="fw-bold small">{sent.label}</span>
                      <span className={`badge ${sentimentClass(sent.label)}`}>
                        {sentimentText(sent.label)}: {sent.count} reviews
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {stats.perCourseAnalytics && stats.perCourseAnalytics.length > 0 && (
            <div className="col-12 mt-5">
              <h4 className="fw-bold mb-3 text-start">Per-Course Analytics Breakdown</h4>
              <div className="card shadow-sm border-0 bg-white rounded-3 overflow-hidden">
                <div className="table-responsive">
                  <table className="table table-hover align-middle mb-0 text-center">
                    <thead className="table-light">
                      <tr>
                        <th className="px-4 text-start">Course Title</th>
                        <th>Enrollments</th>
                        <th>Total Revenue</th>
                        <th>Avg Rating</th>
                        <th>Avg Progress</th>
                        <th>Quiz Avg Score</th>
                      </tr>
                    </thead>
                    <tbody>
                      {stats.perCourseAnalytics.map(c => (
                        <tr key={c.courseId}>
                          <td className="px-4 py-3 text-start">
                            <div className="d-flex align-items-center gap-2">
                              <CourseThumbnail src={c.thumbnailUrl} alt={c.title} style={{ width: '60px', height: '40px', objectFit: 'cover', borderRadius: '4px' }} />
                              <span className="fw-semibold small">{c.title}</span>
                            </div>
                          </td>
                          <td>{c.enrollmentsCount} students</td>
                          <td className="font-monospace text-success fw-bold">{formatCurrencyVN(c.revenue)}</td>
                          <td>
                            <div className="d-flex align-items-center justify-content-center gap-1">
                              <Star size={13} fill="currentColor" className="text-warning" />
                              <span className="small">{c.averageRating}</span>
                            </div>
                          </td>
                          <td>
                            <div className="d-flex flex-column align-items-center">
                              <div className="progress" style={{ width: '80px', height: '6px' }}>
                                <div className="progress-bar bg-success" style={{ width: `${c.completionRate}%` }} />
                              </div>
                              <span className="small text-muted mt-1" style={{ fontSize: '10px' }}>{c.completionRate}%</span>
                            </div>
                          </td>
                          <td>
                            <span className={`badge ${c.finalQuizAverageScore > 0 ? 'bg-info' : 'bg-light text-dark'}`}>
                              {c.finalQuizAverageScore > 0 ? `${c.finalQuizAverageScore}%` : 'N/A'}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          )}

          {stats.recentReviews && stats.recentReviews.length > 0 && (
            <div className="col-12 mt-4">
              <h4 className="fw-bold mb-3">Recent Reviews</h4>
              <div className="card shadow-sm border-0 bg-white">
                <ul className="list-group list-group-flush">
                  {stats.recentReviews.map(review => (
                    <li key={review.reviewId} className="list-group-item p-3 bg-transparent">
                      <div className="d-flex justify-content-between align-items-start">
                        <div>
                          <h6 className="mb-1 fw-bold"><Link to={`/users/${review.userId}`}>{review.studentName}</Link> <span className="text-muted fw-normal">on {review.courseTitle}</span></h6>
                          <p className="mb-1 text-muted small">{review.comment}</p>
                          <Link className="btn btn-sm btn-link px-0" to={`/courses/${review.courseId}#reviews`}>Mở review và phản hồi</Link>
                        </div>
                        <div className="text-end">
                          <div className="text-warning mb-1">
                            {[...Array(5)].map((_, i) => <Star key={i} size={12} fill={i < review.rating ? "currentColor" : "none"} />)}
                          </div>
                          {review.sentimentLabel && (
                            <span className={`badge ${sentimentClass(review.sentimentLabel)}`}>
                              {sentimentText(review.sentimentLabel)}
                            </span>
                          )}
                        </div>
                      </div>
                    </li>
                  ))}
                </ul>
              </div>
            </div>
          )}
        </div>
      )}
      <ConfirmModal open={Boolean(courseToDelete)} title="Xóa khóa học?" message={courseToDelete ? `“${courseToDelete.title}” sẽ bị ẩn khỏi công khai nhưng học viên đã đăng ký vẫn có thể học.` : ''} confirmLabel="Xóa khóa học" danger loading={deleting} onCancel={() => !deleting && setCourseToDelete(null)} onConfirm={deleteCourse} />
    </div>
  );
}

export default InstructorDashboard;
