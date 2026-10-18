import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve } from 'node:path';
import { preview } from 'vite';
import { chromium } from 'playwright';
import {
  checkUnsavedChanges,
  checkEditorSafety,
} from './check-unsaved-changes.mjs';

const root = fileURLToPath(new URL('..', import.meta.url));
const artifacts = resolve(root, 'artifacts/smbc');
await mkdir(artifacts, { recursive: true });
const server = await preview({
  root,
  configFile: false,
  preview: { host: '127.0.0.1', port: 0 },
  logLevel: 'error',
});
const browser = await chromium.launch();
const errors = [];
const requests = [];
const assets = [];
const user = {
  userId: 1,
  userName: 'alex.morgan',
  displayName: 'Alex Morgan',
  isGlobalAdministrator: true,
  permissions: [
    'message.view',
    'message.assign',
    'review.level1',
    'review.undo',
    'audit.view',
  ],
  branches: [1],
  departments: [1],
  scopes: [],
};
const row = {
  id: 1,
  externalId: 'MSG-001',
  sender: 'BANK-A',
  receiver: 'BANK-B',
  messageType: 'MT103',
  direction: 'Incoming',
  branchId: 1,
  branchName: 'London',
  departmentIds: [1],
  departmentNames: 'Operations',
  state: 'New',
  receivedAt: '2026-10-18T08:00:00Z',
  currentAssigneeId: null,
  currentAssigneeName: null,
  activeReviewId: null,
  activeReviewLevel: null,
  activeReviewerId: null,
  activeReviewerName: null,
  workflowDefinitionId: 1,
  workflowDefinitionName: 'Standard',
  undoReviewId: null,
  requiredReviewLevels: [1, 2],
  canReview: true,
  canAssign: true,
  canViewAudit: true,
  canChangeWorkflow: true,
  canConfirm: true,
  rejectAllowed: true,
  confirmationMode: 'Direct',
  confirmationLevel: 1,
  version: 'v1',
};
const rows = Array.from({ length: 25 }, (_, i) => ({
  ...row,
  id: i + 1,
  externalId: `MSG-${String(i + 1).padStart(3, '0')}`,
}));
const catalog = {
  roles: [
    { id: 1, name: 'Reviewer', permissions: ['message.view'] },
    { id: 2, name: 'Auditor', permissions: ['audit.view'] },
  ],
  permissions: ['message.view', 'audit.view'],
  branches: [{ id: 1, name: 'London' }],
  departments: [{ id: 1, name: 'Operations' }],
};
function matches(item, filter) {
  if (!filter) return true;
  if (Array.isArray(filter[0])) {
    const values = filter
      .filter((_, index) => index % 2 === 0)
      .map((part) => matches(item, part));
    return filter.includes('or') ? values.some(Boolean) : values.every(Boolean);
  }
  const [field, operator, value] = filter;
  return operator === 'contains'
    ? String(item[field]).toLowerCase().includes(String(value).toLowerCase())
    : item[field] === value;
}
// Small valid one-page PDF, generated locally to exercise the actual PDF.js worker.
const objects = [
  '<< /Type /Catalog /Pages 2 0 R >>',
  '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
  '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R >>',
  '<< /Length 0 >>\nstream\n\nendstream',
];
let pdf = '%PDF-1.4\n';
const offsets = [0];
objects.forEach((object, index) => {
  offsets.push(Buffer.byteLength(pdf));
  pdf += `${index + 1} 0 obj\n${object}\nendobj\n`;
});
const xref = Buffer.byteLength(pdf);
pdf += `xref\n0 5\n0000000000 65535 f \n${offsets
  .slice(1)
  .map((offset) => `${String(offset).padStart(10, '0')} 00000 n \n`)
  .join('')}trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n${xref}\n%%EOF`;
