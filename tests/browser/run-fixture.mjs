import { spawn, spawnSync } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdir, mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import process from 'node:process';
import { performance } from 'node:perf_hooks';
import { chromium } from 'playwright';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(here, '../..');
const fixtureProject = path.join(repoRoot, 'samples/DiagnosticFixture/AuthStateDoctor.DiagnosticFixture.csproj');
const staticFixtureProject = path.join(repoRoot, 'samples/StaticSsrFixture/AuthStateDoctor.StaticSsrFixture.csproj');
const scenarioId = 'synthetic-cookie-fixture';
const maxWaitMs = 30_000;

function parseOutput(args) {
  let output = null;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--output' && i + 1 < args.length) output = args[++i];
    else throw new Error('invalid-arguments');
  }
  if (!output) throw new Error('output-required');
  return path.resolve(output);
}

async function reserveLoopbackPort() {
  const server = createServer();
  await new Promise((resolve, reject) => {
    server.once('error', reject);
    server.listen(0, '127.0.0.1', resolve);
  });
  const address = server.address();
  if (!address || typeof address === 'string') throw new Error('port-allocation-failed');
  const port = address.port;
  await new Promise((resolve, reject) => server.close(error => error ? reject(error) : resolve()));
  return port;
}

async function waitUntilReady(child, url) {
  const deadline = Date.now() + maxWaitMs;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error('fixture-exited');
    try {
      const response = await fetch(`${url}/_authdoctor/server-state`, { signal: AbortSignal.timeout(750) });
      if (response.ok) return;
    } catch {
      // Startup is expected to take a few seconds; no response body or path is logged.
    }
    await new Promise(resolve => setTimeout(resolve, 300));
  }
  throw new Error('fixture-start-timeout');
}

async function stopChild(child) {
  if (!child || child.exitCode !== null) return;
  child.kill('SIGTERM');
  await Promise.race([
    new Promise(resolve => child.once('exit', resolve)),
    new Promise(resolve => setTimeout(resolve, 5_000))
  ]);
  if (child.exitCode === null) child.kill('SIGKILL');
}

