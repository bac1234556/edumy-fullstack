import React, { useState, useEffect, useContext } from 'react';
import api from '../api/axiosConfig';
import { AuthContext } from '../context/AuthContext';
import { User, Mail, Calendar, Edit3, Check, X, Bookmark, Globe, ShoppingBag } from 'lucide-react';
import { toast } from 'react-hot-toast';
import { formatCurrencyVN } from '../utils/format';
import './UserProfile.css';
import { useNavigate } from 'react-router-dom';
import ConfirmModal from '../components/ConfirmModal';

function UserProfile() {
  const navigate = useNavigate();
  const [profile, setProfile] = useState(null);
  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  
  const [isEditing, setIsEditing] = useState(false);
  const [formData, setFormData] = useState({ fullName: '', headline: '', bio: '', avatarUrl: '' });
  const [saving, setSaving] = useState(false);
  const [deletePhrase, setDeletePhrase] = useState('');
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const { user, isStudent, clearSession, updateUserProfile } = useContext(AuthContext);

  const fetchProfile = async () => {
    try {
      const response = await api.get('/users/profile');
      setProfile(response.data);
      setFormData({
        fullName: response.data.fullName || '',
        headline: response.data.headline || '',
        bio: response.data.bio || '',
        avatarUrl: response.data.avatarUrl || ''
      });

      if (user && isStudent) {
        const ordersRes = await api.get('/orders/my-orders');
        setOrders(ordersRes.data || []);
      }
    } catch (err) {
      console.error(err);
      setError('Could not fetch profile information.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchProfile();
  }, [user]);

  const handleEdit = () => {
    setIsEditing(true);
  };

  const handleCancel = () => {
    setFormData({
      fullName: profile.fullName || '',
      headline: profile.headline || '',
      bio: profile.bio || '',
      avatarUrl: profile.avatarUrl || ''
    });
    setIsEditing(false);
  };

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      setSaving(true);
      const response = await api.put('/users/profile', formData);
      setProfile(response.data);
      if (typeof updateUserProfile === 'function') {
        updateUserProfile(response.data);
      }
      setIsEditing(false);
      toast.success('Cập nhật hồ sơ thành công!');
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to update profile.');
    } finally {
      setSaving(false);
    }
  };

  const deleteAccount = async () => {
    if (deletePhrase !== 'XÓA TÀI KHOẢN' || deleting) return;
    setDeleting(true);
    try {
      await api.delete('/account/me', { data: { confirmation: deletePhrase } });
      clearSession();
      navigate('/login', { replace: true, state: { flash: { type: 'success', message: 'Tài khoản của bạn đã được xóa.' } } });
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể xóa tài khoản.');
      setDeleteOpen(false);
    } finally { setDeleting(false); }
  };

  if (!user) {
    return <div className="container text-center my-5">Please login to view your profile.</div>;
  }

  if (loading) return <div className="container text-center my-5">Loading profile...</div>;
  if (error) return <div className="container text-center my-5 text-danger">{error}</div>;

  return (
    <div className="container my-5 profile-page">
      <div className="row justify-content-center g-4">
        {/* Left Column: Avatar & Summary */}
        <div className="col-md-4">
          <div className="card shadow-sm border-0 text-center p-4 bg-white" style={{ borderRadius: '12px' }}>
            <div className="position-relative mx-auto mb-3" style={{ width: '120px', height: '120px' }}>
              {profile.avatarUrl ? (
                <img 
                  src={profile.avatarUrl} 
                  alt={profile.fullName} 
                  className="rounded-circle border shadow-sm w-100 h-100 object-fit-cover"
                  onError={(e) => {
                    e.target.onerror = null;
                    e.target.src = 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=200';
                  }}
                />
              ) : (
                <div className="profile-avatar mx-auto d-flex align-items-center justify-content-center bg-primary text-white fs-1 fw-bold rounded-circle shadow w-100 h-100">
                  {profile.fullName ? profile.fullName.charAt(0).toUpperCase() : 'U'}
                </div>
              )}
            </div>
            <h4 className="fw-bold mb-1">{profile.fullName}</h4>
            <p className="text-muted small mb-2">{profile.headline || 'Edumy Member'}</p>
            <span className="badge bg-primary bg-opacity-10 text-primary rounded-pill px-3 py-1.5 fw-bold uppercase">
              {user.role}
            </span>
          </div>
        </div>

        {/* Right Column: Details & Edit */}
        <div className="col-md-8">
          <div className="card shadow-sm border-0 bg-white p-4 mb-4" style={{ borderRadius: '12px' }}>
            <div className="d-flex justify-content-between align-items-center mb-4 pb-2 border-bottom">
              <h4 className="fw-bold text-primary m-0">Public Profile</h4>
              {!isEditing && (
                <button className="btn btn-outline-primary btn-sm rounded-pill px-3" onClick={handleEdit}>
                  <Edit3 size={16} className="me-2" /> Edit Profile
                </button>
              )}
            </div>

            {isEditing ? (
              <form onSubmit={handleSubmit} className="profile-form">
                <div className="mb-3">
                  <label className="form-label fw-bold">Full Name</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    name="fullName" 
                    value={formData.fullName} 
                    onChange={handleChange} 
                    required 
                  />
                </div>

                <div className="mb-3">
                  <label className="form-label fw-bold">Headline</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    name="headline" 
                    value={formData.headline} 
                    onChange={handleChange} 
                    placeholder="E.g., Computer Science Student / Web Developer"
                  />
                </div>

                <div className="mb-3">
                  <label className="form-label fw-bold">Avatar URL</label>
                  <input 
                    type="text" 
                    className="form-control" 
                    name="avatarUrl" 
                    value={formData.avatarUrl} 
                    onChange={handleChange} 
                    placeholder="HTTPS Image URL"
                  />
                </div>
                
                <div className="mb-3">
                  <label className="form-label fw-bold">Bio</label>
                  <textarea 
                    className="form-control" 
                    name="bio" 
                    rows="4" 
                    value={formData.bio} 
                    onChange={handleChange}
                    placeholder="Tell us a little bit about yourself..."
                  ></textarea>
                </div>

                <div className="d-flex gap-2 justify-content-end mt-4">
                  <button type="button" className="btn btn-light px-4" onClick={handleCancel} disabled={saving}>
                    <X size={18} className="me-1" /> Cancel
                  </button>
                  <button type="submit" className="btn btn-primary px-4" disabled={saving}>
                    {saving ? 'Saving...' : <><Check size={18} className="me-1" /> Save Changes</>}
                  </button>
                </div>
              </form>
            ) : (
              <div className="profile-info">
                <div className="row mb-3">
                  <div className="col-sm-3 text-muted"><Mail size={16} className="me-2"/> Email</div>
                  <div className="col-sm-9 fw-medium">{profile.email}</div>
                </div>
                
                <div className="row mb-3">
                  <div className="col-sm-3 text-muted"><User size={16} className="me-2"/> Full Name</div>
                  <div className="col-sm-9 fw-medium">{profile.fullName}</div>
                </div>

                <div className="row mb-3">
                  <div className="col-sm-3 text-muted"><Bookmark size={16} className="me-2"/> Headline</div>
                  <div className="col-sm-9 fw-medium">{profile.headline || <span className="text-muted fst-italic">No headline provided</span>}</div>
                </div>

                <div className="row mb-3">
                  <div className="col-sm-3 text-muted"><Calendar size={16} className="me-2"/> Member Since</div>
                  <div className="col-sm-9 fw-medium">{new Date(profile.createdAt).toLocaleDateString('vi-VN', { year: 'numeric', month: 'long', day: 'numeric' })}</div>
                </div>

                <div className="row mt-4 pt-3 border-top">
                  <div className="col-12">
                    <h6 className="text-muted mb-2">Bio</h6>
                    {profile.bio ? (
                      <p className="mb-0" style={{whiteSpace: 'pre-line'}}>{profile.bio}</p>
                    ) : (
                      <p className="text-muted fst-italic mb-0">No bio provided yet.</p>
                    )}
                  </div>
                </div>
              </div>
            )}
          </div>

          <div className="card shadow-sm border border-danger bg-white p-4 mb-4" style={{ borderRadius: '12px' }}>
            <h4 className="fw-bold text-danger">Vùng nguy hiểm</h4>
            <p className="text-muted">Xóa tài khoản sẽ thu hồi mọi phiên đăng nhập và ẩn danh thông tin cá nhân. Lịch sử giao dịch/học tập cần thiết vẫn được bảo toàn. Giảng viên đang có khóa học Published phải hủy xuất bản trước.</p>
            <label className="form-label fw-semibold" htmlFor="delete-account-phrase">Nhập chính xác <strong>XÓA TÀI KHOẢN</strong> để tiếp tục</label>
            <div className="d-flex flex-wrap gap-2">
              <input id="delete-account-phrase" className="form-control" style={{ maxWidth: 360 }} value={deletePhrase} onChange={event => setDeletePhrase(event.target.value)} autoComplete="off" />
              <button type="button" className="btn btn-danger" disabled={deletePhrase !== 'XÓA TÀI KHOẢN'} onClick={() => setDeleteOpen(true)}>Xóa tài khoản</button>
            </div>
          </div>

          {/* Student Purchase History Section */}
          {isStudent && (
            <div className="card shadow-sm border-0 bg-white p-4" style={{ borderRadius: '12px' }}>
              <h4 className="fw-bold text-primary mb-4 pb-2 border-bottom d-flex align-items-center gap-2">
                <ShoppingBag size={22} /> Lịch sử đơn hàng
              </h4>
              {orders.length === 0 ? (
                <p className="text-muted mb-0 py-3 text-center">Bạn chưa thực hiện bất kỳ đơn hàng nào.</p>
              ) : (
                <div className="table-responsive">
                  <table className="table align-middle">
                    <thead>
                      <tr>
                        <th>Đơn hàng</th>
                        <th>Ngày mua</th>
                        <th>Khóa học</th>
                        <th>Tổng tiền</th>
                        <th>Trạng thái</th>
                      </tr>
                    </thead>
                    <tbody>
                      {orders.map(order => (
                        <tr key={order.orderId}>
                          <td className="font-monospace fw-bold">#{order.orderId}</td>
                          <td>{new Date(order.createdAt).toLocaleDateString('vi-VN')}</td>
                          <td>
                            {order.orderItems?.map(item => (
                              <div key={item.orderItemId} className="small fw-semibold text-dark">
                                {item.course?.title || 'Course'}
                              </div>
                            ))}
                          </td>
                          <td className="fw-bold">{formatCurrencyVN(order.totalAmount)}</td>
                          <td>
                            <span className={`badge rounded-pill ${order.status === 'Completed' ? 'bg-success bg-opacity-10 text-success' : 'bg-warning bg-opacity-10 text-warning'}`}>
                              {order.status === 'Completed' ? 'Đã hoàn thành' : order.status}
                            </span>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}
        </div>
      </div>
      <ConfirmModal open={deleteOpen} title="Xác nhận xóa tài khoản" message="Thao tác này không thể hoàn tác. Bạn sẽ bị đăng xuất ngay lập tức." confirmLabel="Xóa vĩnh viễn" danger loading={deleting} onCancel={() => !deleting && setDeleteOpen(false)} onConfirm={deleteAccount} />
    </div>
  );
}

export default UserProfile;
