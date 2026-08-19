import { app, BrowserWindow, Menu } from 'electron';
import path from 'node:path';
import { startBackend, stopBackend } from './backendProcess';

// No File/Edit/View/Window menu - this is a single-window app with no menu-driven
// commands, and the default template only adds clutter (plus a Ctrl+W(indow)/Cmd+Q an
// accidental keyboard shortcut could trigger).
Menu.setApplicationMenu(null);

let mainWindow: BrowserWindow | null = null;

async function createWindow(apiBaseUrl: string): Promise<void> {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    // Matches theme/tokens.css's --color-background, so the window doesn't flash white
    // while the renderer is still loading.
    backgroundColor: '#0c0a1d',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
      additionalArguments: [`--chiaroscuro-api-base-url=${apiBaseUrl}`],
    },
  });

  const devServerUrl = process.env.CHIAROSCURO_DEV_SERVER_URL;
  if (!app.isPackaged && devServerUrl) {
    await mainWindow.loadURL(devServerUrl);
  } else {
    // Packaged layout: dist/main.js sits next to a sibling renderer/ directory containing
    // the built SPA - see electron-builder.yml's `files` mapping.
    await mainWindow.loadFile(path.join(__dirname, '../renderer/index.html'));
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

app.whenReady().then(async () => {
  const backend = await startBackend(app.isPackaged, process.resourcesPath);
  await createWindow(backend.baseUrl);

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      void createWindow(backend.baseUrl);
    }
  });
});

app.on('window-all-closed', () => {
  stopBackend();
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('before-quit', () => {
  stopBackend();
});
