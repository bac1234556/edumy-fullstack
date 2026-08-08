import React from 'react';

function TermsPrivacy() {
  return (
    <div className="container my-5 py-3" style={{ maxWidth: '850px' }}>
      <div className="text-center mb-5">
        <h1 className="fw-bold mb-2">Điều Khoản Dịch Vụ & Chính Sách Bảo Mật</h1>
        <p className="text-muted leading-relaxed">
          Cập nhật ngày: 01 tháng 08 năm 2026. Vui lòng đọc kỹ trước khi sử dụng nền tảng của chúng tôi.
        </p>
      </div>

      <div className="card border shadow-sm p-5 rounded-4 bg-white">
        <section className="mb-5">
          <h3 className="fw-bold mb-3" style={{ color: '#1e3a8a' }}>1. Điều khoản sử dụng tài khoản</h3>
          <p className="text-muted leading-relaxed" style={{ fontSize: '0.95rem' }}>
            Khi đăng ký tài khoản trên Edumy, bạn đồng ý cung cấp thông tin chính xác, đầy đủ và tự bảo mật thông tin tài khoản đăng nhập của mình.
            Học viên không được chia sẻ tài khoản cho người khác dùng chung. Nếu phát hiện dùng chung tài khoản, hệ thống có quyền tạm khóa mà không cần báo trước.
          </p>
        </section>

        <section className="mb-5">
          <h3 className="fw-bold mb-3" style={{ color: '#1e3a8a' }}>2. Sở hữu trí tuệ</h3>
          <p className="text-muted leading-relaxed" style={{ fontSize: '0.95rem' }}>
            Tất cả các tài liệu bài giảng, video khóa học, mã nguồn mẫu, câu hỏi trắc nghiệm đều thuộc quyền sở hữu trí tuệ của Edumy hoặc giảng viên tạo ra chúng.
            Hành vi sao chép, phân phối phi pháp hoặc phát tán nội dung lên mạng xã hội là vi phạm pháp luật và sẽ bị xử lý nghiêm khắc.
          </p>
        </section>

        <section className="mb-5">
          <h3 className="fw-bold mb-3" style={{ color: '#1e3a8a' }}>3. Chính sách bảo mật thông tin</h3>
          <p className="text-muted leading-relaxed" style={{ fontSize: '0.95rem' }}>
            Chúng tôi thu thập các thông tin cơ bản bao gồm Email, Họ tên để cá nhân hóa lộ trình học tập, quản lý thanh toán và phục vụ việc cấp chứng nhận.
            Edumy cam kết tuyệt đối không chia sẻ hoặc bán dữ liệu cá nhân của học viên cho bên thứ ba vì bất kỳ mục đích quảng cáo thương mại nào.
          </p>
        </section>

        <section className="mb-0">
          <h3 className="fw-bold mb-3" style={{ color: '#1e3a8a' }}>4. Chính sách Cookie & Khả năng tiếp cận</h3>
          <p className="text-muted leading-relaxed" style={{ fontSize: '0.95rem' }}>
            Website sử dụng cookie lưu trữ cục bộ (local storage) nhằm duy trì trạng thái đăng nhập của người dùng, sản phẩm trong giỏ hàng và danh sách khóa học yêu thích.
            Chúng tôi luôn nỗ lực thiết kế website tuân thủ tiêu chuẩn khả năng tiếp cận W3C để hỗ trợ người dùng có hoàn cảnh đặc biệt học tập thuận tiện nhất.
          </p>
        </section>
      </div>
    </div>
  );
}

export default TermsPrivacy;
