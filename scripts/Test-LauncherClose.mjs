import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';
class Element{
 constructor(id){this.id=id;this.children=[];this.dataset={};this.value='';this.disabled=false;this.textContent='';}
 append(...items){this.children.push(...items);}replaceChildren(...items){this.children=items;}setAttribute(){}focus(){}showModal(){this.open=true;}close(){this.open=false;}
}
const launcher=fs.readFileSync(new URL('../src/ArchSift.Web/wwwroot/launcher.js',import.meta.url),'utf8').replace(/^applyLocale\(\);refresh\(.*$/m,'applyLocale();');
function harness(mode){
 const elements=new Map(),storage=new Map(),get=id=>{if(!elements.has(id))elements.set(id,new Element(id));return elements.get(id);};let requests=0;
 const context=vm.createContext({document:{documentElement:{},getElementById:get,createElement:tag=>new Element(tag),querySelectorAll:q=>q==='button,input,select,textarea'?[...elements.values()]:[]},
  URLSearchParams,location:{hash:'',pathname:'/',host:'127.0.0.1:12345'},history:{replaceState(){}},sessionStorage:{getItem:key=>storage.get(key)||null,setItem:(key,value)=>storage.set(key,value),removeItem:key=>storage.delete(key)},localStorage:{getItem:()=>null,setItem(){}},
  AbortSignal:{timeout:()=>undefined},setTimeout:callback=>queueMicrotask(callback),setInterval(){},console,
  fetch:async(path,options)=>{if(path.endsWith('/shutdown')){requests++;if(mode==='network')throw new TypeError('Connection failed');return {ok:mode!=='refused',status:mode==='refused'?400:202,text:async()=>JSON.stringify(mode==='refused'?{error:'Finish the active job'}:{accepted:true})};}if(mode==='accepted')throw new TypeError('Endpoint stopped');return {ok:true};}});
 vm.runInContext(fs.readFileSync(new URL('../src/ArchSift.Web/wwwroot/i18n.js',import.meta.url),'utf8'),context);vm.runInContext(launcher,context);
 const run=code=>vm.runInContext(code,context);run('snapshot={profiles:[],installation:{root:"D:/synthetic",selectedVersion:"0.7.0"},toolVersion:"0.7.0",managedCandidate:null};paint();');
 return {get,run,storage,requests:()=>requests};
}
const cancelled=harness('accepted');const cancelling=cancelled.run('closeLauncher()');cancelled.get('confirm-no').onclick();await cancelling;
assert.equal(cancelled.requests(),0);assert.equal(cancelled.get('launcher-close').disabled,false);
for(const mode of ['accepted','refused','network','timeout']){
 const h=harness(mode),closing=h.run('closeLauncher()');h.get('confirm-yes').onclick();await h.run('closeLauncher()');await closing;
 assert.equal(h.requests(),1,'One request despite repeated activation');
 if(mode==='accepted'){assert.equal(h.run('closePhase'),'unreachable');assert.match(h.get('launcher-status').textContent,/cannot confirm process exit/);assert.equal(h.get('launcher-close').disabled,true);h.get('locale').value='zh';h.get('locale').onchange();assert.match(h.get('launcher-status').textContent,/无法确认进程退出/);}
 else{assert.equal(h.run('closePhase'),'idle');assert.equal(h.get('launcher-close').disabled,false);assert.match(h.get('launcher-status').textContent,mode==='timeout'?/timed out/:/failed/);}
}
console.log('PASS: launcher confirmation cancel, one shutdown request, accepted versus endpoint loss, cautious process wording, rejection/network recovery, timeout, en/zh');
