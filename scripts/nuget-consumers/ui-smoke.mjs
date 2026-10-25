import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import { spawn } from 'node:child_process';
import { once } from 'node:events';
import { assertHosted, contained, writeJson } from './model.mjs';
assertHosted(process.env); assert.equal(process.platform,'win32');
const [dotnet,payload,config,root] = process.argv.slice(2); contained(process.env.RUNNER_TEMP,root);
const tokens=[];
const isClosed = port => new Promise(resolve=>{ const client=net.connect({host:'127.0.0.1',port}); client.once('error',()=>{client.destroy();resolve(true);}); client.once('connect',()=>{client.destroy();resolve(false);}); });
for(const mode of ['workbench','launcher']){
  const child=spawn(dotnet,[payload,'workspace','start','--config',config,'--mode',mode,'--no-browser'],{cwd:path.dirname(path.dirname(config)),env:{...process.env,DOTNET_ADD_GLOBAL_TOOLS_TO_PATH:'0'},stdio:['pipe','pipe','pipe'],windowsHide:true});
  const exited=once(child,'exit'); let output='',errors='',address=null,webPid=null;
  child.stderr.on('data',chunk=>{errors+=chunk.toString();});
  child.stdout.on('data',chunk=>{ output+=chunk.toString(); const url=output.match(/^ARCHSIFT_UI=(\S+)/m); if(url)address=new URL(url[1]); const pid=output.match(/^ARCHSIFT_PID=(\d+)/m);if(pid)webPid=Number(pid[1]); });
  const deadline=Date.now()+45000;
  try{
    while(!/^ARCHSIFT_STATE=waiting/m.test(output) && child.exitCode===null && Date.now()<deadline) await new Promise(resolve=>setTimeout(resolve,50));
    assert.ok(address && webPid && /^ARCHSIFT_STATE=waiting/m.test(output),'Owned UI readiness failed');
    assert.equal(address.hostname,'127.0.0.1'); assert.equal(address.protocol,'http:');
    const token=address.hash.slice(9); assert.ok(token.length>20);tokens.push(token);
    const request=async(route,body,status=200,url=address)=>{
      const secret=url.hash.slice(9); const response=await fetch(url.origin+route,{method:body?'POST':'GET',headers:{'X-ArchSift-Token':secret,...(body?{'Content-Type':'application/json'}:{})},body:body?JSON.stringify(body):undefined,signal:AbortSignal.timeout(15000)});
      assert.equal(response.status,status,`UI ${route} status`); const text=await response.text(); return text?JSON.parse(text):null;
    };
    if(mode==='workbench'){
      const state=await request('/api/config'); assert.equal(state.toolVersion,'0.11.0'); assert.equal(path.resolve(state.config.target.root),path.resolve(path.dirname(path.dirname(config))));
      await request('/api/shutdown',{},202);
    }else{
      const state=await request('/api/launcher/state'); assert.ok(state.profiles.length>0);
      const profile=state.profiles[0]; const started=await request('/api/launcher/start',{profileId:profile.entry.profileId,catalogSha256:state.catalogSha256,configSha256:profile.sha256,selectionSha256:state.selectionSha256});
      const nested=new URL(started.workbenchUrl); tokens.push(nested.hash.slice(9));
      const nestedConfig=await request('/api/config',null,200,nested); assert.equal(nestedConfig.toolVersion,'0.11.0');
      await request('/api/launcher/stop',{discardChanges:false});
      await request('/api/launcher/shutdown',{},202);
      assert.equal(await isClosed(Number(nested.port)),true,'Owned nested listener remains');
    }
    const [code]=await Promise.race([exited,new Promise((_,reject)=>setTimeout(()=>reject(new Error('Owned UI did not exit')),20000).unref())]); assert.equal(code,0);
    assert.equal(await isClosed(Number(address.port)),true,'Owned listener remains');
    writeJson(path.join(root,`ui-${mode}.json`),{status:'passed-host-compatibility-smoke',mode,version:'0.11.0',pid:child.pid,ownedWebPid:webPid,closed:true,scope:'Headless machine smoke only, not lifecycle completion or human acceptance'});
  }catch(error){
    // Only the process this helper spawned is signalled; its owned pipe closes its children.
    child.stdin.destroy(); if(child.exitCode===null)child.kill();
    for(const token of tokens){output=output.replaceAll(token,'[redacted]'); errors=errors.replaceAll(token,'[redacted]');}
    // Do not persist readiness lines/fragments even on failures.
    fs.writeFileSync(path.join(root,`ui-${mode}-failure.log`),`${error.message}\n${output.replace(/^ARCHSIFT_UI=.*$/gm,'ARCHSIFT_UI=[redacted]')}\n${errors}`);
    throw new Error(`Published ${mode} smoke failed; retained redacted evidence`);
  }
}
