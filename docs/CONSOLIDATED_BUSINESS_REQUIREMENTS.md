# SWIFT replacement — business requirements

Consolidated from the DC, CS and TFO BRD summaries supplied on 21 September 2026. Unconfirmed requirements are identified below. “Confirmation” means an in-system action; it does not necessarily represent an independent review by another person.

## 1. Common requirements

- Authorised users can find and view complete, readable SWIFT messages within their business scope.
- Viewing or reopening a message does not change its processing or confirmation status.
- Preserve message content, references and sender/receiver information.
- For CS and TFO, show SWIFT tag codes, values and descriptions, plus bank names and addresses where reference data is available.

## 2. Business scope

| Team | Message scope |
|---|---|
| Data Control (DC) | Incoming MT094 and MT671 across all branches. Provide one copy per message, regardless of how many branches receive it. |
| Customer Service (CS) | Relevant incoming messages for London, Brussels, Dubai, Dusseldorf, Frankfurt, Paris, Milan and SMBC London. Candidate types: MT999, MT299, MT199, MT094 and MT210; final scope requires CS approval. Ownership may depend on content and references, not just message type. |
| Trade Finance Operations (TFO), Dubai | All incoming MT7xx; selected outgoing MT7xx; incoming pacs.008 and MT101. The outgoing list remains open. |
| Wider TFO — unconfirmed | MT4xx, MT7xx, MT999 and pacs.008, incoming and outgoing. Branch scope and confirmation ownership require approval. |

## 3. Data Control

- Automatically generate a complete, readable PDF when an in-scope message becomes available and save it to the designated shared repository.
- All authorised DC team members have equivalent access to retrieve messages and PDFs, including older messages needed for investigation or audit.
- Allow messages to be reopened, including when a PDF is corrupted, without changing workflow status.
- Processing, reconciliation, approvals, evidence of checks and management reporting remain outside the application. In-system confirmation is not a primary requirement.
- Operational checks are 4-eye for MT094 and 6-eye for MT671. They are generally performed by AVP-or-above staff; management may authorise a competent Associate.
- If an in-system audit workflow is introduced, each confirmation stage must be reversible, and only its original confirmer may reverse it. Notification requirements remain unconfirmed.

## 4. Customer Service

- Preserve current message visibility and operational capability. The message list must show branch, message type and receipt date/time. References, including fields 20 and 21 where present, must be available in the full message view.
- Support exactly two confirmation stages: the first must finish before the second; no third stage is required.
- Any appropriately authorised CS user may perform either stage. Separate first- and second-confirmer roles are not required. **The same user may complete both stages.**
- The first confirmation may be undone only before the second is completed. The second cannot be undone; after it is completed, neither confirmation can be undone.
- Users must be able to inspect content and references to decide whether CS should process or forward a message, particularly MT999.
- Allow email forwarding to approved distribution groups and manually entered recipients. Recipient restrictions require confirmation.

## 5. Trade Finance Operations

- Show complete messages with expanded tag values. Include references, particularly fields 20 and 21, in both the message view and list.
- Provide readable local printing and PDF export, including the complete message, references and sender/receiver details. Word export is not required if PDF meets these needs.
- Authorised Dubai users can email messages as PDF attachments to internal SMBC recipients, selected from saved recipients or entered manually.

### Confirmation scope

| Dubai messages | Required confirmations |
|---|---|
| Incoming MT700, MT701, MT707, MT710, MT760 and MT767 | First and second; the same authorised user may perform both. |
| Incoming pacs.008 and MT101 | None. |
| Incoming MT4xx and MT999 | No confirmation by Dubai; wider TFO handling remains to be confirmed. |
| Other Dubai SWIFT messages | Confirmation ownership remains to be agreed. |

The wider TFO proposal describes incoming messages as a “4-eye” workflow and outgoing messages as a “2-eye” workflow, with no third confirmation and same-user confirmation allowed. These wider rules remain provisional; the number of stages and required participants must be confirmed for each applicable message scope.

### Rejection and undo

- A fully approved message cannot be rejected.
- Rejection at the second confirmation returns the message to the first reviewer's queue and notifies that reviewer.
- In a two-stage workflow, the first confirmation may be undone only before the second is completed. After the second confirmation, neither can be undone.
- In a single-stage workflow, the final confirmation cannot be undone.

## 6. Decisions still required

- Approve the final message-type, direction and branch matrix, including CS Dublin scope, Dubai outgoing types and wider TFO ownership.
- Confirm whether DC needs any in-system confirmation workflow.
- Resolve the TFO “4-eye” terminology alongside explicit permission for same-user confirmation; confirm which rules apply beyond Dubai.
- Define CS ownership rules where message type alone is insufficient, and approve email-recipient restrictions for CS and TFO.
- Agree PDF storage/access, retention and historical-message availability.
- Confirm whether existing automated CS confirmations must be retained and whether future Pega/Smart Investigate integration is in scope.
- Decide whether the existing externally generated Dubai TFO cover letter must be attached to or printed with messages.
- Agree service availability and recovery requirements. The approximately 24-hour CS recovery target is indicative, not approved.

Source photographs: [DC](photo_2026-09-21_12-12-38.jpg), [CS](photo_2026-09-21_12-12-49.jpg), [CS continued](photo_2026-09-21_12-12-54.jpg), [TFO](photo_2026-09-21_12-12-58.jpg), [TFO continued](photo_2026-09-21_12-13-24.jpg).
