"""Capture the real application against an explicitly disposable in-memory API."""
import asyncio
import json
import os
from pathlib import Path
from urllib.request import Request, urlopen
from playwright.async_api import async_playwright

OUT = Path(__file__).parent / 'assets'
API = 'http://127.0.0.1:5080'

def api(path, user='admin', body=None):
    payload = None if body is None else json.dumps(body).encode()
    req = Request(API + '/api/' + path, data=payload, headers={
        'X-Debug-User': str(user), 'Content-Type': 'application/json'})
    with urlopen(req) as response:
        raw = response.read()
        return json.loads(raw) if raw else None

async def main():
    if os.environ.get('PRESENTATION_DISPOSABLE_MOCK_API') != '1':
        raise SystemExit('Run only against a new disposable UseMockData=true API; set PRESENTATION_DISPOSABLE_MOCK_API=1.')
    OUT.mkdir(exist_ok=True)
    # These are seeded demonstration messages, all initially New.
    for mid in [2,5,8,11,14,17,20,75,74,73,72,71,70,69,68]:
        message = api(f'messages/{mid}')
        if message['state'] == 'New':
            candidates = api(f'messages/{mid}/assignment-candidates')
            api(f'messages/{mid}/assign', body={'assignedTo':candidates[0]['id']})
    if api('messages/5')['state'] == 'Assigned':
        api('messages/5/reviews/start',2,{'level':1})
        api('messages/5/reviews/approve',2,{'level':1,'comment':'Demo: first review completed.'})
    if api('messages/8')['state'] == 'Assigned':
        api('messages/8/reviews/start',2,{'level':1})
    if api('messages/74')['state'] == 'Assigned':
        api('messages/74/reviews/start',2,{'level':1})
        api('messages/74/reviews/approve',2,{'level':1,'comment':'Demo: reviewed and ready for the next level.'})
    async with async_playwright() as p:
        browser = await p.chromium.launch(executable_path=os.environ.get('PRESENTATION_CHROMIUM',
            '/home/alexandr/.cache/ms-playwright/chromium-1234/chrome-linux64/chrome'),
            headless=True,args=['--no-sandbox'])
        page = await browser.new_page(viewport={'width':1600,'height':980},device_scale_factor=1.5)
        async def proxy(route):
            path = route.request.url.split('/api/',1)[1]
            response = await route.fetch(url=API+'/api/'+path)
            await route.fulfill(response=response)
        await page.route('http://127.0.0.1:5173/api/**',proxy)
        await page.goto('http://127.0.0.1:5173/messages?user=admin')
        await page.locator('.dx-data-row').first.wait_for(timeout=60000)
        await page.screenshot(path=str(OUT/'messages.png'))
        bounds = await page.locator('.dx-datagrid').first.bounding_box()
        await page.screenshot(path=str(OUT/'queue-detail.png'),clip={**bounds,'height':min(bounds['height'],480)})
        await page.locator('.dx-data-row').filter(has_text='MSG-00067-').get_by_role('button',name='Assign',exact=True).click()
        await page.locator('#assignment-reviewer').wait_for()
        await page.locator('#assignment-reviewer').click()
        await page.wait_for_timeout(300)
        await page.screenshot(path=str(OUT/'assignment.png'))
        await page.locator('.dx-dropdownlist-popup-wrapper .dx-list-item').first.click()
        await page.wait_for_timeout(400)
        await page.get_by_role('dialog',name='Assign message',exact=True).screenshot(path=str(OUT/'assignment-dialog.png'))
        await page.goto('http://127.0.0.1:5173/messages/assigned?scope=mine&user=2')
        await page.locator('.dx-data-row').first.wait_for()
        await page.screenshot(path=str(OUT/'my-work.png'))
        await page.get_by_role('button',name='Review',exact=True).first.click()
        await page.locator('[aria-label="Raw message content"]').wait_for()
        await page.screenshot(path=str(OUT/'review.png'))
        await page.get_by_role('dialog',name='Review message',exact=True).screenshot(path=str(OUT/'review-dialog.png'))
        dialog = await page.get_by_role('dialog',name='Review message',exact=True).bounding_box()
        actions = await page.locator('.review-decision-popup__actions').bounding_box()
        await page.screenshot(path=str(OUT/'review-detail.png'),clip={**dialog,'height':actions['y']+actions['height']+20-dialog['y']})
        await page.goto('http://127.0.0.1:5173/messages?user=admin')
        await page.locator('.dx-data-row').first.wait_for()
        row=page.locator('.dx-data-row').filter(has_text='MSG-00074-')
        await row.get_by_role('button',name='View audit trail').click()
        await page.locator('.audit-event').nth(3).wait_for()
        await page.screenshot(path=str(OUT/'audit.png'))
        await page.locator('.audit-drawer-panel').screenshot(path=str(OUT/'audit-panel.png'))
        await page.locator('.audit-event').filter(has_text='Review approved').screenshot(path=str(OUT/'audit-decision.png'))
        print('Captured 5 real interface screenshots using synthetic seed data.')
        await browser.close()

if __name__ == '__main__':
    asyncio.run(main())
