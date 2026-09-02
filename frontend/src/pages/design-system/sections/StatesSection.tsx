import { useEffect, useRef, useState } from 'react';

import Button from 'devextreme-react/button';
import LoadIndicator from 'devextreme-react/load-indicator';

import Section from '../components/Section';

export default function StatesSection() {
  const [loadingDemo, setLoadingDemo] = useState(false);
  const loadingTimeout = useRef<number | undefined>(undefined);

  useEffect(() => {
    return () => window.clearTimeout(loadingTimeout.current);
  }, []);

  const runLoadingDemo = () => {
    window.clearTimeout(loadingTimeout.current);
    setLoadingDemo(true);
    loadingTimeout.current = window.setTimeout(() => {
      setLoadingDemo(false);
      loadingTimeout.current = undefined;
    }, 1200);
  };

  return (
    <Section id="states" title="Loading, empty & error states">
      <div className="ds-state-grid">
        <div className="app-card">
          <div className="app-card__header">
            <div className="app-card__title">Loading</div>
          </div>
          <div className="app-card__body ds-loading-demo" aria-busy={loadingDemo}>
            {loadingDemo ? (
              <div
                className="ds-loading-status"
                role="status"
                aria-live="polite"
                aria-atomic="true"
              >
                <LoadIndicator elementAttr={{ 'aria-hidden': 'true' }} />
                <span>Refreshing payments…</span>
              </div>
            ) : (
              <Button text="Run loading state" onClick={runLoadingDemo} />
            )}
          </div>
        </div>

        <div className="app-empty-state">
          <div className="ds-empty-icon" aria-hidden="true">
            <i className="dx-icon dx-icon-search" />
          </div>
          <h3>No payments match these filters</h3>
          <p>Try changing the date range or clearing one or more filters.</p>
          <Button text="Clear filters" stylingMode="outlined" type="default" />
        </div>

        <div className="app-card">
          <div className="app-card__header">
            <div className="app-card__title">Error</div>
          </div>
          <div className="app-card__body">
            <div className="app-callout app-callout--danger">
              <strong>Unable to load payment history</strong>
              <span>Try again. If the problem continues, contact support.</span>
            </div>
          </div>
        </div>
      </div>
    </Section>
  );
}
