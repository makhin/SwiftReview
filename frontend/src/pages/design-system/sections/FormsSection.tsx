import CheckBox from 'devextreme-react/check-box';
import DateBox from 'devextreme-react/date-box';
import RadioGroup from 'devextreme-react/radio-group';
import SelectBox from 'devextreme-react/select-box';
import TextArea from 'devextreme-react/text-area';
import TextBox from 'devextreme-react/text-box';
import Validator, { RequiredRule } from 'devextreme-react/validator';

import formOptions from '../data/form-options.json';
import Section from '../components/Section';

export default function FormsSection() {
  return (
    <Section
      id="forms"
      title="Forms"
      description="Default, validation, read-only, disabled, and selection states."
    >
      <div className="app-card">
        <div className="app-card__body">
          <div className="ds-form-grid">
            <div className="app-field">
              <label className="app-label" htmlFor="ds-reference">
                Payment reference
              </label>
              <TextBox
                inputAttr={{ id: 'ds-reference' }}
                defaultValue="PAY-2026-008421"
              />
            </div>

            <div className="app-field">
              <label className="app-label" htmlFor="ds-country">
                Country
              </label>
              <SelectBox
                inputAttr={{ id: 'ds-country' }}
                items={formOptions.countries}
                defaultValue="Poland"
                searchEnabled
              />
            </div>

            <div className="app-field">
              <label className="app-label" htmlFor="ds-settlement-date">
                Settlement date
              </label>
              <DateBox
                inputAttr={{ id: 'ds-settlement-date' }}
                type="date"
                defaultValue={new Date(2026, 7, 27)}
                displayFormat="dd MMM yyyy"
              />
            </div>

            <div className="app-field">
              <div className="app-label" id="ds-priority-label">
                Priority
              </div>
              <RadioGroup
                elementAttr={{ 'aria-labelledby': 'ds-priority-label' }}
                items={formOptions.priorities}
                defaultValue="Standard"
                layout="horizontal"
              />
            </div>

            <div className="app-field app-field--wide">
              <label className="app-label" htmlFor="ds-payment-note">
                Payment note *
              </label>
              <TextArea
                inputAttr={{ id: 'ds-payment-note' }}
                height={88}
                placeholder="Enter a short operational note"
              >
                <Validator>
                  <RequiredRule message="Enter a payment note." />
                </Validator>
              </TextArea>
            </div>

            <div className="app-field">
              <label className="app-label" htmlFor="ds-read-only-value">
                Read-only value
              </label>
              <TextBox
                inputAttr={{ id: 'ds-read-only-value' }}
                value="SMBC Bank International"
                readOnly
              />
            </div>

            <div className="app-field">
              <label className="app-label" htmlFor="ds-disabled-value">
                Disabled value
              </label>
              <TextBox
                inputAttr={{ id: 'ds-disabled-value' }}
                value="Unavailable"
                disabled
              />
            </div>

            <div className="app-field ds-checkbox-field">
              <CheckBox text="Require additional approval" />
            </div>
          </div>
        </div>
      </div>
    </Section>
  );
}
