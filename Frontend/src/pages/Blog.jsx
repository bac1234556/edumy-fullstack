import React from 'react';
import './CourseList.css'; // Re-use course grid styling

function Blog() {
  const articles = [
    {
      id: 1,
      title: 'Tương lai của AI trong lập trình: Copilot hay người viết code thực thụ?',
      excerpt: 'Sự phát triển vượt bậc của các mô hình ngôn ngữ lớn (LLM) đang thay đổi cách lập trình viên làm việc mỗi ngày...',
      author: 'Lê Hoàng Nam',
      date: '02/08/2026',
      readTime: '5 phút đọc',
      image: 'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=600&q=80'
    },
    {
      id: 2,
      title: 'Làm chủ React 19: Những tính năng mới quan trọng cần nắm rõ',
      excerpt: 'React 19 giới thiệu các Server Components cải tiến, tính năng xử lý Form Actions tối ưu và cải tiến lớn về hiệu năng render...',
      author: 'Nguyễn Bích Thủy',
      date: '28/07/2026',
      readTime: '8 phút đọc',
      image: 'https://images.unsplash.com/photo-1633356122544-f134324a6cee?w=600&q=80'
    },
    {
      id: 3,
      title: 'Xây dựng REST API chất lượng cao với ASP.NET Core và SQL Server',
      excerpt: 'Hướng dẫn từng bước thiết lập Clean Architecture và xử lý Exceptions tập trung chuyên nghiệp trong dự án Backend thực tế...',
      author: 'Trần Minh Quân',
      date: '20/07/2026',
      readTime: '10 phút đọc',
      image: 'https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&q=80'
    },
    {
      id: 4,
      title: 'Tại sao Micro-Animations giúp nâng tầm trải nghiệm người dùng?',
      excerpt: 'Các chuyển động nhỏ, mượt mà khi hover hay click giúp website của bạn sống động, thu hút người xem hơn bao giờ hết...',
      author: 'Phạm Hồng Anh',
      date: '15/07/2026',
      readTime: '4 phút đọc',
      image: 'https://images.unsplash.com/photo-1550751827-4bd374c3f58b?w=600&q=80'
    }
  ];

  return (
    <div className="container my-5 py-3">
      <div className="text-center mb-5">
        <h1 className="fw-bold mb-2">Edumy Tech Blog</h1>
        <p className="text-muted leading-relaxed">
          Chia sẻ kiến thức, tin tức công nghệ mới nhất và hướng dẫn lập trình từ các chuyên gia hàng đầu.
        </p>
      </div>

      <div className="row g-4">
        {articles.map((art) => (
          <div className="col-md-6" key={art.id}>
            <div className="card h-100 border-0 shadow-sm overflow-hidden rounded-4 hover-3d" style={{ transition: 'transform 0.2s ease-in-out' }}>
              <img 
                src={art.image} 
                alt={art.title} 
                className="card-img-top" 
                style={{ height: '220px', objectFit: 'cover' }}
              />
              <div className="card-body p-4 d-flex flex-column">
                <div className="d-flex justify-content-between text-muted small mb-2">
                  <span>{art.date} • {art.readTime}</span>
                  <span className="text-primary fw-medium">{art.author}</span>
                </div>
                <h4 className="card-title fw-bold mb-3" style={{ fontSize: '1.25rem', color: '#1a202c', minHeight: '3rem' }}>
                  {art.title}
                </h4>
                <p className="card-text text-muted mb-4" style={{ fontSize: '0.95rem' }}>
                  {art.excerpt}
                </p>
                <button className="btn btn-outline-primary rounded-pill mt-auto align-self-start fw-semibold">
                  Đọc thêm
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export default Blog;
