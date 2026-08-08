import React from 'react';
import { Link } from 'react-router-dom';
import './Footer.css';

const Footer = () => {
  const handleScroll = () => {
    window.scrollTo(0, 0);
  };

  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-top">
          <div className="footer-links-grid">
            <div className="footer-col">
              <Link to="/about" onClick={handleScroll}>Edumy Business</Link>
              <Link to="/instructor" onClick={handleScroll}>Teach on Edumy</Link>
              <Link to="/help" onClick={handleScroll}>Get the app</Link>
              <Link to="/about" onClick={handleScroll}>About us</Link>
              <Link to="/help" onClick={handleScroll}>Contact us</Link>
            </div>
            <div className="footer-col">
              <Link to="/about" onClick={handleScroll}>Careers</Link>
              <Link to="/blog" onClick={handleScroll}>Blog</Link>
              <Link to="/help" onClick={handleScroll}>Help and Support</Link>
              <Link to="/about" onClick={handleScroll}>Affiliate</Link>
              <Link to="/about" onClick={handleScroll}>Investors</Link>
            </div>
            <div className="footer-col">
              <Link to="/terms" onClick={handleScroll}>Terms</Link>
              <Link to="/terms" onClick={handleScroll}>Privacy policy</Link>
              <Link to="/terms" onClick={handleScroll}>Cookie settings</Link>
              <Link to="/help" onClick={handleScroll}>Sitemap</Link>
              <Link to="/terms" onClick={handleScroll}>Accessibility statement</Link>
            </div>
          </div>
        </div>
        
        <div className="footer-bottom">
          <div className="footer-logo">
            <span className="logo-text">Edumy</span>
          </div>
          <div className="footer-copyright">
            © 2026 Edumy, Inc.
          </div>
        </div>
      </div>
    </footer>
  );
};

export default Footer;
