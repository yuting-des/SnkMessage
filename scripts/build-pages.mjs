import {mkdir,copyFile} from 'node:fs/promises';
const destination=new URL('../dist/pages/',import.meta.url);
const files=['index.html','src/app.js','src/ai.js','src/state.js','src/styles.css',...['sparkle','reload','loading','down'].map(name=>`assets/${name}.svg`),'.nojekyll'];
for(const file of files){
 const target=new URL(file,destination);
 await mkdir(new URL('.',target),{recursive:true});
 await copyFile(new URL(`../${file}`,import.meta.url),target);
}
console.log(`GitHub Pages static demo: ${destination.pathname}`);
