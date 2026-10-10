// Native synthetic launcher/workbench browser QA. URLs and tokens stay in memory, never in logs or screenshots of browser chrome.
import fs from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import assert from 'node:assert/strict';
const [installRoot,outputRoot,modules,browserChannel]=process.argv.slice(2);
if(!installRoot||!outputRoot||!modules)throw new Error('Supply synthetic install root, external QA directory and bundled Node modules.');
await fs.mkdir(outputRoot,{recursive:true});
const installation=JSON.parse(await fs.readFile(path.join(installRoot,'install.json'),'utf8'));
const version=installation.versions.find(v=>v.version===installation.selectedVersion);
const {chromium}=await import(pathToFileURL(path.join(modules,'playwright','index.mjs')).href);
const processOwned=spawn(path.join(version.directory,'web','ArchSift.Web.exe'),['--launcher',installRoot],{windowsHide:true,stdio:['pipe','pipe','pipe'],env:{...process.env,DOTNET_CLI_HOME:path.join(outputRoot,'cli-home'),DOTNET_ADD_GLOBAL_TOOLS_TO_PATH:'0',ARCHSIFT_UI_PARENT_CONTROL:'stdin-v1'}});
let buffer='',readyResolve,readyReject;
const ready=new Promise((resolve,reject)=>{readyResolve=resolve;readyReject=reject;});
processOwned.stdout.on('data',chunk=>{buffer+=chunk.toString('utf8');let index;while((index=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,index).trim();buffer=buffer.slice(index+1);if(line.startsWith('ARCHSIFT_UI='))readyResolve(line.slice(12));}});
processOwned.stderr.on('data',()=>{});
processOwned.on('error',()=>readyReject(new Error('Native launcher did not start.')));
processOwned.on('exit',()=>readyReject(new Error('Native launcher exited before ready.')));
let browser;
const checks=[],screens=[];
try{
 const address=await Promise.race([ready,new Promise((_,reject)=>setTimeout(()=>reject(new Error('Native readiness timeout.')),30000))]);
 browser=await chromium.launch({headless:true,...(browserChannel?{channel:browserChannel}:{})});
 const context=await browser.newContext({viewport:{width:1440,height:1000}});const page=await context.newPage();
 const errors=[];page.on('pageerror',()=>errors.push('Browser script error'));
 await page.goto(address);await page.waitForSelector('#managed-candidate button');
 async function capture(name,target=page){await target.screenshot({path:path.join(outputRoot,name),fullPage:true});screens.push(name);const noOverflow=await target.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth);assert.ok(noOverflow,'Horizontal overflow: '+name);}
 await capture('launcher-empty-en-desktop.png');
 await page.locator('#managed-candidate button').click();await page.locator('#confirm-yes').click();await page.waitForSelector('#profiles input[type=radio]');
 const external=path.join(installRoot,'config','external-copy.json');await fs.copyFile(installation.configPath,external);
 await page.locator('#profile-name').fill('default.json');await page.locator('#profile-path').fill(external);await page.locator('#register-form button').click();await page.locator('#confirm-yes').click();await page.waitForFunction(()=>document.querySelectorAll('#profiles input[type=radio]').length===2);
 await capture('launcher-two-profiles-en-desktop.png');await page.locator('#locale').selectOption('zh');await capture('launcher-two-profiles-zh-desktop.png');await page.setViewportSize({width:480,height:900});await capture('launcher-two-profiles-zh-narrow.png');await page.locator('#locale').selectOption('en');await capture('launcher-two-profiles-en-narrow.png');
 checks.push('two same-name distinct paths and per-resource protection; desktop/480px; en/zh; no horizontal overflow');
 await page.setViewportSize({width:1440,height:1000});await page.locator('#profiles input[type=radio]').first().check();
 const popupPromise=context.waitForEvent('page');await page.locator('#start-profile').click();const workbench=await popupPromise;await workbench.waitForSelector('#product-version');await workbench.waitForFunction(()=>document.querySelector('#product-version').textContent.includes('0.6.0'));
 assert.equal(await workbench.locator('#session-profile-name').textContent(),'default.json');assert.ok((await workbench.locator('#session-profile-path').textContent()).includes('default.json'));
 await capture('workbench-profile-en-desktop.png',workbench);await workbench.locator('#locale').selectOption('zh');await capture('workbench-profile-zh-desktop.png',workbench);await workbench.setViewportSize({width:480,height:900});await capture('workbench-profile-zh-narrow.png',workbench);
 const actualOutput=await workbench.locator('#output').inputValue();await workbench.locator('#output').fill(actualOutput+'-unsaved');
 await workbench.locator('#shutdown').click();await workbench.locator('#confirm-dialog').waitFor({state:'visible'});await workbench.locator('#confirm-no').click();
 assert.ok(await workbench.locator('#shutdown').isEnabled(),'Cancelling discard keeps workbench alive');checks.push('current product/profile identity; edited config shutdown confirmation cancelled without losing session');
 await workbench.locator('#shutdown').click();await workbench.locator('#confirm-yes').click();await page.waitForFunction(()=>document.querySelector('#active-session').hidden,{timeout:15000});
 await fs.writeFile(external,'{}');await page.locator('#refresh').click();await page.waitForFunction(()=>document.querySelector('#profiles').textContent.includes('invalid'));await capture('launcher-invalid-profile-en-desktop.png');
 assert.equal(errors.length,0);checks.push('safe workbench exit reflected by launcher; damaged external config visible; no browser script errors');
 await page.locator('#launcher-close').click();
 await fs.writeFile(path.join(outputRoot,'visual-result.json'),JSON.stringify({status:'pass',version:'0.6.0',checks,screens,scope:'synthetic native package; not independent operator attestation; no target analysis/build or entry-point execution'},null,2));
 console.log('PASS: native launcher/profile/browser QA; screenshots='+screens.length);
}finally{
 if(browser)await browser.close();
 if(processOwned.exitCode===null){processOwned.stdin.end();await Promise.race([once(processOwned,'exit'),new Promise(resolve=>setTimeout(resolve,12000))]);}
 if(processOwned.exitCode===null)throw new Error('Owned native launcher did not exit; preserve exact PID for review.');
}
