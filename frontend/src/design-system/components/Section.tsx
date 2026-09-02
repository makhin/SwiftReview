import type { ReactNode } from 'react';

import './section.css';

type SectionProps = {
  id: string;
  title: string;
  description?: string;
  children: ReactNode;
};

export default function Section({ id, title, description, children }: SectionProps) {
  return (
    <section className="ds-section" id={id}>
      <div className="ds-section__header">
        <div>
          <h2>{title}</h2>
          {description && <p className="app-muted">{description}</p>}
        </div>
        <a className="ds-anchor" href={`#${id}`} aria-label={`Link to ${title}`}>
          #
        </a>
      </div>
      <div className="ds-section__content">{children}</div>
    </section>
  );
}
