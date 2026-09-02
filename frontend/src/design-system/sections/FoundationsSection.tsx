import Section from '../components/Section';

type Swatch = readonly [name: string, token: string, value: string];

const primarySwatches = [
  ['Traditional Green', '--color-brand-traditional', '#004831'],
  ['Fresh Green', '--color-brand-fresh-500', '#C4D600'],
] as const satisfies readonly Swatch[];

const traditionalGreenTints = [
  ['Traditional Green 100%', '--color-brand-traditional', '#004831'],
  ['Traditional Green 90%', '--color-brand-traditional-tint-90', '#005742'],
  ['Traditional Green 80%', '--color-brand-traditional-tint-80', '#006451'],
  ['Traditional Green 70%', '--color-brand-traditional-tint-70', '#137260'],
  ['Traditional Green 60%', '--color-brand-traditional-tint-60', '#398171'],
  ['Traditional Green 50%', '--color-brand-traditional-tint-50', '#589284'],
  ['Traditional Green 40%', '--color-brand-traditional-tint-40', '#75A499'],
  ['Traditional Green 30%', '--color-brand-traditional-tint-30', '#91B7AE'],
  ['Traditional Green 20%', '--color-brand-traditional-tint-20', '#B1CCC4'],
  ['Traditional Green 10%', '--color-brand-traditional-tint-10', '#D5E4E1'],
] as const satisfies readonly Swatch[];

const supplementarySwatches = [
  ['Pure crimson', '--color-brand-pure-crimson', '#C3272B'],
  ['Daylily', '--color-brand-daylily', '#FF8936'],
  ['Triandra grass', '--color-brand-triandra-grass', '#E2B13C'],
  ['Thousand herbs', '--color-brand-thousand-herbs', '#317589'],
  ['Navy', '--color-brand-navy', '#003171'],
  ['Vine grape', '--color-brand-vine-grape', '#6D2B50'],
  ['Cherry blossom', '--color-brand-cherry-blossom', '#FCC9BA'],
  ['Smoked bamboo', '--color-brand-smoked-bamboo', '#593A27'],
  ['Indigo ink', '--color-brand-indigo-ink', '#393432'],
] as const satisfies readonly Swatch[];

const interfaceSwatches = [
  ['Page background', '--color-page-background', '#F6F8F6'],
  ['Default border', '--color-border-default', '#D8DDD9'],
  ['Control border', '--color-border-control', '#7F8C85'],
  ['Primary text', '--color-text-primary', '#1F2522'],
  ['Secondary text', '--color-text-secondary', '#68716C'],
] as const satisfies readonly Swatch[];

const spacingValues = [2, 4, 8, 12, 16, 20, 24, 32, 40, 48];

function SwatchGrid({ swatches }: { swatches: readonly Swatch[] }) {
  return (
    <div className="ds-swatch-grid">
      {swatches.map(([name, token, value]) => (
        <div className="ds-swatch" key={name}>
          <div
            className="ds-swatch__color"
            style={{ background: `var(${token})` }}
          />
          <div className="ds-swatch__meta">
            <strong>{name}</strong>
            <code>{value}</code>
            <code>{token}</code>
          </div>
        </div>
      ))}
    </div>
  );
}

export default function FoundationsSection() {
  return (
    <Section
      id="foundations"
      title="Foundations"
      description="Corporate colour, spacing, shape, and interface surface rules."
    >
      <div className="ds-subsection">
        <h3>Primary corporate colours</h3>
        <p className="app-muted">
          Traditional Green is dominant. Fresh Green is a restrained accent;
          the project keeps #C4D600 pending confirmation against the source PDF.
        </p>
        <SwatchGrid swatches={primarySwatches} />
      </div>

      <div className="ds-subsection">
        <h3>Traditional Green tints</h3>
        <SwatchGrid swatches={traditionalGreenTints} />
      </div>

      <div className="ds-subsection">
        <h3>Supplementary corporate colours</h3>
        <p className="app-muted">
          Use these only when a category or semantic role needs distinction;
          they are not decorative page colours.
        </p>
        <SwatchGrid swatches={supplementarySwatches} />
      </div>

      <div className="ds-subsection">
        <h3>Interface neutrals</h3>
        <SwatchGrid swatches={interfaceSwatches} />
      </div>

      <div className="ds-subsection">
        <h3>Spacing scale</h3>
        <div className="ds-spacing-row">
          {spacingValues.map((value) => (
            <div className="ds-spacing-item" key={value}>
              <div style={{ width: value, height: value }} />
              <span>{value}px</span>
            </div>
          ))}
        </div>
      </div>

      <div className="ds-subsection">
        <h3>Shape</h3>
        <div className="ds-control-row">
          <div className="ds-shape ds-shape--control">3px control</div>
          <div className="ds-shape ds-shape--card">6px card</div>
          <span className="app-badge app-badge--brand">Pill status</span>
        </div>
      </div>
    </Section>
  );
}
