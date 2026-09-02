import Button from 'devextreme-react/button';
import DateBox from 'devextreme-react/date-box';
import SelectBox from 'devextreme-react/select-box';
import TextBox from 'devextreme-react/text-box';

import Section from '../components/Section';

const statuses = ['Pending', 'Under review', 'Approved', 'Rejected', 'Failed'];

export default function FiltersSection() {
  return (
    <Section id="filters" title="Filters">
      <div className="app-filter-panel">
        <div className="app-field">
          <label className="app-label" htmlFor="ds-filter-reference">
            Reference
          </label>
          <TextBox
            inputAttr={{ id: 'ds-filter-reference' }}
            placeholder="Payment reference"
          />
        </div>
        <div className="app-field">
          <label className="app-label" htmlFor="ds-filter-status">
            Status
          </label>
          <SelectBox
            inputAttr={{ id: 'ds-filter-status' }}
            items={statuses}
            placeholder="All statuses"
            showClearButton
          />
        </div>
        <div className="app-field">
          <label className="app-label" htmlFor="ds-filter-from">
            From
          </label>
          <DateBox inputAttr={{ id: 'ds-filter-from' }} type="date" />
        </div>
        <div className="app-field">
          <label className="app-label" htmlFor="ds-filter-to">
            To
          </label>
          <DateBox inputAttr={{ id: 'ds-filter-to' }} type="date" />
        </div>
        <div className="ds-filter-actions">
          <Button text="Reset" stylingMode="text" />
          <Button text="Apply filters" type="default" />
        </div>
      </div>
    </Section>
  );
}
