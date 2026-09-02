import './styles/index.css';
import './theme/dx.smbc.css';
import './theme/smbc-devextreme-overrides.css';
import './theme/smbc-viz-palette';

import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './app/App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
