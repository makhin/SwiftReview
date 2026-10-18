import assert from 'node:assert/strict';

// Every attempt must pass; these are repeated user interactions, not retries.
export async function checkUnsavedChanges(page, base) {
  await page.goto(`${base}admin`);
  const tab = (name) => page.getByRole('tab', { name, exact: true });
  const confirm = page.getByRole('dialog', { name: 'Unsaved changes', exact: true });
  const keepEditing = async () => {
    await confirm.getByRole('button', { name: 'Keep editing', exact: true }).click();
    await confirm.waitFor({ state: 'hidden' });
    assert.equal(await confirm.count(), 0, 'Confirmation reopened after Keep editing');
  };
  await page.getByRole('button', { name: 'Edit access', exact: true }).first().click();
  await page.getByRole('button', { name: 'Remove scope', exact: true }).click();
  for (let attempt = 0; attempt < 8; attempt++) {
    await tab('Roles').click();
    await keepEditing();
    assert.equal(await tab('Users').getAttribute('aria-selected'), 'true');
    await page.getByText('No business access. Add a scope to assign roles.', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Edit access', exact: true }).nth(1).click();
    await keepEditing();
    await page.getByText('No business access. Add a scope to assign roles.', { exact: true }).waitFor();
  }
  await tab('Roles').click();
  await confirm.getByRole('button', { name: 'Discard changes', exact: true }).click();
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
    const editor = page.getByRole('region', { name: 'Role permissions editor', exact: true });
    await editor.getByRole('button', { name: 'audit.view', exact: true }).waitFor();
  }
  // Escape must cancel the same pending transition and leave edits dirty.
  await tab('Users').click();
  await confirm.getByRole('button', { name: 'Keep editing', exact: true }).focus();
  await page.keyboard.press('Escape');
  await confirm.waitFor({ state: 'hidden' });
  assert.equal(await tab('Roles').getAttribute('aria-selected'), 'true');
  await tab('Users').click();
  await confirm.getByRole('button', { name: 'Discard changes', exact: true }).click();
  await confirm.waitFor({ state: 'hidden' });
  await page.getByRole('button', { name: 'Edit access', exact: true }).first().waitFor();
  assert.equal(await tab('Users').getAttribute('aria-selected'), 'true');
}
