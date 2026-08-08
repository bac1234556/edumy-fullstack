import { useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useCategories } from '../context/CategoryContext';
import './CategorySlider.css';

export default function CategorySlider() {
  const scrollRef = useRef(null);
  const [showLeft, setShowLeft] = useState(false);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { categories, loading, error } = useCategories();
  const selectedId = searchParams.get('categoryId');
  const move = amount => scrollRef.current?.scrollBy({ left: amount, behavior: 'smooth' });

  return (
    <div className="category-slider-wrapper">
      {showLeft && <button type="button" className="slider-btn left" onClick={() => move(-260)} aria-label="Cuộn danh mục sang trái"><ChevronLeft size={24} /></button>}
      <div className="category-slider" ref={scrollRef} onScroll={event => setShowLeft(event.currentTarget.scrollLeft > 4)} aria-label="Danh mục khóa học">
        {loading && <span className="category-slider-state">Đang tải danh mục...</span>}
        {!loading && error && <span className="category-slider-state text-danger">{error}</span>}
        {!loading && !error && categories.map(category => (
          <button type="button" key={category.categoryId} className={`category-item hover-3d${String(category.categoryId) === selectedId ? ' active' : ''}`} onClick={() => navigate(`/courses?categoryId=${category.categoryId}`)} aria-pressed={String(category.categoryId) === selectedId}>
            {category.name}<small>{category.publishedCourseCount} khóa học</small>
          </button>
        ))}
      </div>
      {!loading && categories.length > 0 && <button type="button" className="slider-btn right" onClick={() => move(260)} aria-label="Cuộn danh mục sang phải"><ChevronRight size={24} /></button>}
    </div>
  );
}
