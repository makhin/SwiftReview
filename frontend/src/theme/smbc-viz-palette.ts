import { registerPalette } from 'devextreme/viz/palette';

/**
 * SMBC palette for DevExtreme SVG-based components.
 *
 * CSS ThemeBuilder styles HTML-based widgets. Charts, gauges, maps and other
 * SVG-based widgets use palettes/configuration, so register this once during
 * application startup and pass palette="SMBC" where a palette is supported.
 */
export const SMBC_VIZ_PALETTE_NAME = 'SMBC';

function readColorToken(name: string) {
  const value = getComputedStyle(document.documentElement)
    .getPropertyValue(name)
    .trim();

  if (!value) {
    throw new Error(`Missing required design token: ${name}`);
  }

  return value;
}

export const smbcVizPalette = {
  simpleSet: [
    readColorToken('--color-data-series-1'),
    readColorToken('--color-data-series-2'),
    readColorToken('--color-data-series-3'),
    readColorToken('--color-data-series-4'),
    readColorToken('--color-data-series-5'),
    readColorToken('--color-data-series-6'),
    readColorToken('--color-data-series-7'),
  ],
  indicatingSet: [
    readColorToken('--color-data-positive'),
    readColorToken('--color-data-target'),
    readColorToken('--color-data-caution'),
  ],
  gradientSet: [
    readColorToken('--color-data-gradient-start'),
    readColorToken('--color-data-gradient-end'),
  ],
};

registerPalette(SMBC_VIZ_PALETTE_NAME, smbcVizPalette);
