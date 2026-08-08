import React, { useContext, useState, useEffect, useRef } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Search, ShoppingCart, Bell, Heart, Loader2, ChevronDown, CheckCheck } from 'lucide-react';
import { AuthContext } from '../context/AuthContext';
import api from '../api/axiosConfig';
import './Navbar.css';
import { useCategories } from '../context/CategoryContext';
import CourseThumbnail from './CourseThumbnail';
import { toSafeString, getDisplayName, getUserInitials, normalizeNotificationTargetUrl } from '../utils/userUtils';

const Navbar = () => {
  const { user, logout, isStudent, isInstructor, isAdmin } = useContext(AuthContext);
  const navigate = useNavigate();
  const location = useLocation();
  const [searchQuery, setSearchQuery] = useState('');
  const [dropdownOpen, setDropdownOpen] = useState(false);
  const [activeCategory, setActiveCategory] = useState(-1);
  const [avatarDropdownOpen, setAvatarDropdownOpen] = useState(false);
  const [avatarImageError, setAvatarImageError] = useState(false);

  const displayName = getDisplayName(user);
  const initials = getUserInitials(user);
  const avatarUrl = toSafeString(user?.avatarUrl);

  useEffect(() => {
    setAvatarImageError(false);
  }, [avatarUrl]);

  const [cartCount, setCartCount] = useState(0);
  const [wishlistCount, setWishlistCount] = useState(0);
  const [notifications, setNotifications] = useState([]);
  const [unreadNotificationsCount, setUnreadNotificationsCount] = useState(0);
  const [notificationDropdownOpen, setNotificationDropdownOpen] = useState(false);
  const [suggestions, setSuggestions] = useState([]);
  const [suggestionsLoading, setSuggestionsLoading] = useState(false);
  const [suggestionsOpen, setSuggestionsOpen] = useState(false);
  const [activeSuggestion, setActiveSuggestion] = useState(-1);
  
  const searchRef = useRef(null);
  const categoryRef = useRef(null);
  const notifRef = useRef(null);
  const categoryCloseTimer = useRef(null);
  const { categories, loading: categoriesLoading, error: categoriesError, refetch: refetchCategories } = useCategories();

  // Reset/restore search box on route change
  useEffect(() => {
    const searchParams = new URLSearchParams(location.search);
    if (location.pathname === '/courses' && searchParams.has('search')) {
      setSearchQuery(searchParams.get('search') || '');
    } else {
      setSearchQuery('');
    }
    setSuggestionsOpen(false);
    setSuggestions([]);
    setActiveSuggestion(-1);
    setNotificationDropdownOpen(false);
    setAvatarDropdownOpen(false);
    setDropdownOpen(false);
  }, [location.pathname, location.search]);

  // Suggestions search debouncer
  useEffect(() => {
    const keyword = searchQuery.trim();
    if (keyword.length < 2) {
      setSuggestions([]); setSuggestionsOpen(false); setSuggestionsLoading(false); setActiveSuggestion(-1);
      return;
    }
    const controller = new AbortController();
    const timer = setTimeout(async () => {
      setSuggestionsLoading(true); setSuggestionsOpen(true);
      try {
        const response = await api.get('/courses/suggestions', { params: { keyword, limit: 8 }, signal: controller.signal });
        setSuggestions(response.data || []); setActiveSuggestion(-1);
      } catch (error) {
        if (error.code !== 'ERR_CANCELED') setSuggestions([]);
      } finally {
        if (!controller.signal.aborted) setSuggestionsLoading(false);
      }
    }, 300);
    return () => { clearTimeout(timer); controller.abort(); };
  }, [searchQuery]);

  // Search click outside
  useEffect(() => {
    const close = (event) => {
      if (searchRef.current && !searchRef.current.contains(event.target)) setSuggestionsOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  // Category dropdown keyboard & click outside
  useEffect(() => {
    const close = event => {
      if (!categoryRef.current?.contains(event.target)) { setDropdownOpen(false); setActiveCategory(-1); }
    };
    const escape = event => { if (event.key === 'Escape') { setDropdownOpen(false); setActiveCategory(-1); categoryRef.current?.querySelector('button')?.focus(); } };
    document.addEventListener('mousedown', close); document.addEventListener('keydown', escape);
    return () => { document.removeEventListener('mousedown', close); document.removeEventListener('keydown', escape); clearTimeout(categoryCloseTimer.current); };
  }, []);

  // Notification click outside (pointerdown) & Escape listener
  useEffect(() => {
    const handleOutsideClick = (e) => {
      if (notifRef.current && !notifRef.current.contains(e.target)) {
        setNotificationDropdownOpen(false);
      }
    };
    const handleEscape = (e) => {
      if (e.key === 'Escape') {
        setNotificationDropdownOpen(false);
      }
    };
    document.addEventListener('pointerdown', handleOutsideClick);
    document.addEventListener('keydown', handleEscape);
    return () => {
      document.removeEventListener('pointerdown', handleOutsideClick);
      document.removeEventListener('keydown', handleEscape);
    };
  }, []);

  // Reset avatar image error state when user object changes
  useEffect(() => {
    setAvatarImageError(false);
  }, [user?.avatarUrl]);

  const selectCategory = category => {
    navigate(category ? `/courses?categoryId=${category.categoryId}` : '/courses');
    setDropdownOpen(false); setActiveCategory(-1);
  };
  const categoryKeyDown = event => {
    const count = categories.length + 1;
    if (event.key === 'ArrowDown') { event.preventDefault(); setDropdownOpen(true); setActiveCategory(index => Math.min(index + 1, count - 1)); }
    else if (event.key === 'ArrowUp') { event.preventDefault(); setDropdownOpen(true); setActiveCategory(index => index <= 0 ? count - 1 : index - 1); }
    else if (event.key === 'Enter' && dropdownOpen && activeCategory >= 0) { event.preventDefault(); selectCategory(activeCategory === 0 ? null : categories[activeCategory - 1]); }
  };

  // Fetch Cart, Wishlist, and real Notifications
  const fetchUserData = async () => {
    if (!user) {
      setCartCount(0); setWishlistCount(0); setNotifications([]); setUnreadNotificationsCount(0);
      return;
    }

    if (isStudent) {
      try {
        const cartRes = await api.get('/cart');
        setCartCount(cartRes.data?.items?.length || 0);
      } catch (err) { /* quiet fallback */ }

      try {
        const wishlistRes = await api.get('/wishlist');
        setWishlistCount(wishlistRes.data?.length || 0);
      } catch (err) { /* quiet fallback */ }
    }

    try {
      const [listRes, countRes] = await Promise.allSettled([
        api.get('/notifications?page=1&pageSize=20'),
        api.get('/notifications/unread-count')
      ]);
      if (listRes.status === 'fulfilled') {
        setNotifications(listRes.value.data?.items || []);
      }
      if (countRes.status === 'fulfilled') {
        setUnreadNotificationsCount(countRes.value.data?.unreadCount || 0);
      }
    } catch (err) {
      console.error('Failed to fetch notifications', err);
    }
  };

  useEffect(() => {
    fetchUserData();
    if (!user) return;
    const interval = setInterval(fetchUserData, 15000);
    return () => clearInterval(interval);
  }, [user, isStudent]);

  const handleNotificationClick = async (notif) => {
    setNotificationDropdownOpen(false);
    const targetUrl = normalizeNotificationTargetUrl(notif.targetUrl);

    if (!notif.isRead) {
      setUnreadNotificationsCount(prev => Math.max(0, prev - 1));
      setNotifications(prev => prev.map(n => n.notificationId === notif.notificationId ? { ...n, isRead: true } : n));
      try {
        await api.put(`/notifications/${notif.notificationId}/read`);
      } catch (err) {
        console.error('Failed to mark notification read', err);
      }
    }

    if (!targetUrl) {
      toast.error("Thông báo này không có đường dẫn hợp lệ.");
      return;
    }

    navigate(targetUrl);
  };

  const handleMarkAllAsRead = async () => {
    try {
      await api.put('/notifications/read-all');
      setUnreadNotificationsCount(0);
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
    } catch (err) {
      console.error('Failed to mark all as read', err);
    }
  };

  const handleSearchSubmit = (e) => {
    e.preventDefault();
    if (searchQuery.trim()) {
      navigate(`/courses?search=${encodeURIComponent(searchQuery.trim())}`);
    } else {
      navigate('/courses');
    }
  };

  const handleSearchKeyDown = (e) => {
    if (!suggestionsOpen && e.key !== 'Escape') return;
    if (e.key === 'ArrowDown') { e.preventDefault(); setActiveSuggestion(i => Math.min(i + 1, suggestions.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setActiveSuggestion(i => Math.max(i - 1, 0)); }
    else if (e.key === 'Escape') { setSuggestionsOpen(false); setActiveSuggestion(-1); }
    else if (e.key === 'Enter' && activeSuggestion >= 0) {
      e.preventDefault(); const selected = suggestions[activeSuggestion];
      setSuggestionsOpen(false); navigate(`/courses/${selected.courseId}`);
    }
  };

  return (
    <nav className="navbar">
      <div className="navbar-container container">
        <Link to="/" className="navbar-logo">
          <span className="logo-text">Edumy</span>
        </Link>

        <div ref={categoryRef}
          className="navbar-categories-container"
          onMouseEnter={() => { if (window.matchMedia('(hover: hover)').matches) { clearTimeout(categoryCloseTimer.current); setDropdownOpen(true); } }}
          onMouseLeave={() => { if (window.matchMedia('(hover: hover)').matches) categoryCloseTimer.current = setTimeout(() => setDropdownOpen(false), 180); }}
        >
          <button type="button" className="navbar-categories" aria-haspopup="menu" aria-expanded={dropdownOpen}
            onClick={() => { setDropdownOpen(open => !open); setActiveCategory(-1); }} onKeyDown={categoryKeyDown}>
            Categories <ChevronDown size={15} aria-hidden="true" />
          </button>
          {dropdownOpen && (
            <div className="categories-dropdown" role="menu" onKeyDown={categoryKeyDown}>
              <button type="button" role="menuitem" className={`dropdown-item ${activeCategory === 0 ? 'active' : ''}`} onMouseEnter={() => setActiveCategory(0)} onClick={() => selectCategory(null)}>Tất cả danh mục</button>
              {categoriesLoading && <div className="dropdown-state"><Loader2 size={16} className="spin" /> Đang tải danh mục...</div>}
              {categoriesError && <button type="button" className="dropdown-state text-danger" onClick={refetchCategories}>Không thể tải. Thử lại</button>}
              {!categoriesLoading && !categoriesError && categories.length === 0 && <div className="dropdown-state">Chưa có danh mục.</div>}
              {categories.map((cat, index) => (
                <button type="button" role="menuitem" key={cat.categoryId} className={`dropdown-item ${activeCategory === index + 1 ? 'active' : ''}`}
                  onMouseEnter={() => setActiveCategory(index + 1)} onClick={() => selectCategory(cat)}>
                  <span>{cat.name}</span><small>{cat.publishedCourseCount} khóa học</small>
                </button>
              ))}
            </div>
          )}
        </div>

        <form onSubmit={handleSearchSubmit} className="navbar-search" ref={searchRef}>
          <button type="submit" className="search-btn">
            <Search size={18} color="var(--text-muted)" />
          </button>
          <input 
            type="text" 
            placeholder="Search for anything" 
            className="search-input"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            onFocus={() => searchQuery.trim().length >= 2 && setSuggestionsOpen(true)}
            onKeyDown={handleSearchKeyDown}
            role="combobox"
            aria-expanded={suggestionsOpen}
            aria-controls="course-suggestions"
          />
          {suggestionsOpen && (
            <div className="search-suggestions" id="course-suggestions" role="listbox">
              {suggestionsLoading ? (
                <div className="suggestion-state"><Loader2 size={18} className="spin" /> Đang tìm khóa học...</div>
              ) : suggestions.length === 0 ? (
                <div className="suggestion-state">Không tìm thấy khóa học</div>
              ) : suggestions.map((item, index) => (
                <button
                  type="button" role="option" aria-selected={activeSuggestion === index}
                  className={`suggestion-item ${activeSuggestion === index ? 'active' : ''}`}
                  key={item.courseId}
                  onMouseEnter={() => setActiveSuggestion(index)}
                  onClick={() => { setSuggestionsOpen(false); navigate(`/courses/${item.courseId}`); }}
                >
                  <CourseThumbnail className="suggestion-thumb" src={item.thumbnailUrl} categoryName={item.categoryName} alt="" />
                  <span className="suggestion-copy"><strong>{item.title}</strong><small>{item.instructorName}</small></span>
                </button>
              ))}
            </div>
          )}
        </form>

        <div className="navbar-right">
          {isStudent && (
            <>
              <Link to="/cart" className="navbar-icon-btn text-dark position-relative" title="Giỏ hàng">
                <ShoppingCart size={20} />
                {cartCount > 0 && (
                  <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger" style={{ fontSize: '10px', padding: '4px 6px' }}>
                    {cartCount}
                  </span>
                )}
              </Link>
              {user && (
                <>
                  <Link to="/wishlist" className="navbar-icon-btn text-dark position-relative" title="Yêu thích">
                    <Heart size={20} />
                    {wishlistCount > 0 && (
                      <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger" style={{ fontSize: '10px', padding: '4px 6px' }}>
                        {wishlistCount}
                      </span>
                    )}
                  </Link>
                  <Link to="/my-courses" className="navbar-link hidden-mobile fw-medium text-decoration-none">
                    Khóa học của tôi
                  </Link>
                  <Link to="/teach-on-edumy" className="navbar-link hidden-mobile fw-medium text-decoration-none text-primary">
                    Teach on Edumy
                  </Link>
                </>
              )}
            </>
          )}

          {user && isInstructor && (
            <>
              <Link to="/instructor" className="navbar-link hidden-mobile fw-medium text-decoration-none">
                Dashboard Giảng viên
              </Link>
              <Link to="/instructor/courses/new" className="navbar-link hidden-mobile fw-medium text-decoration-none">
                Tạo khóa học
              </Link>
            </>
          )}

          {user && isAdmin && (
            <>
              <Link to="/admin" className="navbar-link hidden-mobile fw-medium text-decoration-none">
                Trang Quản trị Admin
              </Link>
            </>
          )}

          {user && (
            <div ref={notifRef} className="navbar-icon-btn position-relative" style={{ cursor: 'pointer' }}>
              <button
                type="button"
                className="btn btn-link p-0 text-dark border-0 shadow-none position-relative"
                onClick={() => setNotificationDropdownOpen(prev => !prev)}
                aria-label="Thông báo"
                aria-expanded={notificationDropdownOpen}
              >
                <Bell size={20} />
                {unreadNotificationsCount > 0 && (
                  <span className="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger" style={{ fontSize: '10px', padding: '3px 6px' }}>
                    {unreadNotificationsCount > 99 ? '99+' : unreadNotificationsCount}
                  </span>
                )}
              </button>

              {notificationDropdownOpen && (
                <div
                  className="categories-dropdown"
                  style={{
                    right: 0,
                    left: 'auto',
                    width: '360px',
                    maxWidth: '90vw',
                    padding: '0',
                    display: 'block',
                    top: 'calc(100% + 8px)',
                    position: 'absolute',
                    backgroundColor: '#fff',
                    border: '1px solid #e2e8f0',
                    borderRadius: '12px',
                    boxShadow: '0 10px 25px -5px rgba(0,0,0,0.15)',
                    zIndex: 1050,
                    overflow: 'hidden'
                  }}
                  onClick={(e) => e.stopPropagation()}
                >
                  <div className="d-flex justify-content-between align-items-center p-3 border-bottom bg-light">
                    <h6 className="fw-bold mb-0 text-dark" style={{ fontSize: '14px' }}>Thông báo</h6>
                    {unreadNotificationsCount > 0 && (
                      <button
                        type="button"
                        className="btn btn-link p-0 text-primary text-decoration-none small d-flex align-items-center gap-1"
                        style={{ fontSize: '12px' }}
                        onClick={handleMarkAllAsRead}
                      >
                        <CheckCheck size={14} /> Đọc tất cả
                      </button>
                    )}
                  </div>

                  {notifications.length === 0 ? (
                    <div className="text-muted text-center py-4 px-3" style={{ fontSize: '13px' }}>
                      Chưa có thông báo nào
                    </div>
                  ) : (
                    <div style={{ maxHeight: '340px', overflowY: 'auto' }}>
                      {notifications.map(notif => (
                        <div
                          key={notif.notificationId}
                          className={`p-3 border-bottom text-wrap ${notif.isRead ? 'bg-white' : 'bg-light'}`}
                          style={{ cursor: 'pointer', transition: 'background-color 0.15s ease' }}
                          onClick={() => handleNotificationClick(notif)}
                        >
                          <div className="d-flex gap-2 align-items-start">
                            {notif.actor?.avatarUrl ? (
                              <img src={notif.actor.avatarUrl} alt="" className="rounded-circle mt-1" width="32" height="32" style={{ objectFit: 'cover' }} />
                            ) : (
                              <div className="rounded-circle bg-primary text-white d-flex align-items-center justify-content-center mt-1 flex-shrink-0" style={{ width: '32px', height: '32px', fontSize: '12px', fontWeight: 600 }}>
                                {(notif.actor?.fullName || 'E').charAt(0).toUpperCase()}
                              </div>
                            )}
                            <div className="flex-grow-1" style={{ fontSize: '13px' }}>
                              <div className="d-flex justify-content-between align-items-center mb-1">
                                <span className={`fw-bold ${notif.isRead ? 'text-secondary' : 'text-primary'}`} style={{ fontSize: '13px' }}>{notif.title}</span>
                                {!notif.isRead && <span className="badge bg-danger rounded-circle p-1" style={{ width: '8px', height: '8px' }}></span>}
                              </div>
                              <p className="mb-1 text-dark" style={{ lineHeight: '1.4', fontSize: '12px' }}>{notif.message}</p>
                              <small className="text-muted">{new Date(notif.createdAt).toLocaleString('vi-VN')}</small>
                            </div>
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
          
          <div className="navbar-actions">
            {user ? (
              <div 
                className="navbar-avatar-container"
                onMouseEnter={() => setAvatarDropdownOpen(true)}
                onMouseLeave={() => setAvatarDropdownOpen(false)}
              >
                <div className="navbar-avatar-trigger d-flex align-items-center gap-2" style={{ cursor: 'pointer' }}>
                  {avatarUrl && !avatarImageError ? (
                    <img
                      src={avatarUrl}
                      alt={displayName}
                      className="rounded-circle"
                      width="34"
                      height="34"
                      style={{ objectFit: 'cover' }}
                      onError={() => setAvatarImageError(true)}
                    />
                  ) : (
                    <div className="avatar-circle">
                      {initials}
                    </div>
                  )}
                  <span className="navbar-username hidden-mobile fw-semibold" style={{ fontSize: '14px' }}>
                    {displayName}
                  </span>
                </div>
                {avatarDropdownOpen && (
                  <div className="avatar-dropdown">
                    <Link to="/profile" className="dropdown-item" onClick={() => setAvatarDropdownOpen(false)}>
                      👤 Hồ sơ cá nhân
                    </Link>
                    {isStudent && (
                      <>
                        <Link to="/my-courses" className="dropdown-item" onClick={() => setAvatarDropdownOpen(false)}>
                          📚 Khóa học của tôi
                        </Link>
                        <Link to="/wishlist" className="dropdown-item" onClick={() => setAvatarDropdownOpen(false)}>
                          💖 Danh sách yêu thích
                        </Link>
                        <Link to="/cart" className="dropdown-item" onClick={() => setAvatarDropdownOpen(false)}>
                          🛒 Giỏ hàng
                        </Link>
                      </>
                    )}
                    {isInstructor && (
                      <>
                        <Link to="/instructor" className="dropdown-item" onClick={() => setAvatarDropdownOpen(false)}>
                          📊 Dashboard Giảng viên
                        </Link>
                        <Link to="/instructor/courses/new" className="dropdown-item" onClick={() => setAvatarDropdownOpen(false)}>
                          📝 Tạo khóa học
                        </Link>
                      </>
                    )}
                    {isAdmin && (
                      <>
                        <Link to="/admin" className="dropdown-item" onClick={() => setAvatarDropdownOpen(false)}>
                          🛡️ Trang Quản trị Admin
                        </Link>
                      </>
                    )}
                    <hr className="my-1 border-light-subtle" style={{ margin: '4px 0' }} />
                    <button 
                      onClick={async () => {
                        await logout();
                        setAvatarDropdownOpen(false);
                        navigate('/login', { replace: true, state: { flash: { type: 'success', message: 'Đăng xuất thành công.' } } });
                      }} 
                      className="dropdown-item logout-item text-danger border-0 bg-transparent w-100 text-start"
                      style={{ border: 'none', background: 'transparent' }}
                    >
                      🚪 Đăng xuất
                    </button>
                  </div>
                )}
              </div>
            ) : (
              <>
                <Link to="/login" className="btn-edumy-outline login-btn">Log in</Link>
                <Link to="/register" className="btn-edumy signup-btn">Sign up</Link>
              </>
            )}
          </div>
        </div>
      </div>
    </nav>
  );
};

export default Navbar;
