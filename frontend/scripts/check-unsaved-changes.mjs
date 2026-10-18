import assert from 'node:assert/strict';

// Every attempt must pass; these are repeated user interactions, not retries.
export async function checkUnsavedChanges(page, base) {
  await page.goto(`${base}admin`);
  const tab = (name) => page.getByRole('tab', { name, exact: true });
  const confirm = page.getByRole('dialog', {
    name: 'Unsaved changes',
    exact: true,
  });
  const keepEditing = async () => {
    await confirm
      .getByRole('button', { name: 'Keep editing', exact: true })
      .click();
    await confirm.waitFor({ state: 'hidden' });
    assert.equal(
      await confirm.count(),
      0,
      'Confirmation reopened after Keep editing',
    );
  };
  await page
    .getByRole('button', { name: 'Edit access', exact: true })
    .first()
    .click();
  await page.getByRole('button', { name: 'Remove scope', exact: true }).click();
  for (let attempt = 0; attempt < 8; attempt++) {
    await tab('Roles').click();
    await keepEditing();
    assert.equal(await tab('Users').getAttribute('aria-selected'), 'true');
    await page
      .getByText('No business access. Add a scope to assign roles.', {
        exact: true,
      })
      .waitFor();
    await page
      .getByRole('button', { name: 'Edit access', exact: true })
      .nth(1)
      .click();
    await keepEditing();
    await page
      .getByText('No business access. Add a scope to assign roles.', {
        exact: true,
      })
      .waitFor();
  }
  await tab('Roles').click();
  await confirm
    .getByRole('button', { name: 'Discard changes', exact: true })
    .click();
  await confirm.waitFor({ state: 'hidden' });
  const role = page.getByLabel('Role', { exact: true });
  await role.click();
  await page.getByRole('option', { name: 'Reviewer', exact: true }).click();
  await page.getByLabel('Role permissions', { exact: true }).click();
  await page.getByRole('option', { name: 'audit.view', exact: true }).click();
  await page.keyboard.press('Escape');
  for (let attempt = 0; attempt < 8; attempt++) {
    await role.click();
    await page.getByRole('option', { name: 'Auditor', exact: true }).click();
    await keepEditing();
    assert.equal(await role.inputValue(), 'Reviewer');
    await tab('Users').click();
    await keepEditing();
    assert.equal(await tab('Roles').getAttribute('aria-selected'), 'true');
    const editor = page.getByRole('region', {
      name: 'Role permissions editor',
      exact: true,
    });
    await editor
      .getByRole('button', { name: 'audit.view', exact: true })
      .waitFor();
  }
  // Escape must cancel the same pending transition and leave edits dirty.
  await tab('Users').click();
  await confirm
    .getByRole('button', { name: 'Keep editing', exact: true })
    .focus();
  await page.keyboard.press('Escape');
  await confirm.waitFor({ state: 'hidden' });
  assert.equal(await tab('Roles').getAttribute('aria-selected'), 'true');
  await tab('Users').click();
  await confirm
    .getByRole('button', { name: 'Discard changes', exact: true })
    .click();
  await confirm.waitFor({ state: 'hidden' });
  await page
    .getByRole('button', { name: 'Edit access', exact: true })
    .first()
    .waitFor();
  assert.equal(await tab('Users').getAttribute('aria-selected'), 'true');
}

export async function checkEditorSafety(page, base) {
  await page.goto(`${base}admin`);
  const confirm = page.getByRole('dialog', {
    name: 'Unsaved changes',
    exact: true,
  });
  const edit = page.getByRole('button', { name: 'Edit access', exact: true });
  await edit.first().click();
  const save = page.getByRole('button', { name: 'Save access', exact: true });
  assert.equal(await save.getAttribute('aria-disabled'), 'true');
  await page.getByRole('button', { name: 'Remove scope', exact: true }).click();
  await page.getByRole('link', { name: 'User profile', exact: true }).click();
  await confirm
    .getByRole('button', { name: 'Keep editing', exact: true })
    .click();
  await confirm.waitFor({ state: 'hidden' });
  assert.equal(new URL(page.url()).pathname, '/admin');
  await page
    .getByText('No business access. Add a scope to assign roles.', {
      exact: true,
    })
    .waitFor();
  // A native beforeunload prompt protects refresh/closing the browser tab.
  const unload = page.waitForEvent('dialog');
  const reload = page.reload().catch(() => undefined);
  const prompt = await unload;
  assert.equal(prompt.type(), 'beforeunload');
  await prompt.dismiss();
  await reload;
  await page
    .getByText('No business access. Add a scope to assign roles.', {
      exact: true,
    })
    .waitFor();
  let receiveSave;
  const started = new Promise((resolve) => {
    receiveSave = resolve;
  });
  const hold = (route) =>
    route.request().method() === 'PUT' ? receiveSave(route) : route.fallback();
  await page.route('**/api/admin/users/1/access', hold);
  await save.click();
  const request = await started;
  try {
    await page.getByRole('tab', { name: 'Roles', exact: true }).click();
    await page.getByRole('link', { name: 'User profile', exact: true }).click();
    await edit.nth(1).click();
    assert.equal(
      await confirm.count(),
      0,
      'Saving must block transitions without offering to discard an in-flight write',
    );
    assert.equal(new URL(page.url()).pathname, '/admin');
    assert.equal(
      await page
        .getByRole('tab', { name: 'Users', exact: true })
        .getAttribute('aria-selected'),
      'true',
    );
  } finally {
    await request.fulfill({ status: 204 });
    await page.unroute('**/api/admin/users/1/access', hold);
  }
  await page
    .getByRole('button', { name: 'Save access', exact: true })
    .waitFor();
  await page.getByText('All changes saved', { exact: true }).waitFor();
  await page.getByRole('tab', { name: 'Roles', exact: true }).click();
  await page.getByLabel('Role', { exact: true }).click();
  await page.getByRole('option', { name: 'Reviewer', exact: true }).click();
  await page.getByLabel('Role permissions', { exact: true }).click();
  await page.getByRole('option', { name: 'audit.view', exact: true }).click();
  await page.keyboard.press('Escape');
  await page.getByRole('link', { name: 'User profile', exact: true }).click();
  await confirm
    .getByRole('button', { name: 'Keep editing', exact: true })
    .click();
  await confirm.waitFor({ state: 'hidden' });
  await page
    .getByRole('status')
    .filter({ hasText: /^Unsaved changes$/ })
    .waitFor();
  await page.getByRole('button', { name: 'Reset', exact: true }).click();
  assert.equal(
    await page
      .getByRole('button', { name: 'Save permissions', exact: true })
      .getAttribute('aria-disabled'),
    'true',
  );
  await page.getByLabel('Role permissions', { exact: true }).click();
  await page.getByRole('option', { name: 'audit.view', exact: true }).click();
  await page.keyboard.press('Escape');
  await page.getByRole('link', { name: 'User profile', exact: true }).click();
  await confirm
    .getByRole('button', { name: 'Discard changes', exact: true })
    .click();
  await page.getByText('Global administrator', { exact: true }).waitFor();
}
