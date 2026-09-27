const {app, BrowserWindow, ipcMain, Menu, Tray, nativeImage, screen} = require('electron');
const path = require('node:path');

let mainWindow;
let tray;
let isQuitting = false;

const traySvg = encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32"><rect width="32" height="32" rx="8" fill="#5a2dfc"/><path d="M16 5c.7 5.8 2.2 7.3 8 8-5.8.7-7.3 2.2-8 8-.7-5.8-2.2-7.3-8-8 5.8-.7 7.3-2.2 8-8Z" fill="white"/><circle cx="24" cy="23" r="3" fill="#c4c0fd"/></svg>`);

function showWindow() {
  if (!mainWindow) return;
  if (mainWindow.isMinimized()) mainWindow.restore();
  mainWindow.show();
  mainWindow.focus();
}

function createWindow() {
  const area = screen.getPrimaryDisplay().workAreaSize;
  mainWindow = new BrowserWindow({
    width: Math.min(1180, area.width - 80),
    height: Math.min(820, area.height - 80),
    minWidth: 760,
    minHeight: 620,
    frame: false,
    transparent: false,
    backgroundColor: '#f5f6fa',
    show: false,
    alwaysOnTop: true,
    skipTaskbar: false,
    webPreferences: {
      preload: path.join(__dirname, 'preload.cjs'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true
    }
  });

  mainWindow.loadFile(path.join(__dirname, '..', 'index.html'));
  mainWindow.once('ready-to-show', () => mainWindow.show());
  mainWindow.on('close', event => {
    if (!isQuitting) {
      event.preventDefault();
      mainWindow.hide();
    }
  });
  mainWindow.on('closed', () => { mainWindow = null; });
}

function createTray() {
  const image = nativeImage.createFromDataURL(`data:image/svg+xml;charset=utf-8,${traySvg}`).resize({width: 16, height: 16});
  tray = new Tray(image);
  tray.setToolTip('SnkMessage');
  tray.setContextMenu(Menu.buildFromTemplate([
    {label: '显示 SnkMessage', click: showWindow},
    {label: '保持置顶', type: 'checkbox', checked: true, click: item => mainWindow?.setAlwaysOnTop(item.checked)},
    {type: 'separator'},
    {label: '退出', click: () => { isQuitting = true; app.quit(); }}
  ]));
  tray.on('click', () => mainWindow?.isVisible() ? mainWindow.hide() : showWindow());
  tray.on('double-click', showWindow);
}

app.whenReady().then(() => {
  createWindow();
  createTray();
  app.on('activate', showWindow);
});

app.on('window-all-closed', event => event.preventDefault());
app.on('before-quit', () => { isQuitting = true; });

ipcMain.handle('window:minimize', () => mainWindow?.minimize());
ipcMain.handle('window:hide', () => mainWindow?.hide());
ipcMain.handle('window:toggle-top', () => {
  if (!mainWindow) return false;
  const next = !mainWindow.isAlwaysOnTop();
  mainWindow.setAlwaysOnTop(next);
  return next;
});
ipcMain.handle('window:is-top', () => mainWindow?.isAlwaysOnTop() ?? false);
