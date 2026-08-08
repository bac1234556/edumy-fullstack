import React, { useState, useEffect } from 'react';
import api from '../api/axiosConfig';
import { toast } from 'react-hot-toast';
import { Check, X, Shield, Calendar, Mail, FileText, User } from 'lucide-react';

export default function AdminInstructorApplications() {
  const [applications, setApplications] = useState([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [selectedApp, setSelectedApp] = useState(null);
  const [adminNote, setAdminNote] = useState('');
  const [modalType, setModalType] = useState(''); // 'approve' or 'reject'

  const fetchApplications = async () => {
    try {
      const { data } = await api.get('/admin/instructor-applications?status=Pending');
      setApplications(data || []);
    } catch (err) {
      console.error(err);
      toast.error('Không thể tải danh sách đăng ký.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchApplications();
  }, []);

  const handleActionClick = (app, type) => {
    setSelectedApp(app);
    setModalType(type);
    setAdminNote('');
  };

  const handleConfirmAction = async () => {
    if (!selectedApp) return;
    setSubmitting(true);
    try {
      if (modalType === 'approve') {
        await api.post(`/admin/instructor-applications/${selectedApp.instructorApplicationId}/approve`, { adminNote });
        toast.success(`Đã phê duyệt đơn đăng ký của ${selectedApp.applicantName}!`);
      } else {
        await api.post(`/admin/instructor-applications/${selectedApp.instructorApplicationId}/reject`, { adminNote });
        toast.success(`Đã từ chối đơn đăng ký của ${selectedApp.applicantName}.`);
      }
      setSelectedApp(null);
      fetchApplications();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Thao tác thất bại.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return <div className="container py-5 text-center">Đang tải danh sách đăng ký...</div>;
  }

  return (
    <div className="container py-5">
      <div className="d-flex align-items-center gap-3 mb-4">
        <Shield size={36} className="text-primary" />
        <div>
          <h2 className="fw-bold mb-0">Phê duyệt Giảng viên</h2>
          <p className="text-muted mb-0">Xét duyệt đơn đăng ký trở thành Giảng viên từ Học viên.</p>
        </div>
      </div>

      {applications.length === 0 ? (
        <div className="card text-center p-5 border shadow-sm bg-white" style={{ borderRadius: '12px' }}>
          <User size={48} className="text-muted mx-auto mb-2" />
          <h4 className="text-muted">Không có đơn đăng ký nào đang chờ duyệt.</h4>
        </div>
      ) : (
        <div className="row g-4">
          <div className="col-12">
            <div className="card border shadow-sm bg-white overflow-hidden" style={{ borderRadius: '12px' }}>
              <div className="table-responsive">
                <table className="table table-hover align-middle mb-0">
                  <thead className="table-light">
                    <tr>
                      <th className="px-4">Học viên</th>
                      <th>Giới thiệu bản thân</th>
                      <th>Kinh nghiệm</th>
                      <th>Lý do muốn dạy</th>
                      <th>Ngày đăng ký</th>
                      <th className="text-end px-4">Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {applications.map((app) => (
                      <tr key={app.instructorApplicationId}>
                        <td className="px-4 py-3">
                          <div className="fw-bold">{app.applicantName}</div>
                          <small className="text-muted d-flex align-items-center gap-1">
                            <Mail size={12} /> {app.applicantEmail}
                          </small>
                        </td>
                        <td style={{ maxWidth: '200px', whiteSpace: 'pre-line' }}>{app.introduction}</td>
                        <td style={{ maxWidth: '200px', whiteSpace: 'pre-line' }}>{app.expertise}</td>
                        <td style={{ maxWidth: '200px', whiteSpace: 'pre-line' }}>{app.reason}</td>
                        <td>
                          <small className="text-muted d-flex align-items-center gap-1">
                            <Calendar size={12} /> {new Date(app.createdAt).toLocaleDateString('vi-VN')}
                          </small>
                        </td>
                        <td className="text-end px-4">
                          <div className="d-flex justify-content-end gap-2">
                            <button 
                              className="btn btn-sm btn-success d-flex align-items-center gap-1"
                              onClick={() => handleActionClick(app, 'approve')}
                            >
                              <Check size={14} /> Duyệt
                            </button>
                            <button 
                              className="btn btn-sm btn-danger d-flex align-items-center gap-1"
                              onClick={() => handleActionClick(app, 'reject')}
                            >
                              <X size={14} /> Từ chối
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        </div>
      )}

      {selectedApp && (
        <div className="modal fade show d-block" style={{ backgroundColor: 'rgba(0,0,0,0.5)', zIndex: 1050 }}>
          <div className="modal-dialog modal-dialog-centered">
            <div className="modal-content border-0 shadow" style={{ borderRadius: '12px' }}>
              <div className="modal-header border-bottom">
                <h5 className="modal-title fw-bold">
                  {modalType === 'approve' ? 'Duyệt giảng viên' : 'Từ chối giảng viên'}
                </h5>
                <button type="button" className="btn-close" onClick={() => setSelectedApp(null)}></button>
              </div>
              <div className="modal-body">
                <p>
                  Bạn đang chuẩn bị {modalType === 'approve' ? 'DUYỆT' : 'TỪ CHỐI'} đơn đăng ký của{' '}
                  <strong>{selectedApp.applicantName}</strong>.
                </p>
                {modalType === 'approve' ? (
                  <p className="text-muted small">
                    Học viên sẽ được đổi vai trò sang Giảng viên ngay lập tức và nhận được thông báo chúc mừng.
                  </p>
                ) : (
                  <p className="text-muted small">
                    Học viên vẫn giữ vai trò Học viên. Vui lòng nhập lý do từ chối để học viên nắm thông tin.
                  </p>
                )}

                <div className="mb-3">
                  <label className="form-label fw-bold">Ghi chú của Admin (Tùy chọn)</label>
                  <textarea 
                    className="form-control" 
                    rows="3" 
                    placeholder={modalType === 'reject' ? 'Nhập lý do từ chối (bắt buộc)...' : 'Nhập ghi chú...'} 
                    value={adminNote}
                    onChange={(e) => setAdminNote(e.target.value)}
                    required={modalType === 'reject'}
                  />
                </div>
              </div>
              <div className="modal-footer border-top">
                <button type="button" className="btn btn-outline-secondary" onClick={() => setSelectedApp(null)}>Hủy</button>
                <button 
                  type="button" 
                  className={`btn ${modalType === 'approve' ? 'btn-success' : 'btn-danger'}`}
                  onClick={handleConfirmAction}
                  disabled={submitting || (modalType === 'reject' && !adminNote.trim())}
                >
                  {submitting ? 'Đang xử lý...' : modalType === 'approve' ? 'Xác nhận duyệt' : 'Xác nhận từ chối'}
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
