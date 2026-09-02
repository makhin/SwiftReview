import { useEffect, useRef, useState } from 'react';
import { NavLink } from 'react-router-dom';

import './global-header.css';

export default function GlobalHeader() {
  const [menuOpen, setMenuOpen] = useState(false);
  const [hidden, setHidden] = useState(false);
  const headerRef = useRef<HTMLElement>(null);

  useEffect(() => {
    let lastScrollY = window.scrollY;
    let animationFrame: number | undefined;

    const updateHeader = () => {
      animationFrame = undefined;

      const currentScrollY = Math.max(window.scrollY, 0);
      const scrollDelta = currentScrollY - lastScrollY;
      const headerHeight = headerRef.current?.offsetHeight ?? 0;

      if (menuOpen || currentScrollY <= headerHeight) {
        setHidden(false);
        lastScrollY = currentScrollY;
        return;
      }

      if (Math.abs(scrollDelta) < 8) {
        return;
      }

      setHidden(scrollDelta > 0);
      lastScrollY = currentScrollY;
    };

    const handleScroll = () => {
      if (animationFrame === undefined) {
        animationFrame = window.requestAnimationFrame(updateHeader);
      }
    };

    window.addEventListener('scroll', handleScroll, { passive: true });

    return () => {
      window.removeEventListener('scroll', handleScroll);
      if (animationFrame !== undefined) {
        window.cancelAnimationFrame(animationFrame);
      }
    };
  }, [menuOpen]);

  return (
    <header
      className="global-header"
      data-hidden={hidden}
      ref={headerRef}
      onFocus={() => setHidden(false)}
    >
      <div className="global-header__inner">
        <NavLink className="global-header__brand" to="/" aria-label="SMBC home">
          <img src="/smbc-logo.svg" alt="SMBC" width="146" height="42" />
          <span>Application UI</span>
        </NavLink>

        <button
          className="global-header__menu-button"
          type="button"
          aria-controls="global-navigation"
          aria-expanded={menuOpen}
          aria-label={menuOpen ? 'Close navigation' : 'Open navigation'}
          data-open={menuOpen}
          onClick={() => {
            setHidden(false);
            setMenuOpen((open) => !open);
          }}
        >
          <span aria-hidden="true" />
          <span aria-hidden="true" />
          <span aria-hidden="true" />
        </button>

        <nav
          className="global-header__nav"
          id="global-navigation"
          aria-label="Global navigation"
          data-open={menuOpen}
        >
          <NavLink to="/design-system" onClick={() => setMenuOpen(false)}>
            Design system
          </NavLink>
          <span className="global-header__region">EMEA</span>
        </nav>
      </div>
    </header>
  );
}
