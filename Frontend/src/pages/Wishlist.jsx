import React, { useState, useEffect, useContext } from 'react';
import { Link } from 'react-router-dom';
import api from '../api/axiosConfig';
import { AuthContext } from '../context/AuthContext';
import { Heart, Trash2 } from 'lucide-react';
import { toast } from 'react-hot-toast';
import { formatCurrencyVN } from '../utils/format';
import './CourseList.css'; // Reuse course list styles
import CourseThumbnail from '../components/CourseThumbnail';

const BACKEND_URL = (import.meta.env.VITE_API_URL || '/api').replace('/api', '');

function Wishlist() {
  const [wishlist, setWishlist] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const { user } = useContext(AuthContext);

  useEffect(() => {
    fetchWishlist();
  }, []);

  const fetchWishlist = async () => {
    try {
      const response = await api.get('/wishlist');
      setWishlist(response.data);
    } catch (err) {
      console.error(err);
      setError('Could not load wishlist.');
    } finally {
      setLoading(false);
    }
  };

  const handleRemove = async (courseId) => {
    try {
      const token = localStorage.getItem('token');
      await api.delete(`/wishlist/${courseId}`, {
        headers: {
          Authorization: `Bearer ${token}`
        }
      });
      // Optimistic update
      setWishlist(prev => prev.filter(item => item.courseId !== courseId && item.course?.courseId !== courseId));
      toast.success('Đã cập nhật danh sách yêu thích!');
    } catch (err) {
      console.error(err);
      toast.error('Failed to remove course from wishlist.');
    }
  };

  if (!user) {
    return <div className="container text-center my-5">Please login to view your wishlist.</div>;
  }

  if (loading) return <div className="container text-center my-5">Loading wishlist...</div>;
  if (error) return <div className="container text-center my-5 text-danger">{error}</div>;

  return (
    <div className="container my-5">
      <h2 className="mb-4">My Wishlist <Heart className="text-danger ms-2" /></h2>
      
      {wishlist.length === 0 ? (
        <div className="text-center my-5">
          <h4 className="text-muted">Your wishlist is empty.</h4>
          <p>Browse our courses and add your favorites here!</p>
          <Link to="/courses" className="btn btn-primary mt-3 fw-bold">Explore Courses</Link>
        </div>
      ) : (
        <div className="course-grid">
          {wishlist.map(item => (
            <div key={item.id} className="course-card position-relative shadow-sm border-0">
              <Link to={`/courses/${item.courseId}`}>
                <CourseThumbnail src={item.course.thumbnailUrl} categoryName={item.course.categoryName} alt={item.course.title} className="course-image" />
                <div className="course-content">
                  <h3 className="course-title text-truncate" title={item.course.title}>{item.course.title}</h3>
                  <p className="course-instructor text-muted small">{item.course.instructor?.fullName || 'Instructor'}</p>
                  <div className="course-price fw-bold fs-5 mt-2">{formatCurrencyVN(item.course.price)}</div>
                </div>
              </Link>
              
              <button 
                className="btn btn-light position-absolute top-0 end-0 m-2 rounded-circle shadow-sm border"
                style={{ width: '40px', height: '40px', padding: '0', zIndex: 10 }}
                onClick={(e) => {
                  e.preventDefault();
                  handleRemove(item.courseId);
                }}
                title="Remove from Wishlist"
              >
                <Trash2 size={18} className="text-danger" />
              </button>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

export default Wishlist;
