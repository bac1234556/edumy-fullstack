import React from 'react';

function QuizResults({ result, quizTitle, onRetry }) {
  if (!result) return null;

  const passClass = result.passed ? 'text-success' : 'text-danger';

  return (
    <div className="quiz-results p-4 bg-white rounded shadow-sm text-dark h-100 overflow-auto">
      <div className="text-center mb-5 mt-3">
        <h2 className="mb-3">Results for: {quizTitle}</h2>
        <h1 className={`display-4 fw-bold ${passClass}`}>
          {result.score} / {result.totalPoints}
        </h1>
        <h4 className={passClass}>
          {result.passed ? 'Congratulations! You passed.' : 'You did not pass. Keep practicing!'}
        </h4>
        
        {!result.passed && (
          <button className="btn btn-outline-primary mt-3" onClick={onRetry}>
            Retry Quiz
          </button>
        )}
      </div>

      <div className="results-breakdown mt-5">
        <h4 className="mb-4 border-bottom pb-2">Question Breakdown</h4>
        {result.results.map((r, i) => (
          <div key={r.questionId} className={`card mb-3 border-0 shadow-sm ${r.isCorrect ? 'border-start border-success border-4' : 'border-start border-danger border-4'}`}>
            <div className="card-body">
              <h5 className="card-title">
                Question {i + 1} 
                {r.isCorrect ? (
                  <span className="badge bg-success ms-2">Correct</span>
                ) : (
                  <span className="badge bg-danger ms-2">Incorrect</span>
                )}
              </h5>
              {r.explanation && (
                <div className="mt-3 p-3 bg-light rounded text-muted">
                  <strong>Explanation:</strong> {r.explanation}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export default QuizResults;
