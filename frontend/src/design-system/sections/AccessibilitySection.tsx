import { useState } from 'react';

import Button from 'devextreme-react/button';
import Tabs from 'devextreme-react/tabs';

import Section from '../components/Section';

const tabs = [
  { id: 0, text: 'Overview' },
  { id: 1, text: 'Payment details' },
  { id: 2, text: 'Audit history' },
];

export default function AccessibilitySection() {
  const [selectedTab, setSelectedTab] = useState(0);

  return (
    <Section
      id="accessibility"
      title="Accessibility reference"
      description="Focus, target size, keyboard navigation, and contrast must be visible here."
    >
      <div className="ds-accessibility-grid">
        <div className="app-card">
          <div className="app-card__body">
            <h3>Keyboard focus</h3>
            <p className="app-muted">
              Tab through these controls. Focus must remain obvious on both light and
              dark surfaces.
            </p>

            <div className="ds-focus-surface ds-focus-surface--light">
              <Button text="Light surface" />
              <a href="#accessibility">Text link</a>
            </div>

            <div className="ds-focus-surface ds-focus-surface--dark">
              <Button text="Dark surface" stylingMode="outlined" />
              <a href="#accessibility">Text link</a>
            </div>
          </div>
        </div>

        <div className="app-card">
          <div className="app-card__body">
            <h3>Tabs</h3>
            <Tabs
              items={tabs}
              selectedIndex={selectedTab}
              onSelectionChanged={(event) => {
                const item = event.addedItems[0] as { id?: number } | undefined;
                if (typeof item?.id === 'number') {
                  setSelectedTab(item.id);
                }
              }}
            />
            <div className="ds-tab-content">
              Selected: <strong>{tabs[selectedTab].text}</strong>
            </div>
          </div>
        </div>
      </div>
    </Section>
  );
}
