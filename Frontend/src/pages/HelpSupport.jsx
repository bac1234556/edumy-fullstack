import React, { useState } from 'react';
import { toast } from 'react-hot-toast';

function HelpSupport() {
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [message, setMessage] = useState('');
  const [activeFaq, setActiveFaq] = useState(null);

  const faqs = [
    {
      q: 'Làm thế nào để đăng ký tài khoản trên Edumy?',
      a: 'Bạn chỉ cần click vào nút "Sign up" trên header, điền thông tin họ tên, email và mật khẩu mong muốn để đăng ký ngay lập tức. Bạn cũng có thể đăng ký tài khoản Giảng viên để đăng khóa học.'
    },
    {
      q: 'Phương thức thanh toán nào được hỗ trợ?',
      a: 'Hệ thống hỗ trợ thanh toán giả lập thành công qua cổng thanh toán bảo mật của Edumy. Nhấn nút "Simulate Successful Payment" để mua khóa học ngay lập tức.'
    },
    {
      q: 'Tôi có thể yêu cầu hoàn tiền không?',
      a: 'Edumy cam kết hoàn trả 100% học phí trong vòng 30 ngày kể từ ngày mua nếu học viên chưa xem quá 10% thời lượng khóa học và không hài lòng về nội dung.'
    },
    {
      q: 'Chứng chỉ hoàn thành khóa học được cấp khi nào?',
      a: 'Chứng chỉ số của Edumy sẽ được tự động kích hoạt và hiển thị trong phần "Học tập" ngay khi bạn hoàn thành 100% tất cả các bài học và bài trắc nghiệm.'
    }
  ];

  const handleSubmit = (e) => {
    e.preventDefault();
    toast.success('Cảm ơn bạn đã gửi ý kiến phản hồi! Chúng tôi sẽ liên hệ lại sớm nhất.');
    setName('');
    setEmail('');
    setMessage('');
  };

  return (
    <div className="container my-5 py-3">
      <div className="text-center mb-5">
        <h1 className="fw-bold mb-2">Trợ Giúp & Hỗ Trợ</h1>
        <p className="text-muted leading-relaxed">
          Tìm kiếm câu trả lời nhanh chóng hoặc gửi liên hệ hỗ trợ trực tiếp tới chúng tôi.
        </p>
      </div>

      <div className="row g-5">
        {/* FAQs */}
        <div className="col-lg-7">
          <h3 className="fw-bold mb-4">Câu Hỏi Thường Gặp (FAQs)</h3>
          <div className="accordion">
            {faqs.map((faq, idx) => (
              <div key={idx} className="card border shadow-sm mb-3 rounded-3 overflow-hidden">
                <div 
                  className="card-header bg-white p-3 d-flex justify-content-between align-items-center"
                  style={{ cursor: 'pointer', fontWeight: '600', color: activeFaq === idx ? '#1e3a8a' : '#1a202c' }}
                  onClick={() => setActiveFaq(activeFaq === idx ? null : idx)}
                >
                  <span>{faq.q}</span>
                  <span>{activeFaq === idx ? '−' : '+'}</span>
                </div>
                {activeFaq === idx && (
                  <div className="card-body bg-light text-muted" style={{ fontSize: '0.95rem', lineHeight: '1.6' }}>
                    {faq.a}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>

        {/* Contact Form */}
        <div className="col-lg-5">
          <div className="card border-0 shadow-sm p-4 rounded-4 bg-light">
            <h3 className="fw-bold mb-4 text-center">Gửi Liên Hệ Feedback</h3>
            <form onSubmit={handleSubmit}>
              <div className="mb-3">
                <label className="form-label small fw-bold">Họ và tên</label>
                <input 
                  type="text" 
                  className="form-control" 
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Họ tên của bạn"
                  required 
                />
              </div>
              <div className="mb-3">
                <label className="form-label small fw-bold">Email liên hệ</label>
                <input 
                  type="email" 
                  className="form-control" 
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="name@example.com"
                  required 
                />
              </div>
              <div className="mb-4">
                <label className="form-label small fw-bold">Nội dung tin nhắn</label>
                <textarea 
                  className="form-control" 
                  rows="4"
                  value={message}
                  onChange={(e) => setMessage(e.target.value)}
                  placeholder="Góp ý hoặc mô tả vấn đề bạn gặp phải..."
                  required
                ></textarea>
              </div>
              <button type="submit" className="btn btn-primary w-100 rounded-pill py-2 fw-semibold">
                Gửi hỗ trợ
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}

export default HelpSupport;
