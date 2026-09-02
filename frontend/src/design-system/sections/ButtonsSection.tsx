import Button from 'devextreme-react/button';

import Section from '../components/Section';

export default function ButtonsSection() {
  return (
    <Section
      id="buttons"
      title="Buttons"
      description="Primary, secondary, destructive, disabled, and icon-only actions."
    >
      <div className="ds-control-row">
        <Button text="Approve payment" type="default" stylingMode="contained" />
        <Button text="Export" type="default" stylingMode="outlined" icon="exportxlsx" />
        <Button text="Cancel" stylingMode="text" />
        <Button text="Delete payment" type="danger" stylingMode="contained" icon="trash" />
        <Button
          icon="refresh"
          hint="Refresh"
          elementAttr={{ 'aria-label': 'Refresh' }}
        />
        <Button text="Disabled" disabled />
      </div>
    </Section>
  );
}