async function main() {
  let outputPath;
  try {
    outputPath = parseOutput(process.argv.slice(2));
  } catch {
    console.error('Uso: node run-fixture.mjs --output <trace.json>');
    return 2;
  }

  let publishDir;
  let staticPublishDir;
  let child;
  let staticChild;
  let browser;
  let phase = 'startup';
  const started = performance.now();
  const observations = [];
  const record = (transition, serverAuthenticated, uiAuthenticated, observedScenarioId = scenarioId) => {
    if (typeof serverAuthenticated !== 'boolean' || typeof uiAuthenticated !== 'boolean') {
      throw new Error('state-not-boolean');
    }
    const elapsedMs = Math.max(observations.at(-1)?.elapsedMs ?? 0, Math.round(performance.now() - started));
    const mismatch = serverAuthenticated !== uiAuthenticated;
    observations.push({
      scenarioId: observedScenarioId,
      transition,
      elapsedMs: Math.min(elapsedMs, 86_400_000),
      serverAuthenticated,
      uiAuthenticated,
      expectation: mismatch ? 'full-reload-required' : 'converge'
    });
  };

  try {
    phase = 'publish-fixture';
    publishDir = await mkdtemp(path.join(tmpdir(), 'authdoctor-fixture-'));
    const publishResult = spawnSync('dotnet', [
      'publish', fixtureProject, '--configuration', 'Release', '--nologo', '--output', publishDir
    ], {
      cwd: path.dirname(fixtureProject),
      env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' },
      stdio: 'ignore',
      timeout: 120_000
    });
    if (publishResult.error || publishResult.status !== 0) throw new Error('fixture-publish-failed');

    phase = 'production-guard';
    const fixtureDll = path.join(publishDir, 'AuthStateDoctor.DiagnosticFixture.dll');
    const productionGuard = spawnSync('dotnet', [fixtureDll], {
      cwd: publishDir,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Production',
        ASPNETCORE_URLS: 'http://127.0.0.1:0',
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        DOTNET_NOLOGO: '1'
      },
      stdio: 'ignore',
      timeout: 10_000
    });
    if (productionGuard.error || productionGuard.status === 0) throw new Error('probe-production-guard-failed');

    const port = await reserveLoopbackPort();
    const baseUrl = `http://127.0.0.1:${port}`;
    child = spawn('dotnet', [fixtureDll], {
      cwd: publishDir,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Testing',
        ASPNETCORE_URLS: baseUrl,
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        DOTNET_NOLOGO: '1',
        Logging__LogLevel__Default: 'Warning'
      },
      stdio: 'ignore'
    });

    await waitUntilReady(child, baseUrl);
    browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ serviceWorkers: 'block' });
    phase = 'origin-guard';
    const offOriginResponse = await context.request.post(`${baseUrl}/_authdoctor/login`, {
      headers: { Origin: 'https://attacker.invalid' }
    });
    if (offOriginResponse.status() !== 404) throw new Error('off-origin-post-accepted');
    const page = await context.newPage();
    let circuitWebSocketCount = 0;
    page.on('websocket', socket => {
      try {
        if (new URL(socket.url()).pathname.endsWith('/_blazor')) circuitWebSocketCount++;
      } catch {
        // Ignore malformed/non-Blazor socket URLs without recording them.
      }
    });
    phase = 'initial-render';
    const response = await page.goto(baseUrl, { waitUntil: 'domcontentloaded' });
    if (!response?.ok()) throw new Error('initial-page-failed');

    const prerenderHtml = await response.text();
    const prerenderMatch = prerenderHtml.match(/data-authdoctor-probe="v1"\s+data-authenticated="(true|false)"/);
    if (!prerenderMatch) throw new Error('prerender-state-missing');
    record('prerender', false, prerenderMatch[1] === 'true');

    const probeLocator = page.locator('[data-authdoctor-probe="v1"]');
    await probeLocator.waitFor({ state: 'attached', timeout: maxWaitMs });
    if (await probeLocator.count() !== 1) throw new Error('probe-default-opt-in-invalid');
    phase = 'probe-shape';
    const probeShape = await probeLocator.evaluate(element => ({
      attributes: Array.from(element.attributes).map(attribute => attribute.name).sort(),
      hasChildren: element.childElementCount !== 0,
      hasText: element.textContent.trim().length !== 0
    }));
    if (JSON.stringify(probeShape.attributes) !== JSON.stringify(['aria-hidden', 'data-authdoctor-probe', 'data-authenticated', 'hidden'])
      || probeShape.hasChildren || probeShape.hasText) throw new Error('probe-contract-invalid');

    phase = 'hydration-marker';
    await page.waitForFunction(() => document.querySelector('[data-testid="hydration-state"]')?.getAttribute('data-hydrated') === 'true', null, { timeout: maxWaitMs });
    phase = 'interactive-event';
    await page.getByTestId('interactive-probe').click();
    await page.waitForFunction(() => document.querySelector('[data-testid="interactive-count"]')?.textContent?.includes('1') === true, null, { timeout: maxWaitMs });
    if (circuitWebSocketCount === 0) throw new Error('interactive-server-circuit-missing');
    record('hydrate', await readServerState(page), await readUiState(page));

    phase = 'login';
    await postSameOrigin(page, '/_authdoctor/login');
    await waitForServerState(page, true);
    record('login', await readServerState(page), await readUiState(page));
    if (await page.evaluate(() => document.cookie.includes('AuthDoctorFixture'))) throw new Error('auth-cookie-accessible-to-javascript');
    const allowedStatus = await protectedStatus(page);
    if (allowedStatus !== 204) throw new Error('authenticated-probe-denied');

    phase = 'login-refresh';
    await page.reload({ waitUntil: 'domcontentloaded' });
    await waitForUiState(page, true);
    record('hard-refresh', await readServerState(page), await readUiState(page));

    phase = 'second-tab-login';
    const secondPage = await context.newPage();
    const secondResponse = await secondPage.goto(baseUrl, { waitUntil: 'domcontentloaded' });
    if (!secondResponse?.ok()) throw new Error('second-tab-initial-page-failed');
    await waitForUiState(secondPage, true);
    record('second-tab', await readServerState(secondPage), await readUiState(secondPage), 'synthetic-second-tab');
    if (await protectedStatus(secondPage) !== 204) throw new Error('second-tab-protected-probe-denied');

    phase = 'logout';
    await postSameOrigin(page, '/_authdoctor/logout');
    await waitForServerState(page, false);
    record('logout', await readServerState(page), await readUiState(page));
    const deniedStatus = await protectedStatus(page);
    if (deniedStatus !== 401) throw new Error('unauthenticated-probe-was-allowed');

    phase = 'second-tab-after-logout';
    await waitForServerState(secondPage, false);
    record('second-tab', await readServerState(secondPage), await readUiState(secondPage), 'synthetic-second-tab');
    if (await protectedStatus(secondPage) !== 401) throw new Error('second-tab-unauthenticated-probe-was-allowed');
    await secondPage.reload({ waitUntil: 'domcontentloaded' });
    await waitForUiState(secondPage, false);
    record('hard-refresh', await readServerState(secondPage), await readUiState(secondPage), 'synthetic-second-tab');
    await secondPage.close();

    phase = 'enhanced-navigation';
    const documentTimeOrigin = await page.evaluate(() => performance.timeOrigin);
    await page.getByTestId('nav-protected').click();
    await page.waitForURL(url => url.pathname === '/protected', { timeout: maxWaitMs });
    const afterNavigationTimeOrigin = await page.evaluate(() => performance.timeOrigin);
    if (afterNavigationTimeOrigin !== documentTimeOrigin) throw new Error('navigation-was-not-enhanced');
    await page.getByTestId('protected-page').waitFor({ state: 'visible', timeout: maxWaitMs });
    record('enhanced-navigation', await readServerState(page), await readUiState(page));

    phase = 'return-home-after-logout';
    await page.getByTestId('nav-home').click();
    await page.waitForURL(url => url.pathname === '/', { timeout: maxWaitMs });

    phase = 'logout-refresh';
    await page.reload({ waitUntil: 'domcontentloaded' });
    phase = 'logout-refresh-state';
    await waitForUiState(page, false);
    phase = 'logout-refresh-checkpoint';
    record('hard-refresh', await readServerState(page), await readUiState(page));

    phase = 'publish-static-fixture';
    await stopChild(child);
    child = null;
    staticPublishDir = await mkdtemp(path.join(tmpdir(), 'authdoctor-static-fixture-'));
    const staticPublishResult = spawnSync('dotnet', [
      'publish', staticFixtureProject, '--configuration', 'Release', '--nologo', '--output', staticPublishDir
    ], {
      cwd: path.dirname(staticFixtureProject),
      env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' },
      stdio: 'ignore',
      timeout: 120_000
    });
    if (staticPublishResult.error || staticPublishResult.status !== 0) throw new Error('static-fixture-publish-failed');
    const staticPort = await reserveLoopbackPort();
    const staticBaseUrl = `http://127.0.0.1:${staticPort}`;
    staticChild = spawn('dotnet', [path.join(staticPublishDir, 'AuthStateDoctor.StaticSsrFixture.dll')], {
      cwd: staticPublishDir,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Testing',
        ASPNETCORE_URLS: staticBaseUrl,
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        DOTNET_NOLOGO: '1',
        Logging__LogLevel__Default: 'Warning'
      },
      stdio: 'ignore'
    });
    await waitUntilReady(staticChild, staticBaseUrl);

    phase = 'static-ssr';
    const socketsBeforeStatic = circuitWebSocketCount;
    const staticResponse = await page.goto(staticBaseUrl, { waitUntil: 'domcontentloaded' });
    if (!staticResponse?.ok()) throw new Error('static-ssr-page-failed');
    await page.getByTestId('static-render').waitFor({ state: 'visible', timeout: maxWaitMs });
    if (await page.locator('[data-testid="hydration-state"]').count() !== 0
      || await page.locator('[data-testid="interactive-probe"]').count() !== 0) {
      throw new Error('static-ssr-contained-interactive-markers');
    }
    const staticServerState = await readServerState(page);
    const staticUiState = await readUiState(page);
    record('static-render', staticServerState, staticUiState, 'synthetic-static-ssr');
    await page.waitForTimeout(250);
    if (circuitWebSocketCount !== socketsBeforeStatic) throw new Error('static-ssr-opened-interactive-circuit');

    const trace = { schema: 'authdoctor-trace/v1', observations };
    const serialized = `${JSON.stringify(trace, null, 2)}\n`;
    await mkdir(path.dirname(outputPath), { recursive: true });
    await writeFile(outputPath, serialized, { encoding: 'utf8', mode: 0o600 });
    console.log(`Harness local concluiu: ${observations.length} observações sanitizadas; ${observations.filter(item => item.expectation === 'full-reload-required' && item.serverAuthenticated !== item.uiAuthenticated).length} divergência(s) observada(s) antes do full reload.`);
    return 0;
  } catch {
    // Avoid echoing browser exceptions, app logs, URLs, cookies, or input file paths.
    console.error(`Falha do harness local durante a etapa '${phase}'. Nenhum conteúdo bruto da página foi registrado.`);
    return 1;
  } finally {
    if (browser) await browser.close().catch(() => {});
    await stopChild(child);
    await stopChild(staticChild);
    if (publishDir) await rm(publishDir, { recursive: true, force: true }).catch(() => {});
    if (staticPublishDir) await rm(staticPublishDir, { recursive: true, force: true }).catch(() => {});
  }
}

