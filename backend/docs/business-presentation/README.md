# ORP business presentation

English presentation for leadership and future users, based on the current SwiftReview implementation. Twelve slides; allow around 12–15 minutes plus discussion.

- `SwiftReview_Business_EN.pptx` — editable slides, diagrams and embedded speaker notes.
- `SwiftReview_Business_EN.pdf` — portable viewing copy exported from the PowerPoint file.
- `Speaker_Notes_EN.md` — standalone talk track.
- `Speaker_Notes_RU.md` — Russian talk track matching the same 12 English slides.
- `preview/overview.png` — overview of all slides.
- `Evidence_EN.md` — source mapping, assumptions and scope boundaries.

Open the PPTX in PowerPoint and use Presenter View for the notes. Diagrams and text are native PowerPoint objects. Interface screenshots use synthetic data and do not contain real customer information. The green palette follows the local application theme.

## Rebuild

`build_deck.cjs` requires Node.js and `pptxgenjs` (created with version 4.0.1). With that dependency available, run:

```bash
node build_deck.cjs
```

The generator uses the saved PNG assets and also regenerates the speaker and evidence notes. Export the rebuilt PPTX to PDF with PowerPoint or LibreOffice. Recheck slide layout after editing text or replacing fonts.

`capture_demo.py` is optional and requires Python with Playwright and a Chromium browser. It captures the real frontend against a **disposable in-memory development API** on port 5080 and the frontend on port 5173. It creates sample workflow actions, so never point it at persistent or production data. Set `PRESENTATION_DISPOSABLE_MOCK_API=1` only after starting the API explicitly with `UseMockData=true`; set `PRESENTATION_CHROMIUM` to the local browser path if necessary.

## Validation

The delivered PPTX is checked with the Anthropic PPTX skill's Office validator and rendered through LibreOffice for visual inspection. The PDF is inspected for page count and text outside page bounds. Application code is unchanged; backend and frontend regression suites are outside the scope of this presentation-only change.
