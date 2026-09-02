import AccessibilitySection from './sections/AccessibilitySection';
import ButtonsSection from './sections/ButtonsSection';
import CardsSection from './sections/CardsSection';
import ChartsSection from './sections/ChartsSection';
import ComponentsSection from './sections/ComponentsSection';
import DataGridSection from './sections/DataGridSection';
import DialogsSection from './sections/DialogsSection';
import FiltersSection from './sections/FiltersSection';
import FormsSection from './sections/FormsSection';
import FoundationsSection from './sections/FoundationsSection';
import StatesSection from './sections/StatesSection';
import StatusSection from './sections/StatusSection';
import TypographySection from './sections/TypographySection';

import './design-system.css';

const sections = [
  {
    id: 'foundations',
    navigationLabel: 'Foundations',
    Component: FoundationsSection,
  },
  {
    id: 'typography',
    navigationLabel: 'Typography',
    Component: TypographySection,
  },
  { id: 'buttons', navigationLabel: 'Buttons', Component: ButtonsSection },
  { id: 'forms', navigationLabel: 'Forms', Component: FormsSection },
  {
    id: 'components',
    navigationLabel: 'More components',
    Component: ComponentsSection,
  },
  { id: 'status', navigationLabel: 'Status', Component: StatusSection },
  { id: 'cards', navigationLabel: 'Cards', Component: CardsSection },
  { id: 'filters', navigationLabel: 'Filters', Component: FiltersSection },
  { id: 'grid', navigationLabel: 'DataGrid', Component: DataGridSection },
  {
    id: 'dialogs',
    navigationLabel: 'Dialogs & feedback',
    Component: DialogsSection,
  },
  { id: 'states', navigationLabel: 'States', Component: StatesSection },
  { id: 'charts', navigationLabel: 'Charts', Component: ChartsSection },
  {
    id: 'accessibility',
    navigationLabel: 'Accessibility',
    Component: AccessibilitySection,
  },
] as const;

export default function DesignSystemPage() {
  return (
    <div className="ds-page">
      <aside className="ds-nav" aria-label="Design system sections">
        <div className="ds-nav__brand">
          <strong>Design system</strong>
          <span>Application UI reference</span>
        </div>

        <nav>
          {sections.map(({ id, navigationLabel }) => (
            <a key={id} href={`#${id}`}>
              {navigationLabel}
            </a>
          ))}
        </nav>
      </aside>

      <main className="ds-main">
        <header className="ds-hero">
          <div>
            <div className="ds-eyebrow">Design system reference</div>
            <h1 className="app-display-title">SMBC application UI</h1>
            <p>
              Visual regression surface for shared application tokens, patterns,
              accessibility states, and DevExtreme components.
            </p>
          </div>
          <span className="app-badge app-badge--brand">v1.2</span>
        </header>

        {sections.map(({ id, Component }) => (
          <Component key={id} />
        ))}

        <footer className="ds-footer">
          <span>SMBC application design system</span>
          <span>Reference surface · DevExtreme 25.1</span>
        </footer>
      </main>
    </div>
  );
}
