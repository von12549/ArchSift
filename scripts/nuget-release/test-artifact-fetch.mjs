// Test-only preload. The production driver has no fixture switch or transport bypass.
import './test-fetch.mjs';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import childProcess from 'node:child_process';
import {syncBuiltinESMExports} from 'node:module';
const originalRead=fs.readFileSync.bind(fs),metadataFetch=globalThis.fetch;
const fixture=JSON.parse(originalRead(process.env.ARCHSIFT_RELEASE_TEST_FIXTURE));
const replacements=new Map([
 [fileURLToPath(new URL('../../docs/releases/0.11.0/trusted-publishing-reference.json',import.meta.url)),Buffer.from(JSON.stringify(fixture.reference))],
 [fileURLToPath(new URL('../../docs/releases/0.11.0/publishing-activation.json',import.meta.url)),Buffer.from(JSON.stringify(fixture.policy))]
]);
fs.readFileSync=(file,options)=>{
 const name=typeof file==='number'?null:path.resolve(file instanceof URL?fileURLToPath(file):String(file));
 if(!replacements.has(name))return originalRead(file,options);
 const bytes=replacements.get(name),encoding=typeof options==='string'?options:options?.encoding;
 return encoding?bytes.toString(encoding):Buffer.from(bytes);
};
for(const name of ['spawn','spawnSync','exec','execSync','execFile','execFileSync','fork'])childProcess[name]=()=>{console.error('FORBIDDEN-TRANSPORT');throw new Error('No native child execution in input-verification fixtures');};
syncBuiltinESMExports();
const storage='https://archsift-test.blob.core.windows.net/reviewed.zip';
globalThis.fetch=async(url,options)=>{
 const u=new URL(url);
 if((options?.method??'GET')!=='GET'){console.error('FORBIDDEN-TRANSPORT');throw new Error('No writes, login or pushes in input fixtures');}
 if(u.href===storage){console.log('SYNTHETIC-ARTIFACT-BYTES-READ');return new Response(Buffer.from(fixture.archiveBase64,'base64'));}
 if(u.origin!=='https://api.github.com'){console.error('FORBIDDEN-TRANSPORT');throw new Error('No public network transport in input fixtures');}
 if(u.pathname==='/repos/von12549/ArchSift/actions/artifacts/42'){console.log('SYNTHETIC-ARTIFACT-METADATA-READ');return new Response(JSON.stringify(fixture.metadata));}
 if(u.pathname==='/repos/von12549/ArchSift/actions/artifacts/42/zip')return new Response(null,{status:302,headers:{location:storage}});
 return metadataFetch(url,options);
};
