import { Link } from 'react-router-dom';

function PaymentSuccess() {
  return (
    <div className="row justify-content-center mt-5">
      <div className="col-md-6 text-center">
        <div className="glass-card p-5">
          <div className="d-inline-flex bg-success bg-opacity-10 text-success p-3 rounded-circle mb-4" style={{width: '80px', height: '80px'}}>
            <i className="bi bi-check-circle-fill" style={{fontSize: '40px', lineHeight: '48px'}}></i>
          </div>
          <h1 className="display-5 fw-bold mb-3">Payment Successful!</h1>
          <p className="lead text-muted mb-4">
            Thank you for your purchase. Your order has been processed and you are now enrolled in the course.
          </p>
          <div className="d-flex justify-content-center gap-3">
            <Link to="/courses" className="btn btn-outline-primary rounded-pill px-4">Browse More Courses</Link>
          </div>
        </div>
      </div>
    </div>
  );
}

export default PaymentSuccess;
