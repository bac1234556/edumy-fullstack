import React from 'react';
import './Auth.css'; // Re-use auth or general card styles for consistent look

function AboutUs() {
  const stats = [
    { label: 'Học viên toàn cầu', value: '10M+' },
    { label: 'Khóa học chất lượng', value: '50k+' },
    { label: 'Giảng viên chuyên môn', value: '1,500+' },
    { label: 'Quốc gia & Vùng lãnh thổ', value: '120+' }
  ];

  const team = [
    { name: 'Nguyễn Văn Hùng', role: 'Founder & CEO', bio: 'Cựu kỹ sư Google với hơn 10 năm kinh nghiệm phát triển nền tảng giáo dục.', avatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150' },
    { name: 'Jane Smith', role: 'Chief of Technology', bio: 'Chuyên gia thiết kế hạ tầng đám mây và hệ thống học tập thông minh (AI Learning).', avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150' },
    { name: 'Alex Johnson', role: 'Lead UI/UX Designer', bio: 'Mang lại giao diện học tập tiện lợi, tinh tế và hướng đến trải nghiệm người dùng tối ưu.', avatar: 'https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?w=150' }
  ];

  return (
    <div className="container my-5 py-3">
      {/* Hero section */}
      <div className="text-center mb-5 p-4 rounded-4 shadow-sm" style={{ background: 'linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%)', color: '#fff' }}>
        <h1 className="display-4 fw-bold mb-3">Về Chúng Tôi - Edumy</h1>
        <p className="lead mx-auto" style={{ maxWidth: '700px' }}>
          Sứ mệnh của Edumy là thay đổi cách thế giới học tập bằng việc kết nối học viên với những khóa học chất lượng cao và các giảng viên hàng đầu toàn cầu.
        </p>
      </div>

      {/* Vision & Mission */}
      <div className="row g-4 mb-5">
        <div className="col-md-6">
          <div className="card h-100 border-0 shadow-sm p-4 rounded-4 hover-3d">
            <h3 className="fw-bold mb-3" style={{ color: '#1e3a8a' }}>Tầm Nhìn</h3>
            <p className="text-muted leading-relaxed">
              Trở thành nền tảng công nghệ giáo dục (EdTech) trực tuyến hàng đầu, mang lại cơ hội tiếp cận tri thức bình đẳng, hiệu quả và tối ưu thời gian cho tất cả mọi người trên thế giới.
            </p>
          </div>
        </div>
        <div className="col-md-6">
          <div className="card h-100 border-0 shadow-sm p-4 rounded-4 hover-3d">
            <h3 className="fw-bold mb-3" style={{ color: '#1e3a8a' }}>Sứ Mệnh</h3>
            <p className="text-muted leading-relaxed">
              Cung cấp giải pháp học tập cá nhân hóa thông qua AI, giúp người học phát triển các kỹ năng chuyên môn thực chiến, mở khóa tiềm năng sự nghiệp tối đa một cách bền vững.
            </p>
          </div>
        </div>
      </div>

      {/* Stats section */}
      <div className="row text-center g-4 mb-5">
        {stats.map((stat, idx) => (
          <div className="col-6 col-md-3" key={idx}>
            <div className="p-4 bg-light rounded-4 shadow-sm border h-100">
              <h2 className="display-5 fw-extrabold text-primary mb-2">{stat.value}</h2>
              <span className="text-muted fw-medium">{stat.label}</span>
            </div>
          </div>
        ))}
      </div>

      {/* Team section */}
      <div>
        <h3 className="text-center fw-bold mb-4" style={{ color: '#1e3a8a' }}>Đội Ngũ Sáng Lập & Phát Triển</h3>
        <div className="row g-4">
          {team.map((member, idx) => (
            <div className="col-md-4" key={idx}>
              <div className="card h-100 border-0 shadow-sm text-center p-4 rounded-4 hover-3d">
                <img 
                  src={member.avatar} 
                  alt={member.name} 
                  className="rounded-circle mx-auto mb-3 shadow-sm"
                  style={{ width: '90px', height: '90px', objectFit: 'cover' }}
                />
                <h5 className="fw-bold mb-1">{member.name}</h5>
                <p className="text-primary small mb-3">{member.role}</p>
                <p className="text-muted small mb-0">{member.bio}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default AboutUs;
