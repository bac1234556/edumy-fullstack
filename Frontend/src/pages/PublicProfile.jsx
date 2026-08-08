import { useContext, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Calendar, Heart, Users } from 'lucide-react';
import { toast } from 'react-hot-toast';
import api from '../api/axiosConfig';
import { AuthContext } from '../context/AuthContext';
import './UserProfile.css';
import ConfirmModal from '../components/ConfirmModal';

export default function PublicProfile() {
  const { id } = useParams();
  const { user, isAdmin } = useContext(AuthContext);
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [changing, setChanging] = useState(false);
  const [confirmStatus, setConfirmStatus] = useState(false);

  const load = () => api.get(`/users/${id}/public-profile`).then(r => setProfile(r.data))
    .catch(() => setError('Không tìm thấy hồ sơ người dùng.')).finally(() => setLoading(false));
  useEffect(() => { load(); }, [id]);

  const toggleStatus = async () => {
    setChanging(true);
    try { await api.put(`/admin/users/${id}/toggle-status`); toast.success('Đã cập nhật trạng thái tài khoản.'); await load(); setConfirmStatus(false); }
    catch (e) { toast.error(e.response?.data?.message || 'Không thể cập nhật trạng thái.'); }
    finally { setChanging(false); }
  };

  if (loading) return <div className="container py-5 text-center">Đang tải hồ sơ...</div>;
  if (error || !profile) return <div className="container py-5 text-center text-danger">{error}</div>;
  const data = profile.roleData || {};
  const ownProfile = Number(user?.id) === Number(id);

  return <div className="container my-5 profile-page">
    <div className="card shadow-sm border-0 p-4 mb-4">
      <div className="d-flex flex-wrap justify-content-between gap-3">
        <div className="d-flex gap-3 align-items-center">
          {profile.avatarUrl ? <img className="rounded-circle object-fit-cover" width="100" height="100" src={profile.avatarUrl} alt={profile.fullName} /> :
            <div className="profile-avatar d-flex align-items-center justify-content-center bg-primary text-white fs-1 fw-bold rounded-circle" style={{width:100,height:100}}>{profile.fullName?.[0]}</div>}
          <div><h1 className="h3 mb-1">{profile.fullName}</h1><p className="text-muted mb-2">{profile.headline || 'Edumy Member'}</p>
            <span className="badge bg-primary">{profile.role}</span><span className="ms-3 text-muted small"><Calendar size={14}/> Tham gia {new Date(profile.createdAt).toLocaleDateString('vi-VN')}</span>
          </div>
        </div>
        {isAdmin && !ownProfile && profile.isActive !== null && <button className={`btn ${profile.isActive ? 'btn-danger' : 'btn-success'} align-self-start`} disabled={changing} onClick={()=>setConfirmStatus(true)}>{changing ? 'Đang xử lý...' : profile.isActive ? 'Unactive' : 'Active'}</button>}
      </div>
      {profile.bio && <p className="mt-4 mb-0" style={{whiteSpace:'pre-line'}}>{profile.bio}</p>}
    </div>

    {profile.role === 'Instructor' ? <>
      <div className="row g-3 mb-4">
        <div className="col-md-4"><div className="card p-3 text-center"><strong>{data.courseCount || 0}</strong><span>Khóa học</span></div></div>
        <div className="col-md-4"><div className="card p-3 text-center"><strong>{data.totalStudents || 0}</strong><span>Học viên</span></div></div>
        <div className="col-md-4"><div className="card p-3 text-center"><strong>{data.averageRating || 0}</strong><span>Đánh giá trung bình</span></div></div>
      </div>
      <h2 className="h4">Khóa học đã xuất bản</h2><div className="row g-3">{(data.courses || []).map(c => <div className="col-md-4" key={c.courseId}><Link className="card p-3 h-100 text-decoration-none" to={`/courses/${c.courseId}`}><strong>{c.title}</strong><small className="text-muted mt-2"><Users size={14}/> {c.studentCount} học viên · ★ {c.averageRating}</small></Link></div>)}</div>
    </> : <>
      <h2 className="h4">Khóa học đang học</h2><div className="row g-3 mb-4">{(data.enrolledCourses || []).map(c => <div className="col-md-4" key={c.courseId}><Link className="card p-3 text-decoration-none" to={`/courses/${c.courseId}`}><strong>{c.title}</strong><small>{c.progressPercentage}% hoàn thành</small></Link></div>)}</div>
      <h2 className="h4"><Heart size={20}/> Wishlist</h2><div className="row g-3">{(data.wishlist || []).map(c => <div className="col-md-4" key={c.courseId}><Link className="card p-3 text-decoration-none" to={`/courses/${c.courseId}`}>{c.title}</Link></div>)}</div>
    </>}
    <ConfirmModal open={confirmStatus} title={profile.isActive ? 'Khóa tài khoản?' : 'Mở khóa tài khoản?'} message={profile.isActive ? 'Người dùng sẽ không thể đăng nhập hoặc sử dụng các chức năng được bảo vệ.' : 'Người dùng sẽ có thể đăng nhập và sử dụng hệ thống trở lại.'} confirmLabel={profile.isActive ? 'Khóa tài khoản' : 'Mở khóa'} danger={profile.isActive} loading={changing} onCancel={()=>!changing&&setConfirmStatus(false)} onConfirm={toggleStatus}/>
  </div>;
}
