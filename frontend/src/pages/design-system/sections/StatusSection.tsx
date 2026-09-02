import Section from '../components/Section';

export default function StatusSection() {
  return (
    <Section
      id="status"
      title="Status system"
      description="Business state uses text plus semantic styling; colour is never the only cue."
    >
      <div className="ds-control-row">
        <span className="app-badge">Cancelled</span>
        <span className="app-badge app-badge--info">Pending</span>
        <span className="app-badge app-badge--warning">Under review</span>
        <span className="app-badge app-badge--success">Approved</span>
        <span className="app-badge app-badge--danger">Rejected</span>
        <span className="app-badge app-badge--danger">Failed</span>
        <span className="app-badge app-badge--brand">Selected</span>
      </div>

      <div className="ds-message-grid">
        <div className="app-callout">
          <strong>Information</strong>
          <span>The payment has supporting documents.</span>
        </div>
        <div className="app-callout app-callout--fresh">
          <strong>Brand highlight</strong>
          <span>Fresh Green is emphasis, not a universal success colour.</span>
        </div>
        <div className="app-callout app-callout--warning">
          <strong>Attention required</strong>
          <span>Beneficiary details changed since the previous payment.</span>
        </div>
        <div className="app-callout app-callout--danger">
          <strong>Processing failed</strong>
          <span>The payment could not be submitted.</span>
        </div>
      </div>
    </Section>
  );
}
