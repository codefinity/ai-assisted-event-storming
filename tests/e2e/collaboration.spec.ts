import { expect, test, type Browser, type Page } from '@playwright/test';

// Two people, two browsers, one board: everything one does, the other sees without reloading.

const run = Date.now().toString(36);
const password = 'correct horse battery staple';

async function signUp(page: Page, name: string, email: string) {
  await page.getByLabel('Your name').fill(name);
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Create account' }).click();
}

async function newPerson(browser: Browser) {
  const context = await browser.newContext();
  return context.newPage();
}

/** Where a sticky is drawn on the board (world coordinates), from its transform. */
async function positionOf(page: Page, name: string) {
  const transform = await page.getByRole('button', { name }).evaluate((node) => (node as HTMLElement).style.transform);
  const [, x, y] = /translate\((-?[\d.]+)px, (-?[\d.]+)px\)/.exec(transform) ?? [];
  return { x: Number(x), y: Number(y) };
}

async function waitUntilLive(page: Page) {
  await expect(page.getByRole('status').filter({ hasText: 'Live' })).toBeVisible();
}

test('two people build a board together and see each other’s changes live', async ({ browser }) => {
  const ana = await newPerson(browser);
  const bo = await newPerson(browser);

  await test.step('Ana signs up and creates a team', async () => {
    await ana.goto('/sign-up');
    await signUp(ana, 'Ana Lima', `ana-${run}@example.test`);
    await expect(ana.getByRole('heading', { name: 'Your teams' })).toBeVisible();
    await ana.getByLabel('Name your first team').fill(`Food delivery ${run}`);
    await ana.getByRole('button', { name: 'Create team' }).click();
    await ana.waitForURL(/\/teams\/[0-9a-f-]{36}$/);
    await expect(ana.getByRole('heading', { level: 1, name: `Food delivery ${run}` })).toBeVisible();
  });

  const teamUrl = ana.url();

  const invitationLink = await test.step('Ana invites Bo as an editor with a link', async () => {
    await ana.getByRole('link', { name: 'Members & API keys' }).click();
    const invite = ana.getByRole('region', { name: 'Invite people' });
    await invite.getByLabel('Role', { exact: true }).selectOption('editor');
    await invite.getByRole('button', { name: 'Create link' }).click();
    const link = await invite.getByLabel('Invitation link').inputValue();
    expect(link).toContain('/invitations/');
    return link;
  });

  const boardUrl = await test.step('Ana creates a Big Picture board', async () => {
    await ana.goto(teamUrl);
    await ana.getByRole('button', { name: 'New board' }).click();
    const dialog = ana.getByRole('dialog', { name: 'New board' });
    await dialog.getByLabel('Name').fill('Ordering');
    await dialog.getByLabel('Level').selectOption({ label: 'Big Picture' });
    await dialog.getByRole('button', { name: 'Create board' }).click();
    await ana.waitForURL(/\/boards\//);
    await waitUntilLive(ana);
    return ana.url();
  });

  await test.step('Bo accepts the invitation with a new account and opens the board', async () => {
    await bo.goto(invitationLink);
    await expect(bo.getByRole('heading', { name: `Join Food delivery ${run}` })).toBeVisible();
    await bo.getByRole('link', { name: 'Create an account' }).click();
    await signUp(bo, 'Bo Chen', `bo-${run}@example.test`);
    await bo.getByRole('button', { name: /Join as Bo Chen/ }).click();
    await bo.waitForURL(teamUrl);
    await expect(bo.getByRole('heading', { level: 1, name: `Food delivery ${run}` })).toBeVisible();
    await bo.getByRole('link', { name: 'Ordering' }).click();
    await expect(bo).toHaveURL(boardUrl);
    await waitUntilLive(bo);
  });

  await test.step('each sees the other on the board', async () => {
    await expect(ana.getByRole('group', { name: /On this board: .*Bo Chen/ })).toBeVisible();
    await expect(bo.getByRole('group', { name: /On this board: .*Ana Lima/ })).toBeVisible();
  });

  await test.step('Ana adds a Domain Event from the keyboard; Bo sees it appear', async () => {
    const canvas = ana.getByRole('application', { name: 'Board: Ordering' });
    const box = (await canvas.boundingBox())!;
    await ana.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await ana.keyboard.press('e');
    await ana.keyboard.type('Order Placed');
    await ana.keyboard.press('Enter');
    await expect(ana.getByRole('button', { name: 'Domain Event: Order Placed' })).toBeVisible();
    await expect(bo.getByRole('button', { name: 'Domain Event: Order Placed' })).toBeVisible();
  });

  await test.step('Bo sees Ana’s cursor', async () => {
    const canvas = ana.getByRole('application', { name: 'Board: Ordering' });
    const box = (await canvas.boundingBox())!;
    for (let step = 0; step < 5; step++) await ana.mouse.move(box.x + 200 + step * 20, box.y + 200);
    await expect(bo.locator('.remote-cursor', { hasText: 'Ana Lima' })).toBeVisible();
  });

  await test.step('Bo drags the sticky; Ana sees it move', async () => {
    const before = await positionOf(ana, 'Domain Event: Order Placed');
    const sticky = bo.getByRole('button', { name: 'Domain Event: Order Placed' });
    const box = (await sticky.boundingBox())!;
    await bo.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await bo.mouse.down();
    await bo.mouse.move(box.x + box.width / 2 + 150, box.y + box.height / 2 + 60, { steps: 12 });
    await bo.mouse.up();
    await expect.poll(async () => (await positionOf(ana, 'Domain Event: Order Placed')).x).toBeGreaterThan(before.x + 50);
    expect(await positionOf(ana, 'Domain Event: Order Placed')).toEqual(await positionOf(bo, 'Domain Event: Order Placed'));
  });

  await test.step('Bo adds a Hot Spot; Ana sees it, and undoing it removes it for both', async () => {
    const canvas = bo.getByRole('application', { name: 'Board: Ordering' });
    const box = (await canvas.boundingBox())!;
    await bo.mouse.move(box.x + box.width / 2, box.y + box.height - 150);
    await bo.keyboard.press('h');
    await bo.keyboard.type('What if the payment fails?');
    await bo.keyboard.press('Enter');
    await expect(ana.getByRole('button', { name: 'Hot Spot: What if the payment fails?' })).toBeVisible();

    await bo.keyboard.press('Control+z');
    await expect(bo.getByRole('button', { name: 'Hot Spot: What if the payment fails?' })).toHaveCount(0);
    await expect(ana.getByRole('button', { name: 'Hot Spot: What if the payment fails?' })).toHaveCount(0);
    await expect(ana.getByRole('button', { name: 'Domain Event: Order Placed' })).toBeVisible();
  });

  await ana.context().close();
  await bo.context().close();
});
