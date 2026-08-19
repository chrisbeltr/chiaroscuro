import { spawn, type ChildProcess } from 'node:child_process';
import { createServer } from 'node:net';
import path from 'node:path';
import treeKill from 'tree-kill';

// Matches src/Chiaroscuro.Api/Properties/launchSettings.json's fixed dev port - in dev, the
// backend is expected to already be running via `dotnet watch`/`dotnet run` against that
// profile, so this only health-checks it rather than spawning anything.
const DEV_BACKEND_URL = 'http://127.0.0.1:5259';
const HEALTH_CHECK_INTERVAL_MS = 150;
const HEALTH_CHECK_TIMEOUT_MS = 15_000;

let backendProcess: ChildProcess | null = null;

export interface BackendHandle {
  baseUrl: string;
}

/**
 * In a packaged app, spawns the self-contained Chiaroscuro.Api binary bundled under
 * resources/backend/<rid>/ as a sidecar process on a freshly picked local port, and waits
 * for it to answer /health before resolving - so the renderer never races a not-yet-ready
 * backend. In dev, the backend is assumed to already be running (see DEV_BACKEND_URL above).
 */
export async function startBackend(isPackaged: boolean, resourcesPath: string): Promise<BackendHandle> {
  if (!isPackaged) {
    await waitForHealthy(DEV_BACKEND_URL);
    return { baseUrl: DEV_BACKEND_URL };
  }

  const port = await findFreePort();
  const baseUrl = `http://127.0.0.1:${port}`;
  const executablePath = resolveBackendExecutable(resourcesPath);

  backendProcess = spawn(executablePath, [], {
    env: { ...process.env, ASPNETCORE_URLS: baseUrl, ASPNETCORE_ENVIRONMENT: 'Production' },
    windowsHide: true,
    stdio: ['ignore', 'pipe', 'pipe'],
  });

  backendProcess.stdout?.on('data', (chunk: Buffer) => console.log(`[backend] ${chunk.toString().trimEnd()}`));
  backendProcess.stderr?.on('data', (chunk: Buffer) => console.error(`[backend] ${chunk.toString().trimEnd()}`));
  backendProcess.on('exit', (code) => console.log(`[backend] exited with code ${code}`));

  await waitForHealthy(baseUrl);
  return { baseUrl };
}

/**
 * Tree-kills the spawned sidecar (rather than a plain child.kill()) so a
 * PublishSingleFile self-extraction child process never gets orphaned. The API is fully
 * stateless - nothing to flush - so there's no need for a graceful HTTP shutdown first.
 * A no-op in dev mode, since nothing was spawned there.
 */
export function stopBackend(): void {
  if (backendProcess?.pid) {
    treeKill(backendProcess.pid);
    backendProcess = null;
  }
}

function resolveBackendExecutable(resourcesPath: string): string {
  const rid = resolveRuntimeIdentifier();
  const executableName = process.platform === 'win32' ? 'Chiaroscuro.Api.exe' : 'Chiaroscuro.Api';
  return path.join(resourcesPath, 'backend', rid, executableName);
}

function resolveRuntimeIdentifier(): string {
  if (process.platform === 'win32') {
    return 'win-x64';
  }
  if (process.platform === 'linux') {
    return 'linux-x64';
  }
  if (process.platform === 'darwin') {
    return process.arch === 'arm64' ? 'osx-arm64' : 'osx-x64';
  }
  throw new Error(`Unsupported platform: ${process.platform}`);
}

function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = createServer();
    server.unref();
    server.on('error', reject);
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      if (address && typeof address === 'object') {
        const { port } = address;
        server.close(() => resolve(port));
      } else {
        server.close();
        reject(new Error('Could not determine a free port.'));
      }
    });
  });
}

async function waitForHealthy(baseUrl: string): Promise<void> {
  const deadline = Date.now() + HEALTH_CHECK_TIMEOUT_MS;

  while (Date.now() < deadline) {
    try {
      const response = await fetch(`${baseUrl}/health`);
      if (response.ok) {
        return;
      }
    } catch {
      // Not up yet (connection refused, still starting) - keep polling until the deadline.
    }
    await new Promise((resolve) => setTimeout(resolve, HEALTH_CHECK_INTERVAL_MS));
  }

  throw new Error(`Backend at ${baseUrl} did not become healthy within ${HEALTH_CHECK_TIMEOUT_MS}ms.`);
}
