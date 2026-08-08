import React, { useState, useEffect, useContext } from 'react';
import { useParams, Link, useLocation } from 'react-router-dom';
import api from '../api/axiosConfig';
import { AuthContext } from '../context/AuthContext';
import { PlayCircle, CheckCircle, ArrowLeft, HelpCircle, MessageCircle, Award } from 'lucide-react';
import { toast } from 'react-hot-toast';
import CourseQuizTaker from '../components/CourseQuizTaker';
import QuizTaker from '../components/QuizTaker';
import QuizResults from '../components/QuizResults';
import CourseDiscussionDrawer from '../components/CourseDiscussionDrawer';
import LessonResourceViewer from '../components/LessonResourceViewer';
import './CoursePlayer.css';

const BACKEND_URL = (import.meta.env.VITE_API_URL || '/api').replace('/api', '');

function CoursePlayer() {
  const { id } = useParams();
  const location = useLocation();
  const [course, setCourse] = useState(null);
  const [curriculum, setCurriculum] = useState([]);
  const [selectedLessonId, setSelectedLessonId] = useState(null);
  const [selectedQuiz, setSelectedQuiz] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [completedLessons, setCompletedLessons] = useState([]);
  const [progressPercent, setProgressPercent] = useState(0);
  const [completedCount, setCompletedCount] = useState(0);
  const [totalLessons, setTotalLessons] = useState(0);
  const [certificateUrl, setCertificateUrl] = useState(null);
  const [quizResult, setQuizResult] = useState(null);
  const [qaOpen, setQaOpen] = useState(false);
  const [initialThreadId, setInitialThreadId] = useState(null);
  const [initialMessageId, setInitialMessageId] = useState(null);
  
  const { user, isStudent } = useContext(AuthContext);

  const activeLesson = React.useMemo(() => {
    if (selectedQuiz) return selectedQuiz;
    if (!curriculum || curriculum.length === 0) return null;
    const allLessons = curriculum.flatMap(s => s.lessons || []);
    if (allLessons.length === 0) return null;
    if (!selectedLessonId) return allLessons[0];
    return allLessons.find(l => l.lessonId === selectedLessonId) || allLessons[0];
  }, [curriculum, selectedLessonId, selectedQuiz]);

  useEffect(() => {
    const searchParams = new URLSearchParams(location.search);
    const discussion = searchParams.get('discussion') || searchParams.get('thread');
    const message = searchParams.get('message');
    const tab = searchParams.get('tab');
    const lessonParam = searchParams.get('lessonId');

    if (lessonParam) {
      setSelectedLessonId(Number(lessonParam));
      setSelectedQuiz(null);
    }

    if (discussion) {
      setInitialThreadId(discussion);
      if (message) setInitialMessageId(message);
      setQaOpen(true);
    } else if (tab === 'discussions') {
      setQaOpen(true);
    }
  }, [location.search]);

  useEffect(() => {
    const fetchData = async () => {
      try {
        let sections = [];
        const isInstructorPreview = location.pathname.startsWith('/instructor/');
        const isAdminPreview = location.pathname.startsWith('/admin/');

        if (isInstructorPreview) {
          const { data } = await api.get(`/instructor/courses/${id}/preview`);
          setCourse(data.course);
          sections = data.sections || [];
          setCurriculum(sections);
        } else if (isAdminPreview) {
          const { data } = await api.get(`/admin/courses/${id}/preview`);
          setCourse(data.course);
          sections = data.sections || [];
          setCurriculum(sections);
        } else {
          const { data } = await api.get(`/my-courses/${id}/learn`);
          setCourse(data.course);
          sections = data.sections || [];
          setCurriculum(sections);
          setCompletedLessons(sections.flatMap(section => section.lessons || []).filter(lesson => lesson.isCompleted).map(lesson => lesson.lessonId));
          setProgressPercent(data.progressPercentage || 0);
          setCompletedCount(data.completedLessons || 0);
          setTotalLessons(data.totalLessons || 0);
          setCertificateUrl(data.certificateUrl || null);
        }

        if (sections.length > 0) {
          const allLessons = sections.flatMap(s => s.lessons || []);
          if (allLessons.length > 0) {
            setSelectedLessonId(prev => prev || allLessons[0].lessonId);
          }
        }

      } catch (err) {
        setError(err.response?.data?.message || 'Bạn chưa đăng ký khóa học này.');
      } finally {
        setLoading(false);
      }
    };
    
    fetchData();
  }, [id, location.pathname]);

  const handleToggleCompleted = async () => {
    if (!activeLesson) return;
    try {
      const wasCompleted = completedLessons.includes(activeLesson.lessonId);
      const { data } = wasCompleted
        ? await api.delete(`/my-courses/${id}/lessons/${activeLesson.lessonId}/complete`)
        : await api.post(`/my-courses/${id}/lessons/${activeLesson.lessonId}/complete`);
      setCompletedLessons(previous => data.isCompleted
        ? [...new Set([...previous, data.lessonId])]
        : previous.filter(lessonId => lessonId !== data.lessonId));
      setCurriculum(previous => previous.map(section => ({ ...section, lessons: (section.lessons || []).map(lesson => lesson.lessonId === data.lessonId ? { ...lesson, isCompleted: data.isCompleted } : lesson) })));
      setProgressPercent(data.progressPercentage);
      setCompletedCount(data.completedLessons);
      setTotalLessons(data.totalLessons);
      toast.success(data.isCompleted ? 'Đã hoàn thành bài học.' : 'Đã bỏ đánh dấu hoàn thành.');
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể cập nhật tiến độ.');
    }
  };

  if (loading) return <div className="player-loading">Loading Player...</div>;
  if (error) return <div className="player-error">{error}</div>;

  return (
    <div className="course-player-container">
      <div className="player-header d-flex justify-content-between align-items-center">
        <div className="d-flex align-items-center gap-3">
          <Link to="/my-courses" className="back-link"><ArrowLeft size={18} /> Dashboard</Link>
          <h2 className="mb-0">{course?.title}</h2>
        </div>
        <div className="d-flex align-items-center gap-2">
          <div className="progress" style={{ width: '150px', height: '10px' }}>
            <div className="progress-bar bg-success" role="progressbar" style={{ width: `${progressPercent}%` }}></div>
          </div>
          <span className="text-light small">{progressPercent}%</span>
          {isStudent && <span className="text-light small">{completedCount}/{totalLessons} bài</span>}
          {certificateUrl && (
            <Link to={`/certificates/${certificateUrl}`} target="_blank" className="btn btn-sm btn-warning ms-3 fw-bold">
              View Certificate
            </Link>
          )}
          <button className="btn btn-sm btn-outline-light ms-2" onClick={() => setQaOpen(true)}><MessageCircle size={16}/> Hỏi đáp</button>
        </div>
      </div>

      <div className="player-layout">
        {/* Main Video Area */}
        <div className="player-main">
          {activeLesson?.isFinalQuiz ? (
            <CourseQuizTaker courseId={Number(id)} />
          ) : activeLesson?.isQuiz ? (
            quizResult ? (
              <QuizResults 
                result={quizResult} 
                quizTitle={activeLesson.title} 
                onRetry={() => setQuizResult(null)} 
              />
            ) : (
              <QuizTaker 
                quizId={activeLesson.quizId} 
                onComplete={(result) => setQuizResult(result)} 
              />
            )
          ) : activeLesson ? (
            <LessonResourceViewer lesson={activeLesson} />
          ) : (
            <div className="no-video-placeholder">No content found.</div>
          )}
          
          {!activeLesson?.isQuiz && !activeLesson?.isFinalQuiz && activeLesson && (
            <div className="lesson-details d-flex justify-content-between align-items-center">
              <h3>{activeLesson?.title}</h3>
              {isStudent && <button 
                className={`btn btn-sm ${completedLessons.includes(activeLesson.lessonId) ? 'btn-success' : 'btn-outline-primary'}`}
                onClick={handleToggleCompleted}
              >
                {completedLessons.includes(activeLesson.lessonId) ? 'Bỏ hoàn thành' : 'Hoàn thành bài học'}
              </button>}
            </div>
          )}
        </div>

        {/* Sidebar Curriculum */}
        <div className="player-sidebar">
          <div className="sidebar-header">
            <h4>Course Content</h4>
          </div>
          <div className="curriculum-accordion">
            {curriculum.map((section, idx) => (
              <div className="player-section" key={section.sectionId}>
                <div className="section-header">
                  <strong>Section {idx + 1}: {section.title}</strong>
                </div>
                <div className="section-lessons">
                  {section.lessons?.map((lesson, lIdx) => (
                    <div 
                      key={lesson.lessonId}
                      className={`player-lesson-item ${activeLesson?.lessonId === lesson.lessonId && !activeLesson?.isQuiz ? 'active' : ''}`}
                      onClick={() => {
                        setSelectedLessonId(lesson.lessonId);
                        setSelectedQuiz(null);
                      }}
                    >
                      <div className="lesson-icon">
                        <CheckCircle size={14} color={completedLessons.includes(lesson.lessonId) ? "#22c55e" : "#94a3b8"} />
                      </div>
                      <div className="lesson-info">
                        <span className="lesson-title">{lIdx + 1}. {lesson.title}</span>
                        <span className="lesson-time">{lesson.duration ? `${Math.floor(lesson.duration/60)}:${String(lesson.duration%60).padStart(2, '0')}` : '--:--'}</span>
                      </div>
                    </div>
                  ))}
                  {section.quizzes?.map((quiz, qIdx) => (
                    <div 
                      key={`quiz-${quiz.quizId}`}
                      className={`player-lesson-item ${activeLesson?.quizId === quiz.quizId ? 'active' : ''}`}
                      onClick={() => {
                        setSelectedQuiz({ ...quiz, isQuiz: true });
                        setQuizResult(null);
                      }}
                    >
                      <div className="lesson-icon">
                        <HelpCircle size={14} color="#f59e0b" />
                      </div>
                      <div className="lesson-info">
                        <span className="lesson-title">Quiz: {quiz.title}</span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}

            <div className="player-section mt-3">
              <div 
                className={`player-lesson-item p-3 border rounded-3 d-flex align-items-center gap-2 ${activeLesson?.isFinalQuiz ? 'active bg-light border-primary' : 'bg-white'}`}
                style={{ cursor: 'pointer' }}
                onClick={() => {
                  setSelectedQuiz({ isFinalQuiz: true, title: 'Bài kiểm tra cuối khóa' });
                  setQuizResult(null);
                }}
              >
                <Award size={16} className={activeLesson?.isFinalQuiz ? 'text-primary' : 'text-warning'} />
                <span className="fw-semibold small">Bài kiểm tra cuối khóa (Final Quiz)</span>
              </div>
            </div>
          </div>
        </div>
      </div>
      <CourseDiscussionDrawer courseId={id} open={qaOpen} onClose={() => setQaOpen(false)} user={user} initialThreadId={initialThreadId} initialMessageId={initialMessageId} />
    </div>
  );
}

export default CoursePlayer;
