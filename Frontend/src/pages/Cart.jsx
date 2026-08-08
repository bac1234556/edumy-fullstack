import React, { useState, useEffect, useContext } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import api from '../api/axiosConfig';
import { AuthContext } from '../context/AuthContext';
import { formatCurrencyVN } from '../utils/format';
import './Cart.css';
import { toast } from 'react-hot-toast';
import CourseThumbnail from '../components/CourseThumbnail';

const BACKEND_URL = (import.meta.env.VITE_API_URL || '/api').replace('/api', '');

function Cart() {
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [checkoutLoading, setCheckoutLoading] = useState(false);
  const [couponCode, setCouponCode] = useState('');
  const [appliedCoupon, setAppliedCoupon] = useState(null);
  const [couponError, setCouponError] = useState('');
  const [couponLoading, setCouponLoading] = useState(false);
  
  const { user } = useContext(AuthContext);
  const navigate = useNavigate();

  useEffect(() => {
    fetchCart();
  }, [user]);

  const fetchCart = async () => {
    if (!user) {
      setLoading(false);
      return;
    }
    try {
      const response = await api.get('/cart');
      setCart(response.data);
    } catch (err) {
      setError('Could not fetch cart.');
    } finally {
      setLoading(false);
    }
  };

  const handleRemove = async (courseId) => {
    try {
      await api.delete(`/cart/remove/${courseId}`);
      fetchCart();
    } catch (err) {
      toast.error('Failed to remove course.');
    }
  };

  const handleApplyCoupon = async () => {
    if (!couponCode) return;
    setCouponLoading(true);
    setCouponError('');
    try {
      const res = await api.post('/coupons/validate', { code: couponCode });
      setAppliedCoupon({
        code: res.data.code,
        discountType: res.data.discountType,
        discountValue: res.data.discountValue,
        discountPercentage: res.data.discountPercentage,
        message: res.data.message
      });
      setCouponCode('');
    } catch (err) {
      setCouponError(err.response?.data?.message || 'Invalid coupon.');
      setAppliedCoupon(null);
    } finally {
      setCouponLoading(false);
    }
  };

  const handleCheckout = async () => {
    try {
      setCheckoutLoading(true);
      const res = await api.post('/orders/checkout', {
        couponCode: appliedCoupon?.code || null
      });
      // Redirect to mock payment gateway
      navigate(res.data.paymentUrl);
    } catch (err) {
      toast.error(err.response?.data?.message || 'Checkout failed.');
    } finally {
      setCheckoutLoading(false);
    }
  };

  if (!user) {
    return (
      <div className="container text-center mt-5 mb-5" style={{padding: '5rem 0'}}>
        <h2>Please <Link to="/login">login</Link> to view your cart.</h2>
      </div>
    );
  }

  if (loading) return <div className="container text-center mt-5 mb-5" style={{padding: '5rem 0'}}>Loading cart...</div>;
  if (error) return <div className="container text-center mt-5 mb-5 error-msg">{error}</div>;

  return (
    <div className="cart-page container">
      <h1>Shopping Cart</h1>
      
      {!cart || !cart.items || cart.items.length === 0 ? (
        <div className="empty-cart">
          <p>Your cart is empty. Keep shopping to find a course!</p>
          <Link to="/" className="btn-udemy-primary">Keep shopping</Link>
        </div>
      ) : (
        <div className="cart-content">
          <div className="cart-items">
            <h3>{cart.totalItems} Course{cart.totalItems > 1 ? 's' : ''} in Cart</h3>
            {cart.items.map(item => (
              <div key={item.id} className="cart-item">
                <CourseThumbnail src={item.course.thumbnailUrl} categoryName={item.course.categoryName} alt={item.course.title || 'Course'} className="cart-item-img" />
                <div className="cart-item-info">
                  <h4><Link to={`/courses/${item.courseId}`}>{item.course.title}</Link></h4>
                  <p className="instructor">By {item.course.instructor?.fullName || 'Instructor'}</p>
                  <button className="remove-btn" onClick={() => handleRemove(item.courseId)}>Remove</button>
                </div>
                <div className="cart-item-price">
                  <div className="price">{formatCurrencyVN(item.course.price)}</div>
                </div>
              </div>
            ))}
          </div>

          <div className="cart-summary p-4 bg-light rounded shadow-sm">
            <h3 className="mb-4">Total:</h3>
            <div className="total-price mb-3" style={appliedCoupon ? {textDecoration: 'line-through', color: '#6c757d', fontSize: '1.2rem'} : {}}>
              {formatCurrencyVN(cart.totalPrice)}
            </div>
            
            {appliedCoupon && (
              <div className="discount-price mb-3 text-success fw-bold fs-4">
                {formatCurrencyVN(Math.max(0, cart.totalPrice - (appliedCoupon.discountType === 'FixedAmount' ? appliedCoupon.discountValue : cart.totalPrice * appliedCoupon.discountValue / 100)))}
                <div className="small text-muted fw-normal">{appliedCoupon.discountType === 'FixedAmount' ? formatCurrencyVN(appliedCoupon.discountValue) : `${appliedCoupon.discountValue}%`} off applied!</div>
              </div>
            )}

            <div className="coupon-section mb-4">
              <div className="input-group">
                <input 
                  type="text" 
                  className="form-control" 
                  placeholder="Enter coupon code" 
                  value={couponCode}
                  onChange={(e) => setCouponCode(e.target.value)}
                />
                <button 
                  className="btn btn-outline-secondary" 
                  type="button"
                  onClick={handleApplyCoupon}
                  disabled={couponLoading || !couponCode}
                >
                  {couponLoading ? '...' : 'Apply'}
                </button>
              </div>
              {couponError && <div className="text-danger small mt-1">{couponError}</div>}
              {appliedCoupon && <div className="text-success small mt-1">{appliedCoupon.message}</div>}
            </div>

            <button className="btn btn-primary w-100 fw-bold py-2" onClick={handleCheckout} disabled={checkoutLoading}>
              {checkoutLoading ? 'Processing...' : 'Checkout'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

export default Cart;