let failRefresh = false;
async function installRoutes(page) {
  page.on('pageerror', (error) => errors.push(error.message));
  page.on('response', (response) => {
    if (/\.(woff2?|ttf|svg|ico)(\?|$)/.test(response.url()))
      assets.push({ url: response.url(), status: response.status() });
  });
  await page.route('**/api/**', async (route) => {
    const request = route.request(),
      url = new URL(request.url()),
      path = url.pathname;
    requests.push({
      path,
      method: request.method(),
      query: Object.fromEntries(url.searchParams),
      body: request.postDataJSON(),
    });
    const json = (body) => route.fulfill({ json: body });
    if (path === '/api/me') return json(user);
    if (path === '/api/branches') return json(catalog.branches);
    if (path === '/api/departments') return json(catalog.departments);
    if (path === '/api/users')
      return json([
        {
          id: 1,
          displayName: 'Alex Morgan',
          userName: 'alex.morgan',
          departmentIds: [1],
        },
      ]);
    if (path === '/api/message-types') return json(['MT103']);
    if (path === '/api/message-states')
      return json([
        {
          code: 'New',
          label: 'New',
          phase: 'Waiting',
          reviewLevel: 1,
          description: 'Waiting for assignment',
        },
      ]);
    if (path === '/api/workflows' || path.endsWith('/workflow-candidates'))
      return json([
        {
          id: 2,
          code: 'STANDARD',
          name: 'Standard',
          confirmationMode: 'Direct',
          isActive: true,
          steps: [],
        },
      ]);
    if (path === '/api/dashboard/summary')
      return json({
        totalMessages: 25,
        viewOnly: 0,
        unassigned: 25,
        pendingFirstConfirmation: 0,
        pendingSecondConfirmation: 0,
        completed: 0,
      });
    if (path === '/api/messages/grid') {
      if (failRefresh) {
        failRefresh = false;
        return route.fulfill({
          status: 500,
          json: { title: 'Refresh failed' },
        });
      }
      let data = rows.filter((item) =>
        matches(item, JSON.parse(url.searchParams.get('filter') ?? 'null')),
      );
      const sort = JSON.parse(url.searchParams.get('sort') ?? '[]') ?? [];
      if (sort[0])
        data = data.toSorted(
          (a, b) =>
            String(a[sort[0].selector]).localeCompare(
              String(b[sort[0].selector]),
            ) * (sort[0].desc ? -1 : 1),
        );
      const totalCount = data.length,
        skip = Number(url.searchParams.get('skip') ?? 0),
        take = Number(url.searchParams.get('take') ?? 20);
      return json({ data: data.slice(skip, skip + take), totalCount });
    }
    if (path.endsWith('/pdf'))
      return route.fulfill({
        contentType: 'application/pdf',
        body: Buffer.from(pdf),
      });
    if (path.endsWith('/audit'))
      return json({
        items: [
          {
            id: 1,
            eventType: 'MessageRegistered',
            timestamp: '2026-10-18T08:00:00Z',
            actor: null,
            oldState: null,
            newState: 'New',
            details: {},
          },
        ],
        totalCount: 1,
      });
    if (path.endsWith('/assignment-candidates'))
      return json([{ id: 2, userName: 'sam.lee', displayName: 'Sam Lee' }]);
    if (request.method() !== 'GET') return route.fulfill({ status: 204 });
    if (/\/api\/messages\/\d+$/.test(path))
      return json({
        ...row,
        id: Number(path.split('/').at(-1)),
        body: 'SWIFT MESSAGE BODY',
        confirmationMode: 'Direct',
        canConfirm: true,
      });
    if (path === '/api/admin/catalog') return json(catalog);
    if (path === '/api/admin/users/grid')
      return json({
        data: [
          { id: 1, userName: 'alex.morgan', displayName: 'Alex Morgan' },
          { id: 2, userName: 'sam.lee', displayName: 'Sam Lee' },
        ],
        totalCount: 2,
      });
    if (/\/api\/admin\/users\/\d+\/access$/.test(path))
      return json({
        userId: Number(path.split('/')[4]),
        userName: 'alex.morgan',
        displayName: 'Alex Morgan',
        assignments: [{ branchId: 1, departmentId: 1, roleIds: [1] }],
        scopes: [],
      });
    throw new Error(`Unhandled fixture ${request.method()} ${path}`);
  });
}
const base = server.resolvedUrls.local[0];
const countGrid = () =>
  requests.filter((request) => request.path === '/api/messages/grid').length;
