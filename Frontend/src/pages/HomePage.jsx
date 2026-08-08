import React, { useState, useEffect, useContext } from 'react';
import HeroSection from '../components/HeroSection';
import CategorySlider from '../components/CategorySlider';
import CourseCard from '../components/CourseCard';
import { AuthContext } from '../context/AuthContext';
import api from '../api/axiosConfig';
import { Link } from 'react-router-dom';
import { Sparkles, TrendingUp, Award, Clock } from 'lucide-react';
import { useCategories } from '../context/CategoryContext';

function HomePage() {
  const { user, isStudent } = useContext(AuthContext);
  const [recommendedCourses, setRecommendedCourses] = useState([]);
  const { categories, loading: categoriesLoading } = useCategories();
  const [popularCourses, setPopularCourses] = useState([]);
  const [newestCourses, setNewestCourses] = useState([]);
  const [topRatedCourses, setTopRatedCourses] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchHomeData = async () => {
      setLoading(true);
      try {
        // Fetch Popular Courses (sorted by rating descending)
        const popularRes = await api.get('/courses?pageSize=8&sortBy=rating&sortOrder=desc');
        setPopularCourses(popularRes.data.items || []);

        // 3. Fetch Newest Courses
        const newestRes = await api.get('/courses?pageSize=8&sortBy=newest&sortOrder=desc');
        setNewestCourses(newestRes.data.items || []);

        // 4. Fetch Top Rated (fallback/default list)
        const topRatedRes = await api.get('/courses?pageSize=4&minRating=4');
        setTopRatedCourses(topRatedRes.data.items || []);

        // 5. Fetch AI Recommendations if student is logged in
        if (user && isStudent) {
          try {
            const recRes = await api.get('/courses/recommend');
            setRecommendedCourses(recRes.data || []);
          } catch (recErr) {
            console.error('Failed to load AI recommendations', recErr);
          }
        }
      } catch (error) {
        console.error('Failed to load homepage data', error);
      } finally {
        setLoading(false);
      }
    };

    fetchHomeData();
  }, [user, isStudent]);

  return (
    <div className="homepage-wrapper" style={{ marginTop: '-40px' }}>
      <HeroSection />
      
      <div className="container" style={{ marginBottom: '60px' }}>
        <h2 style={{ fontSize: '28px', marginBottom: '12px', fontWeight: '800', textAlign: 'center' }}>All the skills you need in one place</h2>
        <p style={{ color: 'var(--text-muted)', marginBottom: '32px', textAlign: 'center', fontSize: '16px' }}>
          Explore professional engineering, marketing, design, and photography courses led by expert mentors.
        </p>
        
        {/* Dynamic Category Slider */}
        <CategorySlider />
        
        {/* AI Recommendations Section */}
        {user && isStudent && recommendedCourses.length > 0 && (
          <div className="mb-5 p-4" style={{ background: 'linear-gradient(135deg, rgba(109, 93, 252, 0.05) 0%, rgba(129, 140, 248, 0.05) 100%)', borderRadius: '16px', border: '1px solid rgba(109, 93, 252, 0.1)' }}>
            <h3 style={{ fontSize: '22px', marginBottom: '20px', fontWeight: '700', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Sparkles className="text-primary" size={24} /> ✨ AI Recommended For You
            </h3>
            <div className="row g-4">
              {recommendedCourses.slice(0, 4).map(course => (
                <div className="col-md-6 col-lg-3" key={course.courseId}>
                  <div className="card glass-card hover-3d h-100 border-0 p-2 position-relative">
                    <span className="position-absolute top-0 end-0 m-3 badge bg-primary text-white shadow-sm rounded-pill px-2.5 py-1 fw-bold" style={{ zIndex: 10 }}>
                      AI Match
                    </span>
                    <CourseCard course={course} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        )}

        {/* Popular Courses Block */}
        <div style={{ marginBottom: '48px' }}>
          <h3 style={{ fontSize: '22px', marginBottom: '20px', fontWeight: '700', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <TrendingUp className="text-primary" size={24} /> Bestselling & Top Rated
          </h3>
          {loading ? (
            <div className="row g-4">
              {[1, 2, 3, 4].map(n => (
                <div className="col-md-6 col-lg-3" key={n}>
                  <div className="card border-0 p-3 bg-white shadow-sm" style={{ height: '280px', borderRadius: '8px' }}>
                    <div className="bg-light rounded-4 w-100 mb-3" style={{ height: '140px' }}></div>
                    <div className="bg-light rounded w-75 mb-2" style={{ height: '20px' }}></div>
                    <div className="bg-light rounded w-50" style={{ height: '16px' }}></div>
                  </div>
                </div>
              ))}
            </div>
          ) : (
            <div style={{ 
              display: 'grid', 
              gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', 
              gap: '24px' 
            }}>
              {popularCourses.map(course => (
                <CourseCard key={course.courseId} course={course} />
              ))}
            </div>
          )}
        </div>

        {/* Newest Courses Block */}
        <div style={{ marginBottom: '48px' }}>
          <h3 style={{ fontSize: '22px', marginBottom: '20px', fontWeight: '700', display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Clock className="text-primary" size={24} /> Newly Published Courses
          </h3>
          {loading ? (
            <div className="text-center py-5">Loading newest arrivals...</div>
          ) : (
            <div style={{ 
              display: 'grid', 
              gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', 
              gap: '24px' 
            }}>
              {newestCourses.map(course => (
                <CourseCard key={course.courseId} course={course} />
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Top Categories Grid */}
      <div className="bg-light" style={{ padding: '60px 0', borderTop: '1px solid var(--border-color)' }}>
        <div className="container">
          <h2 style={{ textAlign: 'center', marginBottom: '12px', fontWeight: '800', fontSize: '26px' }}>Top Categories</h2>
          <p style={{ textLight: 'center', color: 'var(--text-muted)', marginBottom: '40px', textAlign: 'center' }}>
            Select your discipline of interest and master new techniques.
          </p>
          <div style={{ 
            display: 'grid', 
            gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', 
            gap: '24px' 
          }}>
            {loading || categoriesLoading ? (
              [1, 2, 3, 4].map(n => <div key={n} className="bg-white p-4 rounded text-center border">Loading category...</div>)
            ) : (
              [...categories].sort((a, b) => b.publishedCourseCount - a.publishedCourseCount || a.name.localeCompare(b.name)).slice(0, 8).map((cat) => (
                <Link to={`/courses?categoryId=${cat.categoryId}`} key={cat.categoryId} className="hover-3d" style={{ cursor: 'pointer', textAlign: 'center' }}>
                  <div style={{ backgroundColor: 'white', padding: '32px 24px', borderRadius: '12px', border: '1px solid var(--border-color)', height: '100%', display: 'flex', flexDirection: 'column', justifyContent: 'center' }}>
                    <h4 style={{ margin: 0, fontSize: '18px', color: 'var(--text-dark)', fontWeight: '600' }}>{cat.name}</h4>
                    <span className="small text-muted mt-2">{cat.publishedCourseCount} khóa học →</span>
                  </div>
                </Link>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

export default HomePage;
