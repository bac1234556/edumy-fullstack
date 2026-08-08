import React, { useState, useEffect, useContext } from 'react';
import api from '../api/axiosConfig';
import { AuthContext } from '../context/AuthContext';
import { toast } from 'react-hot-toast';
import { GraduationCap, Award, BookOpen, Clock, CheckCircle, AlertCircle } from 'lucide-react';

export default function TeachOnEdumy() {
  const { user } = useContext(AuthContext);
  const [formData, setFormData] = useState({
    introduction: '',
    expertise: '',
    reason: ''
  });
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  const fetchStatus = async () => {
    try {
      const { data } = await api.get('/instructorapplications/my-status');
      if (data.hasApplication) {
        setStatus(data);
      } else {
        setStatus(null);
      }
    } catch (err) {
      console.error(err);
      toast.error('Không thể kiểm tra trạng thái đăng ký.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStatus();
  }, []);

  const handleChange = (e) => {
    setFormData({ ...formData, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!formData.introduction.trim() || !formData.expertise.trim() || !formData.reason.trim()) {
      toast.error('Vui lòng nhập đầy đủ thông tin.');
      return;
    }

    setSubmitting(true);
    try {
      const { data } = await api.post('/instructorapplications', formData);
      toast.success('Gửi yêu cầu đăng ký thành công!');
      fetchStatus();
    } catch (err) {
      toast.error(err.response?.data || 'Gửi yêu cầu thất bại.');
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return <div className="container py-5 text-center">Đang tải...</div>;
  }

  return (
    <div className="container py-5">
      <div className="row justify-content-center">
        <div className="col-lg-8">
          <div className="card shadow border-0 p-4 bg-white" style={{ borderRadius: '16px' }}>
            <div className="text-center mb-4">
              <GraduationCap size={48} className="text-primary mb-2" />
              <h2 className="fw-bold">Dạy học trên Edumy</h2>
              <p className="text-muted">Chia sẻ kiến thức của bạn với hàng ngàn học viên toàn cầu.</p>
            </div>

            {status && status.status === 'Pending' && (
              <div className="alert alert-info d-flex align-items-center gap-2 py-3 mb-4">
                <Clock size={20} className="flex-shrink-0" />
                <div>
                  <strong className="d-block">Yêu cầu của bạn đang chờ phê duyệt</strong>
                  <span>Yêu cầu đăng ký làm giảng viên đã được gửi vào {new Date(status.createdAt).toLocaleDateString('vi-VN')}. Admin sẽ sớm xem xét và phản hồi.</span>
                </div>
              </div>
            )}

            {status && status.status === 'Approved' && (
              <div className="alert alert-success d-flex align-items-center gap-2 py-3 mb-4">
                <CheckCircle size={20} className="flex-shrink-0" />
                <div>
                  <strong className="d-block">Yêu cầu đã được phê duyệt!</strong>
                  <span>Bạn đã là Giảng viên. Vui lòng đăng xuất và đăng nhập lại để cập nhật quyền truy cập Dashboard Giảng viên.</span>
                </div>
              </div>
            )}

            {status && status.status === 'Rejected' && (
              <div className="alert alert-danger d-flex align-items-center gap-2 py-3 mb-4">
                <AlertCircle size={20} className="flex-shrink-0" />
                <div>
                  <strong className="d-block">Yêu cầu bị từ chối</strong>
                  <span>Yêu cầu trước đó của bạn đã bị từ chối vào {status.reviewedAt ? new Date(status.reviewedAt).toLocaleDateString('vi-VN') : ''}. Lý do: <em>{status.adminNote || 'Không có lý do chi tiết.'}</em></span>
                </div>
              </div>
            )}

            {(!status || status.status === 'Rejected') && (
              <form onSubmit={handleSubmit}>
                <div className="row g-3">
                  <div className="col-md-6">
                    <label className="form-label fw-bold">Họ và tên</label>
                    <input type="text" className="form-control bg-light" value={user?.fullName || ''} disabled />
                  </div>
                  <div className="col-md-6">
                    <label className="form-label fw-bold">Email</label>
                    <input type="email" className="form-control bg-light" value={user?.email || ''} disabled />
                  </div>

                  <div className="col-12">
                    <label className="form-label fw-bold">Giới thiệu ngắn bản thân</label>
                    <textarea 
                      name="introduction" 
                      className="form-control" 
                      rows="3" 
                      placeholder="Hãy tóm tắt ngắn gọn về bạn và sự nghiệp..." 
                      value={formData.introduction} 
                      onChange={handleChange}
                      required
                    />
                  </div>

                  <div className="col-12">
                    <label className="form-label fw-bold">Chuyên môn / Kinh nghiệm</label>
                    <textarea 
                      name="expertise" 
                      className="form-control" 
                      rows="3" 
                      placeholder="Lĩnh vực chuyên môn, số năm kinh nghiệm, các chứng chỉ, dự án lớn..." 
                      value={formData.expertise} 
                      onChange={handleChange}
                      required
                    />
                  </div>

                  <div className="col-12">
                    <label className="form-label fw-bold">Lý do muốn trở thành Giảng viên trên Edumy</label>
                    <textarea 
                      name="reason" 
                      className="form-control" 
                      rows="3" 
                      placeholder="Điều gì thôi thúc bạn chia sẻ kiến thức và tại sao bạn chọn Edumy..." 
                      value={formData.reason} 
                      onChange={handleChange}
                      required
                    />
                  </div>

                  <div className="col-12 text-center mt-4">
                    <button 
                      type="submit" 
                      className="btn btn-primary px-5 py-2.5 fw-semibold" 
                      disabled={submitting}
                      style={{ borderRadius: '8px' }}
                    >
                      {submitting ? 'Đang gửi...' : 'Gửi yêu cầu đăng ký'}
                    </button>
                  </div>
                </div>
              </form>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