async function readServerState(page) {
  return page.evaluate(async () => {
    const response = await fetch('/_authdoctor/server-state', { cache: 'no-store', credentials: 'same-origin' });
    if (!response.ok) return null;
    const value = await response.json();
    return typeof value.authenticated === 'boolean' ? value.authenticated : null;
  });
}

async function readUiState(page) {
  const value = await page.locator('[data-authdoctor-probe="v1"]').getAttribute('data-authenticated');
  return value === 'true' ? true : value === 'false' ? false : null;
}

async function waitForServerState(page, expected) {
  await page.waitForFunction(async wanted => {
    const response = await fetch('/_authdoctor/server-state', { cache: 'no-store', credentials: 'same-origin' });
    if (!response.ok) return false;
    const state = await response.json();
    return state.authenticated === wanted;
  }, expected, { timeout: maxWaitMs });
}

async function waitForUiState(page, expected) {
  await page.waitForFunction(wanted => {
    return document.querySelector('[data-authdoctor-probe="v1"]')?.getAttribute('data-authenticated') === String(wanted);
  }, expected, { timeout: maxWaitMs });
}

async function postSameOrigin(page, path) {
  const status = await page.evaluate(async endpoint => {
    const response = await fetch(endpoint, { method: 'POST', credentials: 'same-origin', cache: 'no-store' });
    return response.status;
  }, path);
  if (status !== 204) throw new Error('fixture-transition-failed');
}

async function protectedStatus(page) {
  return page.evaluate(async () => {
    const response = await fetch('/_authdoctor/protected-probe', { credentials: 'same-origin', cache: 'no-store' });
    return response.status;
  });
}

process.exitCode = await main();
