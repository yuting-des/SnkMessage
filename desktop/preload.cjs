const {contextBridge, ipcRenderer} = require('electron');

contextBridge.exposeInMainWorld('snkWindow', Object.freeze({
  minimize: () => ipcRenderer.invoke('window:minimize'),
  hide: () => ipcRenderer.invoke('window:hide'),
  toggleAlwaysOnTop: () => ipcRenderer.invoke('window:toggle-top'),
  isAlwaysOnTop: () => ipcRenderer.invoke('window:is-top')
}));
