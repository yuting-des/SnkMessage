import {cp, mkdir, rm, rename, writeFile} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const project = path.resolve(fileURLToPath(new URL('..', import.meta.url)));
const source = path.join(project, 'node_modules', 'electron', 'dist');
const output = path.join(project, 'dist', 'SnkMessage-win32-x64');
const appDir = path.join(output, 'resources', 'app');

await rm(output, {recursive: true, force: true});
await mkdir(output, {recursive: true});
await cp(source, output, {recursive: true});
await rm(path.join(output, 'resources', 'default_app.asar'), {force: true});
await mkdir(appDir, {recursive: true});

for (const entry of ['index.html', 'src', 'assets', 'desktop']) {
  await cp(path.join(project, entry), path.join(appDir, entry), {recursive: true});
}
await writeFile(path.join(appDir, 'package.json'), JSON.stringify({name:'snk-message',version:'0.2.0',main:'desktop/main.cjs'}, null, 2));
await rename(path.join(output, 'electron.exe'), path.join(output, 'SnkMessage.exe'));
console.log(output);
