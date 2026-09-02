import Chart, { Legend, Series } from 'devextreme-react/chart';

import { smbcVizPalette } from '../../theme/smbc-viz-palette';
import Section from '../components/Section';
import paymentVolume from '../data/payment-volume.json';

export default function ChartsSection() {
  return (
    <Section
      id="charts"
      title="Charts"
      description="SVG-based components use the separately registered SMBC palette."
    >
      <div className="app-card">
        <div className="app-card__header">
          <div className="app-card__title">Daily payment volume</div>
        </div>
        <div className="app-card__body">
          <Chart
            dataSource={paymentVolume}
            palette={smbcVizPalette.simpleSet}
            height={320}
          >
            <Series
              valueField="approved"
              argumentField="day"
              name="Approved"
              type="bar"
            />
            <Series
              valueField="pending"
              argumentField="day"
              name="Pending"
              type="line"
              dashStyle="dash"
            />
            <Legend verticalAlignment="top" horizontalAlignment="right" />
          </Chart>
        </div>
      </div>
    </Section>
  );
}
