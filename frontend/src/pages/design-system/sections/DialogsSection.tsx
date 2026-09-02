import { useState } from 'react';

import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import Toast from 'devextreme-react/toast';

import Section from '../components/Section';

import './dialogs-section.css';

export default function DialogsSection() {
  const [popupVisible, setPopupVisible] = useState(false);
  const [toastVisible, setToastVisible] = useState(false);

  return (
    <Section id="dialogs" title="Dialogs & feedback">
      <div className="ds-control-row">
        <Button
          text="Open dialog"
          type="default"
          onClick={() => setPopupVisible(true)}
        />
        <Button
          text="Show success toast"
          stylingMode="outlined"
          onClick={() => setToastVisible(true)}
        />
      </div>

      <Popup
        elementAttr={{ class: 'ds-overlay-host' }}
        visible={popupVisible}
        width={600}
        height="auto"
        maxWidth="calc(100vw - 32px)"
        title="Approve payment?"
        showCloseButton
        dragEnabled={false}
        hideOnOutsideClick={false}
        onHiding={() => setPopupVisible(false)}
      >
        <div className="ds-dialog-body">
          <p>
            You are approving <strong>PAY-2026-008421</strong> for 184,250.45 EUR.
          </p>
          <p className="app-muted">
            The payment will proceed to the next workflow stage.
          </p>
          <div className="ds-dialog-actions">
            <Button
              text="Cancel"
              stylingMode="text"
              onClick={() => setPopupVisible(false)}
            />
            <Button
              text="Approve payment"
              type="default"
              onClick={() => {
                setPopupVisible(false);
                setToastVisible(true);
              }}
            />
          </div>
        </div>
      </Popup>

      <Toast
        elementAttr={{ class: 'ds-overlay-host' }}
        visible={toastVisible}
        message="Payment approved."
        type="success"
        displayTime={2500}
        onHiding={() => setToastVisible(false)}
      />
    </Section>
  );
}
