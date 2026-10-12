// Delegated machine QA against a copied real IFX library. Human UI acceptance remains independent.
import fs from 'node:fs/promises';
import path from 'node:path';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {pathToFileURL} from 'node:url';
import assert from 'node:assert/strict';
const [payload,inputConfigPath,output,modules,webExecutable,scope]=process.argv.slice(2);
const copiesOnly=scope==='copies-only';
const config=JSON.parse(await fs.readFile(inputConfigPath,'utf8'));
const sourceBound=Boolean(config.build.assemblyManifest);
assert.ok(path.resolve(config.rulesDirectory).toLowerCase().startsWith('d:\\archsift-lab\\'));
await fs.mkdir(output,{recursive:true});
await fs.cp(config.rulesDirectory,path.join(output,'rules'),{recursive:true,errorOnExist:true,force:false});
config.rulesDirectory=path.join(output,'rules');config.output.directory=path.join(output,'reports');
const configPath=path.join(output,'config.json');await fs.writeFile(configPath,JSON.stringify(config));
const originalFile=path.join(config.rulesDirectory,'ifx-compiled-dependencies-candidate-v1.json');
const originalChainFile=path.join(config.rulesDirectory,'chains','ifx-04-class-A-test.json');
const sourceBytes=await fs.readFile(originalFile),chainBytes=await fs.readFile(originalChainFile);
const sourceSet=JSON.parse(sourceBytes),sourceChain=JSON.parse(chainBytes);
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const owned=spawn(webExecutable||path.join(payload,'web','ArchSift.Web.exe'),['--config',configPath],{windowsHide:true,stdio:['pipe','pipe','pipe'],env:{...process.env,DOTNET_CLI_HOME:path.join(output,'cli-home'),DOTNET_ADD_GLOBAL_TOOLS_TO_PATH:'0',ARCHSIFT_UI_PARENT_CONTROL:'stdin-v1'}});
let buffer='',resolveReady,rejectReady;
const ready=new Promise((resolve,reject)=>{resolveReady=resolve;rejectReady=reject;});
owned.stdout.on('data',chunk=>{buffer+=chunk.toString();let n;while((n=buffer.indexOf('\n'))>=0){const line=buffer.slice(0,n).trim();buffer=buffer.slice(n+1);if(line.startsWith('ARCHSIFT_UI='))resolveReady(line.slice(12));}});
owned.stderr.on('data',()=>{});owned.on('error',()=>rejectReady(new Error('Owned native Web startup failed.')));
owned.on('exit',()=>rejectReady(new Error('Owned native Web exited before readiness.')));
let browser,page,base,token,report;
const checks=[],screens=[];
async function api(endpoint,body){const r=await fetch(base+'/api/'+endpoint,{method:body===undefined?'GET':'POST',headers:{'X-ArchSift-Token':token,'Content-Type':'application/json'},body:body===undefined?undefined:JSON.stringify(body)});const text=await r.text();assert.ok(r.ok,'API operation failed: '+endpoint+' status '+r.status+' '+text.replaceAll(token,'[redacted]'));return text?JSON.parse(text):null;}
async function bytes(endpoint){const r=await fetch(base+'/api/'+endpoint,{headers:{'X-ArchSift-Token':token}});assert.ok(r.ok);return Buffer.from(await r.arrayBuffer());}
try {
 const address=await Promise.race([ready,new Promise((_,reject)=>setTimeout(()=>reject(new Error('Readiness timeout.')),30000))]);
 const url=new URL(address);base=url.origin;token=new URLSearchParams(url.hash.slice(1)).get('session');
 const {chromium}=await import(pathToFileURL(path.join(modules,'playwright','index.mjs')).href);
 browser=await chromium.launch({headless:true,channel:'msedge'});
 page=await browser.newPage({viewport:{width:1440,height:1000}});const errors=[];
 page.on('pageerror',()=>errors.push('pageerror'));
 await page.goto(address);await page.waitForSelector('#product-version');await page.locator('#locale').selectOption('en');
 async function capture(name){await page.evaluate(()=>scrollTo(0,scrollY));await page.screenshot({path:path.join(output,name)});const overflow=await page.evaluate(()=>({width:innerWidth,scrollWidth:document.documentElement.scrollWidth,scrollX,items:[...document.querySelectorAll('body *')].filter(e=>e.getBoundingClientRect().right+scrollX>innerWidth).map(e=>({tag:e.tagName,id:e.id,className:e.className,right:e.getBoundingClientRect().right+scrollX,width:e.getBoundingClientRect().width,minWidth:getComputedStyle(e).minWidth,grid:getComputedStyle(e).gridTemplateColumns,text:e.textContent.slice(0,100)})).slice(0,30)}));if(overflow.scrollWidth>overflow.width)await fs.writeFile(path.join(output,'overflow-'+name+'.json'),JSON.stringify(overflow,null,2));assert.ok(overflow.scrollWidth<=overflow.width,'Horizontal overflow: '+name);screens.push(name);}
 const entry=(await api('library')).entries.find(e=>e.rulesetId===sourceSet.id);assert.ok(entry);
 await page.locator('[data-panel=rulesets]').click();
 const card=page.locator('#library-cards article').filter({has:page.locator('h3').filter({hasText:sourceSet.id})});
 await card.getByRole('button',{name:'Create From',exact:true}).click();
 await page.waitForFunction(()=>!document.querySelector('#editor-panel').hidden&&document.querySelector('#ruleset-id').value==='');
 assert.equal(await page.locator('#ruleset-id').inputValue(),'');assert.equal(await page.locator('#filename').inputValue(),'');
 assert.equal(await page.locator('#rule-list details.editor-rule').count(),406);
 await page.locator('#ruleset-id').fill('ifx-b-native-review-copy');
 let createRequests=0;const countCreates=request=>{if(new URL(request.url()).pathname.startsWith('/api/library/create/'))createRequests++;};page.on('request',countCreates);
 for(const lang of ['en','zh']){
  await page.locator('#locale').selectOption(lang);
  for(const value of ['', '   ']){
   await page.locator('#filename').fill(value);await page.locator('#save-rule').click();
   await page.waitForFunction(message=>document.querySelector('#editor-message').textContent===message,lang==='en'?'Enter a JSON filename before saving.':'保存前请填写 JSON 文件名。');
   assert.equal(await page.locator('#ruleset-id').inputValue(),'ifx-b-native-review-copy');assert.ok(await page.locator('#save-rule').isEnabled());assert.ok(await page.locator('#editor-panel').isVisible());assert.equal(createRequests,0);
  }
 }
 page.off('request',countCreates);await page.locator('#locale').selectOption('en');await page.locator('#filename').fill('ifx-b-native-review-copy.json');
 checks.push('Blank/whitespace copy filenames show precise en/zh validation, keep the draft and make no create request; valid save recovers.');
 await page.locator('#ruleset-version').fill('0.7-machine-review');await page.locator('#rule-filter').fill('b03');
 await page.locator('#editor-panel').evaluate(el=>el.scrollIntoView({block:'start'}));await capture('ifx-copy-en-desktop.png');
 await page.locator('#locale').selectOption('zh');await page.setViewportSize({width:480,height:900});await page.locator('#editor-panel').evaluate(el=>el.scrollIntoView({block:'start'}));await capture('ifx-copy-zh-narrow.png');
 await page.locator('#locale').selectOption('en');await page.setViewportSize({width:1440,height:1000});
 await card.getByRole('button',{name:'Create From',exact:true}).click();await page.locator('#confirm-dialog').waitFor({state:'visible'});await page.locator('#confirm-no').click();
 assert.equal(await page.locator('#ruleset-id').inputValue(),'ifx-b-native-review-copy');
 await page.locator('#save-rule').click();await page.waitForFunction(()=>document.querySelector('#editor-panel').hidden);
 const copied=(await api('library')).entries.find(e=>e.rulesetId==='ifx-b-native-review-copy');assert.ok(copied);assert.notEqual(copied.entryId,entry.entryId);
 assert.equal(hash(await fs.readFile(originalFile)),hash(sourceBytes));checks.push('Actual 406-rule saved-byte copy, new ID/file/version, cancelled replacement preserves draft and source bytes.');
 const duplicateSet={...structuredClone(sourceSet),id:'ifx-b-native-double'};
 const request=()=>fetch(base+'/api/library/create/ifx-b-native-double.json',{method:'POST',headers:{'X-ArchSift-Token':token,'Content-Type':'application/json'},body:JSON.stringify(duplicateSet)});
 const duplicates=await Promise.all([request(),request()]);assert.equal(duplicates.filter(r=>r.ok).length,1);assert.equal(duplicates.filter(r=>r.status===400).length,1);
 checks.push('Real copied library concurrent create accepts one request and rejects one conflict.');
 await page.reload();await page.waitForFunction(()=>document.querySelector('#status').textContent==='Configure a target to begin.');await page.locator('#locale').selectOption('en');await page.locator('[data-panel=chains]').click();
 const chainCard=page.locator('#chain-cards article').filter({hasText:sourceChain.id});await chainCard.getByRole('button',{name:'Create From',exact:true}).click();
 await page.waitForFunction(count=>!document.querySelector('#chain-editor').hidden&&document.querySelector('#chain-id').value===''&&!document.querySelector('#chain-id').disabled&&document.querySelector('#chain-entries').children.length===count,sourceChain.entries.length);
 assert.equal(await page.locator('#chain-id').inputValue(),'');await page.locator('#chain-id').fill('ifx-A-native-review-copy');
 await page.locator('#chain-close').click();await page.locator('#confirm-dialog').waitFor({state:'visible'});await page.locator('#confirm-no').click();assert.equal(await page.locator('#chain-id').inputValue(),'ifx-A-native-review-copy');assert.ok(await page.locator('#chain-id').isVisible());
 await page.locator('#chain-entries').getByRole('button',{name:'Move down'}).first().click();await page.locator('#chain-save').click();
 await page.waitForFunction(()=>document.querySelector('#chain-message').textContent==='Chain saved.');
 const chainCopy=await api('chains/ifx-A-native-review-copy');assert.deepEqual(chainCopy.entries,[sourceChain.entries[1],sourceChain.entries[0],...sourceChain.entries.slice(2)]);
 assert.equal(hash(await fs.readFile(originalChainFile)),hash(chainBytes));await capture('ifx-chain-copy-en-desktop.png');checks.push('Actual A chain copy reorders stable entryIds and preserves the original chain.');
 await page.locator('#chain-close').click();await page.locator('#chain-editor').waitFor({state:'hidden'});checks.push('Visible chain edit panel; cancelled close preserves the draft, clean close hides it.');
 if(!copiesOnly){
 await page.locator('[data-panel=rulesets]').click();await page.locator('#library-cards article').filter({hasText:'ifx-b-native-review-copy'}).getByRole('button',{name:'Verify',exact:true}).click();
 await page.waitForSelector('#results .result-table',{timeout:600000});assert.equal(await page.locator('#results .result-table tr').count(),407);
 const job=(await api('jobs')).find(j=>j.operation==='ruleset');assert.ok(job);
 const reportBytes=await bytes('jobs/'+job.id+'/report/json');report=JSON.parse(reportBytes);
 assert.equal(report.coverage.projectCount,105);assert.equal(report.coverage.assemblyCount,105);assert.equal(report.coverage.sourceBound,sourceBound);
 assert.ok(report.ruleResults.every(r=>r.status===(sourceBound?'pass':'inconclusive')&&r.matched>0));
 assert.equal(report.executionErrors.length,0);
 if(sourceBound){assert.equal(report.buildContext.assemblyBindingMode,'project-contexts');assert.equal(report.buildContext.assemblyContextCount,105);assert.equal(report.buildContext.evidenceOrigin,'external-reviewed');}
 const controls=page.locator('#results .rule-sort-controls');await controls.getByRole('button',{name:'Rule',exact:true}).press('Enter');await controls.getByRole('button',{name:'Rule ↑',exact:true}).click();
 assert.equal(await page.locator('#results .result-table tr').nth(1).locator('td').first().textContent(),report.ruleResults.map(r=>r.ruleId).sort().reverse()[0]);
 assert.equal(await page.locator('#results .result-table th').first().getAttribute('aria-sort'),'descending');await controls.getByRole('button',{name:'Result',exact:true}).click();
 await capture('ifx-results-en-desktop.png');await page.locator('#locale').selectOption('zh');await page.setViewportSize({width:480,height:900});await page.locator('#results .policy-region').evaluate(el=>el.scrollIntoView({block:'start'}));await capture('ifx-results-zh-narrow.png');
 assert.equal(hash(await bytes('jobs/'+job.id+'/report/json')),hash(reportBytes));checks.push('Actual 105-DLL / 406-match '+(sourceBound?'source-bound pass':'inconclusive')+' report; Rule/Result sorting, keyboard/aria, desktop/480px en/zh, immutable download.');
 await page.locator('#locale').selectOption('en');await page.setViewportSize({width:1440,height:1000});await page.locator('[data-panel=chains]').click();
 await page.locator('#chain-cards article').filter({hasText:'ifx-A-native-review-copy'}).getByRole('button',{name:'Verify Chain',exact:true}).click();
 await page.waitForFunction(()=>document.querySelectorAll('#results .rule-sort-controls').length===4,null,{timeout:600000});
 const child=page.locator('#results details').filter({has:page.locator('.rule-sort-controls')}).first();await child.locator('summary').click();await child.locator('.rule-sort-controls').getByRole('button',{name:'Rule',exact:true}).click();await capture('ifx-A-child-sort-en-desktop.png');
 checks.push('Actual copied A chain four independent child sorting controls.');
 }
 assert.equal(hash(await fs.readFile(originalFile)),hash(sourceBytes));assert.equal(hash(await fs.readFile(originalChainFile)),hash(chainBytes));assert.equal(errors.length,0);
 const exited=once(owned,'exit');await api('shutdown',{discardChanges:true});await Promise.race([exited,new Promise((_,reject)=>setTimeout(()=>reject(new Error('Owned shutdown timeout.')),10000))]);assert.equal(owned.exitCode,0);
 await assert.rejects(fetch(base+'/api/session',{headers:{'X-ArchSift-Token':token}}));
 await fs.writeFile(path.join(output,'result.json'),JSON.stringify({status:'pass',version:'0.7.0',scope:copiesOnly?'Copy/editor development regression only; excludes report and current-source conformance gates':'Delegated machine browser regression; independent human UI acceptance remains pending',sourceBound:copiesOnly?null:sourceBound,reportCoverage:report?.coverage??null,reportRuleCounts:report?{pass:report.ruleResults.filter(r=>r.status==='pass').length,inconclusive:report.ruleResults.filter(r=>r.status==='inconclusive').length}:null,checks,screens,sourceRulesSha256:hash(sourceBytes),sourceChainSha256:hash(chainBytes),ownedPid:owned.pid,ownedExitCode:owned.exitCode,endpointClosed:true},null,2));
 console.log('PASS actual IFX machine browser: '+checks.length+' groups, '+screens.length+' screenshots; owned exit 0.');
}catch(error){const message=String(error.stack||error).replaceAll(token||'[none]','[redacted]');await fs.writeFile(path.join(output,'failure.txt'),message);if(page){await page.screenshot({path:path.join(output,'failure.png')}).catch(()=>{});const state=await page.evaluate(()=>({status:document.querySelector('#status').textContent,chainOpen:document.querySelector('#chain-editor').open,chainOpenAttribute:document.querySelector('#chain-editor').getAttribute('open'),chainId:document.querySelector('#chain-id').value,chainDisabled:document.querySelector('#chain-id').disabled,chainMessage:document.querySelector('#chain-message').textContent,chainEntries:document.querySelector('#chain-entries').children.length,chainMarkup:document.querySelector('#chain-editor').outerHTML,chainGeometry:[...document.querySelector('#chain-id').closest('#chain-editor').querySelectorAll('summary,input')].map(e=>({id:e.id,tag:e.tagName,display:getComputedStyle(e).display,visibility:getComputedStyle(e).visibility,rect:{width:e.getBoundingClientRect().width,height:e.getBoundingClientRect().height},visible:e.checkVisibility()})),confirmOpen:document.querySelector('#confirm-dialog').open,confirmText:document.querySelector('#confirm-message').textContent,visiblePanel:[...document.querySelectorAll('.tab-panel')].filter(p=>!p.hidden).map(p=>p.id)})).catch(()=>({}));state.browserVersion=browser?.version();await fs.writeFile(path.join(output,'failure-state.json'),JSON.stringify(state,null,2));}console.error(message);process.exitCode=1;}
finally {if(browser)await browser.close();if(owned.exitCode===null){owned.stdin.end();await Promise.race([once(owned,'exit'),new Promise(resolve=>setTimeout(resolve,10000))]);}if(owned.exitCode===null){owned.kill();throw new Error('Owned native Web required forced termination; retain evidence.');}}
