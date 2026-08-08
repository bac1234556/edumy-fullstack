import React, { useState, useEffect } from 'react';
import api from '../api/axiosConfig';
import { toast } from 'react-hot-toast';
import { Award, Clock, CheckCircle2, AlertTriangle, RefreshCw, BarChart2 } from 'lucide-react';

export default function CourseQuizTaker({ courseId }) {
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [quiz, setQuiz] = useState(null);
  const [answers, setAnswers] = useState({});
  const [result, setResult] = useState(null);
  const [attempts, setAttempts] = useState([]);
  const [activeTab, setActiveTab] = useState('take'); // 'take' or 'history'

  const fetchQuiz = async () => {
    try {
      const { data } = await api.get(`/courses/${courseId}/final-quiz`);
      setQuiz(data);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const fetchAttempts = async () => {
    try {
      const { data } = await api.get(`/courses/${courseId}/final-quiz/attempts`);
      setAttempts(data || []);
    } catch (err) {
      console.error(err);
    }
  };

  useEffect(() => {
    fetchQuiz();
    fetchAttempts();
  }, [courseId]);

  const handleSelectOption = (questionId, optionId) => {
    setAnswers(prev => ({
      ...prev,
      [questionId]: optionId
    }));
  };

  const handleSubmit = async () => {
    // Check if all questions are answered
    if (quiz.questions.some(q => !answers[q.courseQuizQuestionId])) {
      toast.error('Vui lòng trả lời đầy đủ tất cả câu hỏi trước khi nộp bài.');
      return;
    }

    setSubmitting(true);
    try {
      const { data } = await api.post(`/courses/${courseId}/final-quiz/submit`, { answers });
      setResult(data);
      toast.success('Nộp bài thành công!');
      fetchAttempts();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể nộp bài làm.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleRetry = () => {
    setResult(null);
    setAnswers({});
  };

  if (loading) {
    return <div className="text-center py-5">Đang tải câu hỏi Final Quiz...</div>;
  }

  if (!quiz) {
    return (
      <div className="card text-center p-5 border-0 shadow-sm bg-white" style={{ borderRadius: '16px' }}>
        <Award size={48} className="text-muted mx-auto mb-3" />
        <h3 className="fw-bold">Final Quiz</h3>
        <p className="text-muted">Khóa học này chưa có Final Quiz hoặc quiz chưa được kích hoạt.</p>
      </div>
    );
  }

  if (result) {
    return (
      <div className="card shadow-sm border-0 p-4 bg-white" style={{ borderRadius: '16px' }}>
        <div className="text-center mb-4">
          {result.isPassed ? (
            <div className="text-success mb-2">
              <CheckCircle2 size={64} className="mx-auto" />
              <h2 className="fw-bold mt-2">Chúc mừng! Bạn đã Đạt</h2>
            </div>
          ) : (
            <div className="text-danger mb-2">
              <AlertTriangle size={64} className="mx-auto" />
              <h2 className="fw-bold mt-2">Rất tiếc! Bạn chưa Đạt</h2>
            </div>
          )}
          <p className="text-muted">
            Kết quả bài làm Final Quiz: <strong>{result.score}%</strong> (Yêu cầu để đạt: {quiz.passingScore}%)
          </p>
          <div className="d-flex justify-content-center gap-4 py-2 border rounded bg-light max-w-sm mx-auto">
            <div>
              <small className="text-muted d-block">Số câu đúng</small>
              <strong className="fs-5">{result.correctAnswers} / {result.totalQuestions}</strong>
            </div>
            <div>
              <small className="text-muted d-block">Trạng thái</small>
              <strong className={result.isPassed ? 'text-success' : 'text-danger'}>
                {result.isPassed ? 'PASSED' : 'FAILED'}
              </strong>
            </div>
          </div>
        </div>

        <div className="d-flex justify-content-center gap-3">
          <button className="btn btn-primary" onClick={handleRetry}>
            <RefreshCw size={16} className="me-1" /> Làm lại bài thi
          </button>
        </div>

        <h4 className="fw-bold mt-5 mb-3 d-flex align-items-center gap-2">
          <Clock size={18} className="text-muted" /> Lịch sử làm bài của bạn
        </h4>
        <AttemptsTable attempts={attempts} />
      </div>
    );
  }

  return (
    <div className="card shadow-sm border-0 p-4 bg-white" style={{ borderRadius: '16px' }}>
      <div className="d-flex justify-content-between align-items-center border-bottom pb-3 mb-4">
        <div>
          <h2 className="h4 fw-bold mb-1">{quiz.title}</h2>
          <p className="text-muted small mb-0">
            Bài kiểm tra cuối khóa • Đạt khi đạt tối thiểu <strong>{quiz.passingScore}%</strong> điểm.
          </p>
        </div>
        <button 
          className="btn btn-sm btn-outline-secondary" 
          onClick={() => setActiveTab(activeTab === 'take' ? 'history' : 'take')}
        >
          {activeTab === 'take' ? (
            <>
              <BarChart2 size={14} className="me-1" /> Xem lịch sử ({attempts.length})
            </>
          ) : (
            'Quay lại làm bài'
          )}
        </button>
      </div>

      {activeTab === 'history' ? (
        <div>
          <h4 className="fw-bold mb-3 d-flex align-items-center gap-2">Lịch sử làm bài</h4>
          <AttemptsTable attempts={attempts} />
        </div>
      ) : (
        <div>
          {quiz.questions.map((q, qIdx) => (
            <div key={q.courseQuizQuestionId} className="mb-4 p-3 border rounded-3 bg-light">
              <h5 className="fw-bold mb-3">
                Câu {qIdx + 1}: {q.questionText}
              </h5>
              <div className="d-flex flex-column gap-2">
                {q.options.map(opt => {
                  const isSelected = answers[q.courseQuizQuestionId] === opt.courseQuizOptionId;
                  return (
                    <label 
                      key={opt.courseQuizOptionId} 
                      className={`d-flex align-items-center gap-3 p-2.5 rounded-3 border bg-white cursor-pointer hover-bg-light transition-all`}
                      style={{ border: isSelected ? '2px solid var(--primary)' : '1px solid #dee2e6' }}
                    >
                      <input
                        type="radio"
                        className="form-check-input"
                        name={`q-${q.courseQuizQuestionId}`}
                        checked={isSelected}
                        onChange={() => handleSelectOption(q.courseQuizQuestionId, opt.courseQuizOptionId)}
                      />
                      <span>{opt.optionText}</span>
                    </label>
                  );
                })}
              </div>
            </div>
          ))}

          <div className="text-end mt-4">
            <button 
              className="btn btn-primary px-5 py-2.5 fw-semibold"
              onClick={handleSubmit}
              disabled={submitting}
              style={{ borderRadius: '8px' }}
            >
              {submitting ? 'Đang chấm điểm...' : 'Nộp bài làm'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function AttemptsTable({ attempts }) {
  if (attempts.length === 0) {
    return <p className="text-muted small">Bạn chưa có lượt làm bài nào.</p>;
  }

  return (
    <div className="table-responsive">
      <table className="table table-sm table-hover align-middle mb-0 text-center">
        <thead className="table-light">
          <tr>
            <th>Thời gian</th>
            <th>Điểm số</th>
            <th>Câu trả lời</th>
            <th>Kết quả</th>
          </tr>
        </thead>
        <tbody>
          {attempts.map(att => (
            <tr key={att.courseQuizAttemptId}>
              <td>{new Date(att.submittedAt).toLocaleString('vi-VN')}</td>
              <td className="fw-bold">{att.score}%</td>
              <td>{att.correctAnswers} / {att.totalQuestions} đúng</td>
              <td>
                <span className={`badge ${att.isPassed ? 'bg-success' : 'bg-danger'}`}>
                  {att.isPassed ? 'Đạt' : 'Chưa đạt'}
                </span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
