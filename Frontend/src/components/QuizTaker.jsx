import React, { useState, useEffect } from 'react';
import api from '../api/axiosConfig';

function QuizTaker({ quizId, onComplete }) {
  const [quiz, setQuiz] = useState(null);
  const [loading, setLoading] = useState(true);
  const [answers, setAnswers] = useState({}); // questionId -> answerId
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    const fetchQuiz = async () => {
      try {
        const res = await api.get(`/quizzes/${quizId}`);
        setQuiz(res.data);
      } catch (err) {
        setError("Failed to load quiz.");
      } finally {
        setLoading(false);
      }
    };
    fetchQuiz();
  }, [quizId]);

  const handleSelectAnswer = (questionId, answerId) => {
    setAnswers({ ...answers, [questionId]: answerId });
  };

  const handleSubmit = async () => {
    setSubmitting(true);
    try {
      const res = await api.post(`/quizattempts/submit`, {
        quizId: quizId,
        selectedAnswers: answers
      });
      if (onComplete) onComplete(res.data);
    } catch (err) {
      setError("Failed to submit quiz.");
      setSubmitting(false);
    }
  };

  if (loading) return <div>Loading quiz...</div>;
  if (error) return <div className="text-danger">{error}</div>;
  if (!quiz) return <div>Quiz not found.</div>;

  return (
    <div className="quiz-taker p-4 bg-white rounded shadow-sm text-dark h-100 overflow-auto">
      <h3 className="mb-2">{quiz.title}</h3>
      <p className="text-muted mb-4">{quiz.description}</p>
      
      {quiz.questions.map((q, i) => (
        <div key={q.questionId} className="card mb-4 shadow-sm border-0 bg-light">
          <div className="card-body">
            <h5 className="card-title mb-3">
              Question {i + 1} <span className="badge bg-secondary ms-2">{q.points} pts</span>
            </h5>
            <p className="card-text fw-medium">{q.content}</p>
            <div className="d-flex flex-column gap-2 mt-3">
              {q.answers.map(a => (
                <label 
                  key={a.answerId} 
                  className={`p-3 rounded border cursor-pointer ${answers[q.questionId] === a.answerId ? 'bg-primary text-white border-primary' : 'bg-white border-secondary'}`}
                  style={{ cursor: 'pointer' }}
                >
                  <input 
                    type="radio" 
                    name={`q-${q.questionId}`} 
                    className="d-none"
                    checked={answers[q.questionId] === a.answerId}
                    onChange={() => handleSelectAnswer(q.questionId, a.answerId)}
                  />
                  {a.content}
                </label>
              ))}
            </div>
          </div>
        </div>
      ))}

      <div className="d-flex justify-content-end mt-4">
        <button 
          className="btn btn-primary px-5 py-2 fw-bold" 
          onClick={handleSubmit}
          disabled={submitting || Object.keys(answers).length !== quiz.questions.length}
        >
          {submitting ? 'Submitting...' : 'Submit Quiz'}
        </button>
      </div>
    </div>
  );
}

export default QuizTaker;
