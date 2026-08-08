import React, { useState, useEffect } from 'react';
import api from '../api/axiosConfig';
import { toast } from 'react-hot-toast';
import { Plus, Trash, HelpCircle, Save, Check, X } from 'lucide-react';

export default function CourseQuizEditor({ courseId }) {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [quiz, setQuiz] = useState({
    title: 'Bài kiểm tra cuối khóa',
    passingScore: 80,
    isActive: true,
    questions: []
  });

  useEffect(() => {
    const fetchQuiz = async () => {
      try {
        const { data } = await api.get(`/courses/${courseId}/final-quiz`);
        if (data) {
          setQuiz({
            title: data.title || 'Bài kiểm tra cuối khóa',
            passingScore: data.passingScore || 80,
            isActive: data.isActive ?? true,
            questions: data.questions || []
          });
        }
      } catch (err) {
        if (err.response?.status === 404) {
          // If quiz doesn't exist yet, we keep the default state
          setQuiz({
            title: 'Bài kiểm tra cuối khóa',
            passingScore: 80,
            isActive: true,
            questions: [
              {
                questionText: 'Câu hỏi mẫu số 1?',
                orderIndex: 1,
                options: [
                  { optionText: 'Đáp án A (Đúng)', isCorrect: true },
                  { optionText: 'Đáp án B', isCorrect: false }
                ]
              }
            ]
          });
        } else {
          toast.error('Không thể tải thông tin Final Quiz.');
        }
      } finally {
        setLoading(false);
      }
    };

    fetchQuiz();
  }, [courseId]);

  const handleQuizChange = (e) => {
    const { name, value, type, checked } = e.target;
    setQuiz(prev => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
  };

  const handleAddQuestion = () => {
    setQuiz(prev => ({
      ...prev,
      questions: [
        ...prev.questions,
        {
          questionText: '',
          orderIndex: prev.questions.length + 1,
          options: [
            { optionText: '', isCorrect: false },
            { optionText: '', isCorrect: false }
          ]
        }
      ]
    }));
  };

  const handleRemoveQuestion = (qIndex) => {
    setQuiz(prev => ({
      ...prev,
      questions: prev.questions.filter((_, idx) => idx !== qIndex)
    }));
  };

  const handleQuestionTextChange = (qIndex, value) => {
    setQuiz(prev => {
      const updated = [...prev.questions];
      updated[qIndex].questionText = value;
      return { ...prev, questions: updated };
    });
  };

  const handleAddOption = (qIndex) => {
    setQuiz(prev => {
      const updated = [...prev.questions];
      updated[qIndex].options.push({ optionText: '', isCorrect: false });
      return { ...prev, questions: updated };
    });
  };

  const handleRemoveOption = (qIndex, oIndex) => {
    setQuiz(prev => {
      const updated = [...prev.questions];
      updated[qIndex].options = updated[qIndex].options.filter((_, idx) => idx !== oIndex);
      return { ...prev, questions: updated };
    });
  };

  const handleOptionChange = (qIndex, oIndex, field, value) => {
    setQuiz(prev => {
      const updated = [...prev.questions];
      if (field === 'isCorrect') {
        // Only one option can be correct (single choice)
        updated[qIndex].options = updated[qIndex].options.map((opt, idx) => ({
          ...opt,
          isCorrect: idx === oIndex ? value : false
        }));
      } else {
        updated[qIndex].options[oIndex][field] = value;
      }
      return { ...prev, questions: updated };
    });
  };

  const handleSave = async () => {
    // Validate
    if (!quiz.title.trim()) {
      toast.error('Vui lòng nhập tiêu đề Quiz.');
      return;
    }
    if (quiz.questions.length === 0) {
      toast.error('Vui lòng thêm ít nhất 1 câu hỏi.');
      return;
    }

    for (let i = 0; i < quiz.questions.length; i++) {
      const q = quiz.questions[i];
      if (!q.questionText.trim()) {
        toast.error(`Câu hỏi số ${i + 1} chưa nhập nội dung câu hỏi.`);
        return;
      }
      if (q.options.length < 2) {
        toast.error(`Câu hỏi "${q.questionText}" phải có ít nhất 2 đáp án.`);
        return;
      }
      const hasCorrect = q.options.some(o => o.isCorrect);
      if (!hasCorrect) {
        toast.error(`Vui lòng chọn đáp án đúng cho câu hỏi: "${q.questionText}".`);
        return;
      }
      const hasEmptyOption = q.options.some(o => !o.optionText.trim());
      if (hasEmptyOption) {
        toast.error(`Vui lòng nhập đầy đủ nội dung tất cả đáp án cho câu hỏi: "${q.questionText}".`);
        return;
      }
    }

    setSaving(true);
    try {
      await api.post(`/courses/${courseId}/final-quiz`, quiz);
      toast.success('Lưu Final Quiz thành công!');
    } catch (err) {
      toast.error(err.response?.data?.message || 'Không thể lưu Final Quiz.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="text-center py-4">Đang tải thông tin Quiz...</div>;
  }

  return (
    <div className="card shadow-sm border-0 p-4 bg-white" style={{ borderRadius: '16px' }}>
      <div className="d-flex justify-content-between align-items-center mb-4">
        <h2 className="h4 mb-0 fw-bold d-flex align-items-center gap-2">
          <HelpCircle className="text-primary" /> Thiết lập Final Quiz
        </h2>
        <div className="form-check form-switch">
          <input
            className="form-check-input"
            type="checkbox"
            name="isActive"
            id="quizActiveSwitch"
            checked={quiz.isActive}
            onChange={handleQuizChange}
          />
          <label className="form-check-label small fw-bold" htmlFor="quizActiveSwitch">
            Kích hoạt Quiz
          </label>
        </div>
      </div>

      <div className="row g-3 mb-4">
        <div className="col-md-8">
          <label className="form-label fw-bold">Tiêu đề Quiz</label>
          <input
            type="text"
            className="form-control"
            name="title"
            value={quiz.title}
            onChange={handleQuizChange}
            placeholder="Ví dụ: Bài kiểm tra cuối khóa"
          />
        </div>
        <div className="col-md-4">
          <label className="form-label fw-bold">Điểm số vượt qua (%)</label>
          <input
            type="number"
            className="form-control"
            name="passingScore"
            min="10"
            max="100"
            value={quiz.passingScore}
            onChange={handleQuizChange}
          />
        </div>
      </div>

      <hr />

      <h3 className="h5 fw-bold mb-3">Danh sách câu hỏi</h3>

      {quiz.questions.map((q, qIndex) => (
        <div key={qIndex} className="card border rounded-3 p-3 mb-4 bg-light position-relative">
          <button
            type="button"
            className="btn btn-sm btn-outline-danger position-absolute"
            style={{ top: '15px', right: '15px' }}
            onClick={() => handleRemoveQuestion(qIndex)}
            title="Xóa câu hỏi"
          >
            <Trash size={16} />
          </button>

          <div className="mb-3 me-5">
            <label className="form-label fw-bold">Câu hỏi #{qIndex + 1}</label>
            <input
              type="text"
              className="form-control bg-white"
              value={q.questionText}
              onChange={(e) => handleQuestionTextChange(qIndex, e.target.value)}
              placeholder="Nhập nội dung câu hỏi..."
            />
          </div>

          <div className="ps-3 border-start border-2 border-primary">
            <label className="form-label fw-bold small text-muted">Các đáp án lựa chọn</label>
            {q.options.map((opt, oIndex) => (
              <div key={oIndex} className="d-flex align-items-center gap-2 mb-2">
                <input
                  type="radio"
                  name={`correct-option-${qIndex}`}
                  checked={opt.isCorrect}
                  onChange={(e) => handleOptionChange(qIndex, oIndex, 'isCorrect', e.target.checked)}
                  title="Đặt làm đáp án đúng"
                  className="form-check-input"
                />
                <input
                  type="text"
                  className="form-control form-control-sm bg-white"
                  value={opt.optionText}
                  onChange={(e) => handleOptionChange(qIndex, oIndex, 'optionText', e.target.value)}
                  placeholder={`Đáp án ${String.fromCharCode(65 + oIndex)}...`}
                />
                {q.options.length > 2 && (
                  <button
                    type="button"
                    className="btn btn-sm btn-link text-danger p-1"
                    onClick={() => handleRemoveOption(qIndex, oIndex)}
                    title="Xóa đáp án"
                  >
                    <X size={16} />
                  </button>
                )}
              </div>
            ))}
            <button
              type="button"
              className="btn btn-sm btn-link text-primary p-0 mt-1 d-flex align-items-center gap-1"
              onClick={() => handleAddOption(qIndex)}
            >
              <Plus size={14} /> Thêm đáp án lựa chọn
            </button>
          </div>
        </div>
      ))}

      <button
        type="button"
        className="btn btn-outline-primary w-100 py-2.5 mb-4 fw-semibold border-dashed"
        onClick={handleAddQuestion}
      >
        <Plus size={18} className="me-1" /> Thêm câu hỏi trắc nghiệm
      </button>

      <div className="d-flex justify-content-end gap-2">
        <button
          type="button"
          className="btn btn-primary px-4 d-flex align-items-center gap-2"
          onClick={handleSave}
          disabled={saving}
        >
          <Save size={18} /> {saving ? 'Đang lưu...' : 'Lưu Final Quiz'}
        </button>
      </div>
    </div>
  );
}
