import Button from 'devextreme-react/button';

import Section from '../components/Section';

export default function CardsSection() {
  return (
    <Section id="cards" title="Cards & page patterns">
      <div className="app-grid app-grid--3">
        <article className="app-kpi">
          <div className="app-kpi__label">Payments today</div>
          <div className="app-kpi__value">184</div>
          <div className="app-kpi__meta">12 awaiting review</div>
        </article>
        <article className="app-kpi">
          <div className="app-kpi__label">Total value</div>
          <div className="app-kpi__value">€8.4m</div>
          <div className="app-kpi__meta">Across 7 currencies</div>
        </article>
        <article className="app-kpi">
          <div className="app-kpi__label">Exceptions</div>
          <div className="app-kpi__value">6</div>
          <div className="app-kpi__meta">2 require immediate attention</div>
        </article>
      </div>

      <div className="app-card">
        <div className="app-card__header">
          <div>
            <div className="app-card__title">Payment summary</div>
            <div className="app-caption">PAY-2026-008421</div>
          </div>
          <span className="app-badge app-badge--warning">Under review</span>
        </div>
        <div className="app-card__body">
          <dl className="app-details">
            <dt>Beneficiary</dt>
            <dd>Aster Components GmbH</dd>
            <dt>Amount</dt>
            <dd>184,250.45 EUR</dd>
            <dt>Value date</dt>
            <dd>27 Aug 2026</dd>
            <dt>Created by</dt>
            <dd>Operations Team</dd>
          </dl>
        </div>
        <div className="app-card__footer">
          <Button text="Back" stylingMode="text" />
          <Button text="Approve" type="default" />
        </div>
      </div>
    </Section>
  );
}
