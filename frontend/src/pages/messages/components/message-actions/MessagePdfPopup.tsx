import { useEffect, useRef, useState } from 'react';
import Button from 'devextreme-react/button';
import Popup from 'devextreme-react/popup';
import type { PDFDocumentProxy, PDFDocumentLoadingTask, RenderTask } from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';
import { getMessagePdf, type MessageRow } from '../../api/messagesApi';

function PdfPage({ document, number }: { document: PDFDocumentProxy; number: number }) {
  const canvas = useRef<HTMLCanvasElement>(null);
  const [error, setError] = useState<string>();
  useEffect(() => {
    let active = true;
    let rendering: RenderTask | undefined;
    void document.getPage(number).then(async (page) => {
      if (!active || !canvas.current) return;
      const viewport = page.getViewport({ scale: 1.5 });
      canvas.current.width = viewport.width;
      canvas.current.height = viewport.height;
      rendering = page.render({ canvas: canvas.current, viewport });
      await rendering.promise;
    }).catch(() => { if (active) setError(`Unable to render page ${number}. Download the PDF to view it.`); });
    return () => { active = false; rendering?.cancel(); };
  }, [document, number]);
  return error ? <p role="alert">{error}</p>
    : <canvas ref={canvas} aria-label={`PDF page ${number}`} role="img" style={{ maxWidth: '100%', height: 'auto', display: 'block', margin: '0 auto 16px' }} />;
}

export default function MessagePdfPopup({ message, onClose }: { message: MessageRow; onClose: () => void }) {
  const [preview, setPreview] = useState<{ url: string; document: PDFDocumentProxy }>();
  const [error, setError] = useState<string>();
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    let objectUrl: string | undefined;
    let loading: PDFDocumentLoadingTask | undefined;
    void (async () => {
      const blob = await getMessagePdf(message.id, controller.signal);
      const [pdfjs, data] = await Promise.all([import('pdfjs-dist'), blob.arrayBuffer()]);
      if (controller.signal.aborted) return;
      pdfjs.GlobalWorkerOptions.workerSrc = workerUrl;
      loading = pdfjs.getDocument({ data, useSystemFonts: true });
      const document = await loading.promise;
      if (controller.signal.aborted) return;
      objectUrl = URL.createObjectURL(blob);
      setPreview({ url: objectUrl, document });
    })().catch((caught: unknown) => {
      if (!controller.signal.aborted) setError(caught instanceof Error ? caught.message : 'Unable to load PDF.');
    });
    return () => { controller.abort(); void loading?.destroy(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [message.id, attempt]);
  return <Popup visible title={`PDF — ${message.externalId}`} showCloseButton hideOnOutsideClick
    width="90vw" height="85vh" onHiding={onClose}>
    <div style={{ height: '100%', overflowY: 'auto' }}>
      {error ? <div role="alert">{error} <Button text="Retry" onClick={() => { setError(undefined); setAttempt(attempt + 1); }} /></div>
        : preview ? <>
          <p><a href={preview.url} download={`${message.externalId}.pdf`}>Download PDF</a></p>
          {Array.from({ length: preview.document.numPages }, (_, index) =>
            <PdfPage key={index} document={preview.document} number={index + 1} />)}
        </> : <p role="status">Loading PDF…</p>}
    </div>
  </Popup>;
}
