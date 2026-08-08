import React, { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import { formatCurrencyVN } from '../utils/format';
import { toast } from 'react-hot-toast';

function MockPaymentGateway() {
  const [searchParams] = useSearchParams();
  const orderId = searchParams.get('orderId');
  const amount = searchParams.get('amount');
  const navigate = useNavigate();
  const [processing, setProcessing] = useState(false);

  useEffect(() => {
    if (!orderId) {
      navigate('/cart');
    }
  }, [orderId, navigate]);

  const handlePayment = async (success) => {
    setProcessing(true);
    try {
      if (success) {
        await api.post('/payment/simulate-success', {
          orderId: parseInt(orderId)
        });
      } else {
        await api.post('/payment/callback', {
          orderId: parseInt(orderId),
          success: false
        });
      }
      if (success) {
        navigate('/payment-success');
      } else {
        navigate('/payment-cancel');
      }
    } catch (error) {
      toast.error('Payment processing error');
      setProcessing(false);
    }
  };

  return (
    <div className="container mt-5 mb-5 d-flex justify-content-center">
      <div className="card shadow-lg border-0" style={{ maxWidth: '500px', width: '100%' }}>
        <div className="card-header bg-primary text-white text-center py-4">
          <h3 className="mb-0">EduMy Pay</h3>
          <p className="mb-0 opacity-75">Secure Mock Payment Gateway</p>
        </div>
        <div className="card-body p-4">
          <div className="text-center mb-4">
            <h5 className="text-muted mb-2">Total Amount to Pay</h5>
            <h1 className="display-4 fw-bold text-primary">{formatCurrencyVN(parseFloat(amount || 0))}</h1>
            <p className="text-muted">Order ID: #{orderId}</p>
          </div>

          <hr className="my-4" />

          {processing ? (
            <div className="text-center py-4">
              <div className="spinner-border text-primary" role="status">
                <span className="visually-hidden">Processing...</span>
              </div>
              <p className="mt-3 text-muted">Processing your payment...</p>
            </div>
          ) : (
            <div className="d-grid gap-3">
              <button 
                className="btn btn-success btn-lg fw-bold d-flex align-items-center justify-content-center gap-2"
                onClick={() => handlePayment(true)}
              >
                <i className="bi bi-check-circle-fill"></i>
                Simulate Successful Payment
              </button>
              
              <button 
                className="btn btn-outline-danger btn-lg fw-bold d-flex align-items-center justify-content-center gap-2"
                onClick={() => handlePayment(false)}
              >
                <i className="bi bi-x-circle-fill"></i>
                Simulate Failed Payment
              </button>
            </div>
          )}

          <div className="text-center mt-4 text-muted small">
            <i className="bi bi-shield-lock-fill text-success me-1"></i>
            This is a mock gateway for testing purposes only. No real money will be charged.
          </div>
        </div>
      </div>
    </div>
  );
}

export default MockPaymentGateway;