const waitFor = async (predicate) => {
  for (let i = 0; i < 200; i++) {
    if (predicate()) return;
    await new Promise((resolve) => setTimeout(resolve, 25));
  }
  throw new Error('Expected request was not made');
};
try {
  for (const reducedMotion of ['reduce', 'no-preference']) {
    const confirmationPage = await browser.newPage({
      viewport: { width: 1440, height: 1000 },
      reducedMotion,
    });
    confirmationPage.setDefaultTimeout(10000);
    await installRoutes(confirmationPage);
    await checkUnsavedChanges(confirmationPage, base);
    await checkEditorSafety(confirmationPage, base);
    await confirmationPage.close();
    console.log(
      `PASS: unsaved user/role/tab/route protection, browser unload, in-flight save lock and repeated cancellation (${reducedMotion}), no retries.`,
    );
  }
  const page = await browser.newPage({
    viewport: { width: 1440, height: 1000 },
    reducedMotion: 'reduce',
  });
  page.setDefaultTimeout(10000);
  await installRoutes(page);
  await page.goto(`${base}messages`);
  const grid = page.locator('.message-grid-results .dx-datagrid');
  await grid.locator('.dx-data-row').first().waitFor();
  assert.equal(await grid.locator('.dx-data-row').count(), 20);
  await grid.locator('.dx-page').filter({ hasText: /^2$/ }).click();
  await grid.getByText('MSG-021', { exact: true }).waitFor();
  assert(requests.some((request) => request.query.skip === '20'));
  await grid.getByText('External ID', { exact: true }).click();
  await waitFor(() => requests.some((request) => request.query.sort));
  const input = grid.locator('.dx-datagrid-filter-row input').first();
  await input.fill('MSG-001');
  await input.press('Tab');
  await grid.getByText('MSG-001', { exact: true }).waitFor();
  assert.equal(await grid.locator('.dx-data-row').count(), 1);
  assert(requests.some((request) => request.query.filter?.includes('MSG-001')));
  const beforeRefresh = countGrid();
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await waitFor(() => countGrid() > beforeRefresh);
  await page.locator('.dx-loadpanel-content:visible').waitFor({ state: 'hidden' });
  const moreActions = grid
    .getByRole('button', { name: 'More', exact: true })
    .first();
  await moreActions.click();
  const actions = page.getByRole('group', {
    name: 'More message actions',
    exact: true,
  });
  await page.waitForFunction(
    (element) => element.contains(document.activeElement),
    await actions.elementHandle(),
  );
  await page.keyboard.press('End');
  assert(
    await actions.evaluate((element) =>
      element.contains(document.activeElement),
    ),
  );
  await page.keyboard.press('Home');
  assert(
    await actions.evaluate((element) =>
      element.contains(document.activeElement),
    ),
  );
  await page.screenshot({
    path: resolve(artifacts, 'message-actions-1440.png'),
  });
  await page.keyboard.press('Escape');
  await actions.waitFor({ state: 'hidden' });
  await page.waitForFunction(
    (element) => element === document.activeElement,
    await moreActions.elementHandle(),
  );
  const auditTrigger = page.getByRole('button', {
    name: 'View audit trail',
    exact: true,
  });
  await grid.getByRole('button', { name: 'More', exact: true }).first().click();
  await auditTrigger.click();
  const audit = page.getByRole('dialog', { name: 'Audit trail', exact: true });
  await audit.getByText('Message registered', { exact: true }).waitFor();
  await page.waitForFunction(
    () =>
      document.activeElement?.getAttribute('aria-label') ===
      'Close audit trail',
  );
  await page.keyboard.press('Shift+Tab');
  assert(
    await audit.evaluate((element) => element.contains(document.activeElement)),
  );
  await page.keyboard.press('Escape');
  await audit.waitFor({ state: 'hidden' });
  await page.waitForFunction(
    (element) => element === document.activeElement,
    await grid
      .getByRole('button', { name: 'More', exact: true })
      .first()
      .elementHandle(),
  );
  await page.getByRole('button', { name: 'View', exact: true }).click();
  const view = page.getByRole('dialog', { name: 'View message', exact: true });
  await view
    .getByLabel('Raw message content')
    .getByText('SWIFT MESSAGE BODY', { exact: true })
    .waitFor();
  await page.waitForFunction(
    (element) => element.contains(document.activeElement),
    await view.elementHandle(),
  );
  await page.keyboard.press('Escape');
  await view.waitFor({ state: 'hidden' });
  await grid.getByRole('button', { name: 'More', exact: true }).first().click();
  await page.getByRole('button', { name: 'PDF', exact: true }).click();
  await page.getByRole('img', { name: 'PDF page 1', exact: true }).waitFor();
  await page.getByRole('link', { name: 'Download PDF', exact: true }).waitFor();
  await page.waitForFunction(() =>
    document.querySelector('[role=dialog]')?.contains(document.activeElement),
  );
  await page.keyboard.press('Escape');
  await grid.getByRole('button', { name: 'More', exact: true }).first().click();
  await page.getByRole('button', { name: 'Assign', exact: true }).click();
  await page.getByLabel('Reviewer', { exact: true }).click();
  await page.getByRole('option', { name: 'Sam Lee', exact: true }).click();
  failRefresh = true;
  await page
    .getByRole('dialog', { name: 'Assign message', exact: true })
    .getByRole('button', { name: 'Assign', exact: true })
    .click();
  await page.getByText('Message MSG-001 assigned.', { exact: true }).waitFor();
  assert(
    requests.some(
      (request) =>
        request.path === '/api/messages/1/assign' &&
        request.body.assignedTo === 2,
    ),
  );
  await page
    .getByText(
      'Changes saved, but the displayed data could not be refreshed. Refresh the list before continuing.',
      { exact: true },
    )
    .waitFor();
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await page
    .locator('.dx-loadpanel-content:visible')
    .waitFor({ state: 'hidden' });
  await page.screenshot({
    path: resolve(artifacts, 'messages-1440.png'),
    fullPage: true,
  });
  await page.getByRole('link', { name: 'Users & access', exact: true }).click();
  await page
    .getByRole('button', { name: 'Edit access', exact: true })
    .first()
    .click();
  await page.getByRole('button', { name: 'Remove scope', exact: true }).click();
  await page.getByRole('tab', { name: 'Roles', exact: true }).click();
  const confirm = page.getByRole('dialog', {
    name: 'Unsaved changes',
    exact: true,
  });
  await confirm
    .getByRole('button', { name: 'Keep editing', exact: true })
    .click();
  await confirm.waitFor({ state: 'hidden' });
  assert.equal(
    await page
      .getByRole('tab', { name: 'Users', exact: true })
      .getAttribute('aria-selected'),
    'true',
  );
  await page.getByRole('tab', { name: 'Roles', exact: true }).click();
  await confirm
    .getByRole('button', { name: 'Discard changes', exact: true })
    .click();
  await page.getByLabel('Role', { exact: true }).click();
  await page.getByRole('option', { name: 'Reviewer', exact: true }).click();
  await page.getByLabel('Role permissions', { exact: true }).click();
  await page.getByRole('option', { name: 'audit.view', exact: true }).click();
  await page.keyboard.press('Escape');
  await page
    .getByRole('button', { name: 'Save permissions', exact: true })
    .click();
  await page.getByText('Changes saved.', { exact: true }).waitFor();
  assert(
    requests.some(
      (request) =>
        request.path === '/api/admin/roles/1/permissions' &&
        request.method === 'PUT',
    ),
  );
  await page
    .locator('.dx-loadpanel-content:visible')
    .waitFor({ state: 'hidden' });
  await page.screenshot({
    path: resolve(artifacts, 'administration-1440.png'),
    fullPage: true,
  });
  await page.getByRole('link', { name: 'User profile', exact: true }).click();
  await page.getByText('Global administrator', { exact: true }).waitFor();
  assert.equal(
    await page
      .locator('.global-header img')
      .evaluate((image) => image.naturalWidth > 0),
    true,
  );
  await page.evaluate(() => document.fonts.ready);
  assert(
    assets.some(
      (asset) => asset.url.includes('myriad-pro') && asset.status === 200,
    ),
  );
  assert(
    assets.every((asset) => asset.url.startsWith(base) && asset.status === 200),
  );
  for (const width of [320, 768, 1440]) {
    await page.setViewportSize({ width, height: 1000 });
    await page.getByText('Global administrator', { exact: true }).waitFor();
    await page.evaluate(() => document.fonts.ready);
    assert(
      await page.evaluate(() =>
        parseFloat(getComputedStyle(document.documentElement).scrollPaddingTop) >=
        document.querySelector('.global-header').getBoundingClientRect().height,
      ),
      `Scroll offset must clear the sticky header at ${width}px`,
    );
    await page
      .locator('.dx-loadpanel-content:visible')
      .waitFor({ state: 'hidden' });
    await page.screenshot({
      path: resolve(artifacts, `profile-${width}.png`),
      fullPage: true,
    });
    assert(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth + 1,
      ),
      `Horizontal page overflow at ${width}px`,
    );
    if (width === 320) {
      await page
        .getByRole('button', { name: 'Open navigation', exact: true })
        .click();
      const navigation = page.getByRole('dialog', {
        name: 'Navigation',
        exact: true,
      });
      await page.waitForFunction(() =>
        document
          .querySelector('#application-navigation')
          ?.contains(document.activeElement),
      );
      await page.keyboard.press('Shift+Tab');
      assert(
        await navigation.evaluate((element) =>
          element.contains(document.activeElement),
        ),
      );
      await page.keyboard.press('Tab');
      assert(
        await navigation.evaluate((element) =>
          element.contains(document.activeElement),
        ),
      );
      await page.keyboard.press('Escape');
      const navTrigger = page.getByRole('button', {
        name: 'Open navigation',
        exact: true,
      });
      await navTrigger.waitFor();
      await page.waitForFunction(
        (element) => element === document.activeElement,
        await navTrigger.elementHandle(),
      );
      await navTrigger.click();
      await page.getByRole('link', { name: 'Messages', exact: true }).click();
      await grid.locator('.dx-data-row').first().waitFor();
      const stageFilter = page.getByRole('combobox', { name: 'Filter by state', exact: true });
      await stageFilter.click();
      await page
        .getByRole('option', { name: 'Unassigned (25)', exact: true })
        .click();
      await waitFor(() =>
        requests.some((request) => request.query.filter?.includes('New')),
      );
      await stageFilter.click();
      await page
        .getByRole('option', { name: 'Total Messages (25)', exact: true })
        .click();

      await page
        .locator('.dx-loadpanel-content:visible')
        .waitFor({ state: 'hidden' });
      await page
        .locator('.dx-loadpanel-content:visible')
        .waitFor({ state: 'hidden' });
      await page.screenshot({
        path: resolve(artifacts, 'messages-320.png'),
        fullPage: true,
      });
      await page
        .getByRole('button', { name: 'Open navigation', exact: true })
        .waitFor();
      assert(
        await page.evaluate(
          () => document.documentElement.scrollWidth <= innerWidth + 1,
        ),
        'Mobile grid overflows page',
      );
      await grid
        .getByRole('button', { name: 'More', exact: true })
        .first()
        .click();
      await page
        .getByRole('button', { name: 'View audit trail', exact: true })
        .first()
        .click();
      const mobileAudit = page.getByRole('dialog', {
        name: 'Audit trail',
        exact: true,
      });
      await mobileAudit
        .getByText('Message registered', { exact: true })
        .waitFor();
      assert((await mobileAudit.boundingBox()).width <= width);
      await page
        .locator('.dx-loadpanel-content:visible')
        .waitFor({ state: 'hidden' });
      await page.screenshot({
        path: resolve(artifacts, 'audit-320.png'),
        fullPage: true,
      });
      await page.keyboard.press('Escape');
      await mobileAudit.waitFor({ state: 'hidden' });
      await page.goto(`${base}me`);
    }
  }
  // Exercise the audit focus lifecycle with normal vendor animations too.
  const animated = await browser.newPage({
    viewport: { width: 1440, height: 1000 },
    reducedMotion: 'no-preference',
  });
  animated.setDefaultTimeout(10000);
  await installRoutes(animated);
  await animated.goto(`${base}messages`);
  const animatedTrigger = animated
    .getByRole('button', { name: 'View audit trail', exact: true })
    .first();
  const moreTrigger = animated
    .getByRole('button', { name: 'More', exact: true })
    .first();
  await moreTrigger.click();
  await animatedTrigger.click();
  const animatedAudit = animated.getByRole('dialog', {
    name: 'Audit trail',
    exact: true,
  });
  await animatedAudit
    .getByText('Message registered', { exact: true })
    .waitFor();
  await animated.waitForFunction(
    () =>
      document.activeElement?.getAttribute('aria-label') ===
      'Close audit trail',
  );
  await animated.keyboard.press('Escape');
  await animatedAudit.waitFor({ state: 'hidden' });
  await animated.waitForFunction(
    (element) => element === document.activeElement,
    await moreTrigger.elementHandle(),
  );
  await animated.close();
  assert.deepEqual(errors, []);
  await writeFile(
    resolve(artifacts, 'requests.json'),
    JSON.stringify(requests, null, 2),
  );
  console.log(
    'PASS: production SMBC packages, server grid paging/sort/filter/refresh, assignment, audit focus/Escape, raw message, PDF worker, unsaved access confirmation, role save/toast, routes, local assets, 320/768/1440px and reduced motion.',
  );
} catch (error) {
  for (const [index, page] of browser
    .contexts()
    .flatMap((context) => context.pages())
    .entries()) {
    await page.screenshot({
      path: resolve(artifacts, `failure-${index}.png`),
      fullPage: true,
    });
    console.error(
      'Active element:',
      await page.evaluate(() => ({
        tag: document.activeElement?.tagName,
        name: document.activeElement?.getAttribute('aria-label'),
      })),
    );
  }
  throw error;
} finally {
  await browser.close();
  server.httpServer.close();
}
