import Section from '../components/Section';

export default function TypographySection() {
  return (
    <Section
      id="typography"
      title="Typography"
      description="Left-aligned sentence case with a consistent Myriad Pro application hierarchy."
    >
      <div className="app-card">
        <div className="app-card__body ds-type-stack">
          <div>
            <span className="ds-type-meta">Brand display · Capitolium 30 / 700</span>
            <div className="app-display-title">A trusted partner</div>
            <p className="app-caption">
              Capitolium is reserved for occasional brand display headings.
            </p>
          </div>
          <div>
            <span className="ds-type-meta">Application page title · Myriad Pro 24 / 600</span>
            <div className="app-page-title">Payment review</div>
          </div>
          <div>
            <span className="ds-type-meta">Application section · Myriad Pro 20 / 600</span>
            <h2>Payment information</h2>
          </div>
          <div>
            <span className="ds-type-meta">Component title · Myriad Pro 16 / 600</span>
            <h3>Approval history</h3>
          </div>
          <div>
            <span className="ds-type-meta">Standard UI · 14 / 400</span>
            <p>Standard application body text for operational information.</p>
          </div>
          <div>
            <span className="ds-type-meta">Secondary</span>
            <p className="app-muted">
              Secondary information must remain clearly readable.
            </p>
          </div>
          <div>
            <span className="ds-type-meta">Caption · 12 / 400</span>
            <p className="app-caption">Last updated 27 Aug 2026, 14:32 CET</p>
          </div>
          <div className="app-callout">
            <strong>Use sentence case</strong>
            <p>
              Capitalise only the first word and proper names. Keep body copy
              left aligned and use one family in varying sizes and weights.
            </p>
          </div>
        </div>
      </div>
    </Section>
  );
}
