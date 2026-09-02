import Accordion from 'devextreme-react/accordion';
import Button from 'devextreme-react/button';
import NumberBox from 'devextreme-react/number-box';
import TagBox from 'devextreme-react/tag-box';
import TextBox from 'devextreme-react/text-box';
import ValidationGroup from 'devextreme-react/validation-group';
import ValidationSummary from 'devextreme-react/validation-summary';
import Validator, {
  EmailRule,
  RangeRule,
  RequiredRule,
} from 'devextreme-react/validator';

import Section from '../components/Section';
import formOptions from '../data/form-options.json';

const accordionItems = [
  {
    title: 'Payment details',
    text: 'Core transaction data, settlement instructions, and beneficiary information.',
  },
  {
    title: 'Compliance checks',
    text: 'Screening results, policy exceptions, and any checks requiring manual review.',
  },
  {
    title: 'Audit history',
    text: 'A chronological record of changes, decisions, and responsible users.',
  },
];

export default function ComponentsSection() {
  return (
    <Section
      id="components"
      title="More DevExtreme components"
      description="A small set of common application patterns beyond the basic form controls."
    >
      <div className="ds-component-grid">
        <div className="app-card">
          <div className="app-card__header">
            <div>
              <div className="app-card__title">NumberBox</div>
              <div className="app-caption">Formatted numeric input</div>
            </div>
          </div>
          <div className="app-card__body">
            <div className="app-field">
              <label className="app-label" htmlFor="ds-payment-amount">
                Payment amount
              </label>
              <NumberBox
                inputAttr={{ id: 'ds-payment-amount' }}
                defaultValue={184250.45}
                format="#,##0.00"
                min={0}
                showSpinButtons
              />
              <span className="app-caption">EUR · minimum 0.00</span>
            </div>
          </div>
        </div>

        <div className="app-card">
          <div className="app-card__header">
            <div>
              <div className="app-card__title">TagBox</div>
              <div className="app-caption">Searchable multiple selection</div>
            </div>
          </div>
          <div className="app-card__body">
            <div className="app-field">
              <label className="app-label" htmlFor="ds-review-teams">
                Review teams
              </label>
              <TagBox
                inputAttr={{ id: 'ds-review-teams' }}
                items={formOptions.reviewTeams}
                defaultValue={['Payments Operations', 'Financial Crime']}
                placeholder="Select teams"
                searchEnabled
                showSelectionControls
                applyValueMode="useButtons"
              />
              <span className="app-caption">
                Search, select several values, then apply.
              </span>
            </div>
          </div>
        </div>

        <div className="app-card ds-component-grid__wide">
          <div className="app-card__header">
            <div>
              <div className="app-card__title">Accordion</div>
              <div className="app-caption">Progressive disclosure for related content</div>
            </div>
          </div>
          <div className="app-card__body">
            <Accordion
              items={accordionItems}
              defaultSelectedIndex={0}
              collapsible
              multiple={false}
            />
          </div>
        </div>

        <div className="app-card ds-component-grid__wide">
          <div className="app-card__header">
            <div>
              <div className="app-card__title">Validation</div>
              <div className="app-caption">
                Field-level rules with a form-level summary
              </div>
            </div>
          </div>
          <div className="app-card__body">
            <form
              className="ds-validation-form"
              onSubmit={(event) => event.preventDefault()}
            >
              <ValidationGroup>
                <div className="ds-validation-fields">
                  <div className="app-field">
                    <label className="app-label" htmlFor="ds-approver-email">
                      Approver email *
                    </label>
                    <TextBox
                      inputAttr={{ id: 'ds-approver-email' }}
                      placeholder="name@smbcgroup.com"
                    >
                      <Validator>
                        <RequiredRule message="Enter the approver email." />
                        <EmailRule message="Enter a valid email address." />
                      </Validator>
                    </TextBox>
                  </div>

                  <div className="app-field">
                    <label className="app-label" htmlFor="ds-approval-limit">
                      Approval limit *
                    </label>
                    <NumberBox
                      inputAttr={{ id: 'ds-approval-limit' }}
                      defaultValue={0}
                      format="#,##0.00"
                      showSpinButtons
                    >
                      <Validator>
                        <RequiredRule message="Enter the approval limit." />
                        <RangeRule
                          min={100}
                          max={1000000}
                          message="Use a value from 100.00 to 1,000,000.00."
                        />
                      </Validator>
                    </NumberBox>
                  </div>
                </div>

                <div className="ds-validation-actions">
                  <ValidationSummary />
                  <Button text="Validate fields" type="default" useSubmitBehavior />
                </div>
              </ValidationGroup>
            </form>
          </div>
        </div>
      </div>
    </Section>
  );
}
