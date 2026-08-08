import React from 'react';
import { Link } from 'react-router-dom';
import { Star } from 'lucide-react';
import { formatCurrencyVN } from '../utils/format';
import './CourseCard.css';
import CourseThumbnail from './CourseThumbnail';

const CourseCard = ({ course }) => {
  return (
    <Link to={`/courses/${course.courseId || course.id || 1}`} className="course-card hover-3d">
      <div className="course-image" style={{ height: '180px', overflow: 'hidden' }}>
        <CourseThumbnail src={course.thumbnailUrl} categoryName={course.category?.name || course.categoryName} alt={course.title} className="w-100 h-100 object-fit-cover" />
      </div>
      <div className="course-content">
        <h3 className="course-title">{course.title || "Untitled Course"}</h3>
        <p className="course-instructor">{course.instructor?.fullName || course.instructor || "Instructor"}</p>
        
        <div className="course-rating">
          <span className="rating-score">{course.averageRating || course.rating || "4.5"}</span>
          <div className="stars">
            {[1, 2, 3, 4, 5].map((star) => (
              <Star 
                key={star} 
                size={14} 
                className={star <= Math.round(course.averageRating || course.rating || 4.5) ? "star-filled" : "star-empty"}
                fill={star <= Math.round(course.averageRating || course.rating || 4.5) ? "var(--accent-color)" : "none"}
                color={star <= Math.round(course.averageRating || course.rating || 4.5) ? "var(--accent-color)" : "var(--text-muted)"}
              />
            ))}
          </div>
          <span className="rating-count">{course.reviews?.length || course.reviews || "0"}</span>
        </div>
        
        <div className="course-price">
          <span className="current-price">{new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(course.price)}</span>
        </div>
        
        {course.bestseller && (
          <div className="course-badges">
            <span className="badge bestseller">Bestseller</span>
          </div>
        )}
      </div>
    </Link>
  );
};

export default CourseCard;
