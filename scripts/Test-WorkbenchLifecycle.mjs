// Exercise current-result behavior without launching dotnet or a target application.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

class Element {
  constructor(tag='div'){this.tagName=tag;this.children=[];this.value='';this.textContent='';this.hidden=false;this.className='';this.listeners={};this.dataset={};}
  append(...items){this.children.push(...items);}
  replaceChildren(...items){this.children=[...items];this.textContent='';}
  get firstChild(){return this.children[0];}
  addEventListener(name,callback){this.listeners[name]=callback;}
  setAttribute(){}
  showModal(){this.open=true;}
  close(){this.open=false;}
  focus(){}
}
const elements=new Map();
const get=id=>{if(!elements.has(id))elements.set(id,new Element());return elements.get(id);};
const context=vm.createContext({document:{documentElement:{},getElementById:get,createElement:t=>new Element(t),querySelectorAll:()=>[]},Node:Element,
  location:{hash:'',pathname:'/'},sessionStorage:{getItem:()=>'',setItem(){}},history:{replaceState(){}},
  localStorage:{getItem:()=>null,setItem(){}},URLSearchParams,structuredClone,URL,console,setTimeout:()=>{},fetch:async()=>{throw new Error('UI target entry does not exist under the target root.');}});
vm.runInContext(fs.readFileSync(new URL('../src/ArchSift.Web/wwwroot/i18n.js',import.meta.url),'utf8'),context);
let script=fs.readFileSync(new URL('../src/ArchSift.Web/wwwroot/app.js',import.meta.url),'utf8');
script=script.replace(/boot\(\);\s*$/, '');
vm.runInContext(script,context);
const run=code=>vm.runInContext(code,context);
run('configuration={schemaVersion:1,target:{root:"D:/fixture"},build:{mode:"existing",configuration:"Debug"},output:{directory:"D:/reports"},rulesets:[]};fillConfig(configuration);');
get('target-kind').value='fixture';
const seed=()=>{run('selectedJob="old";lastReport={projects:[{id:"old.csproj",name:"Old",frameworks:["net10.0"],references:[]}],scope:{unlistedProjects:[]}};renderProjects(lastReport);');get('result-title').textContent='Old partial result';get('downloads').append(new Element('button'));};
seed();await run('start("verify")');
assert.equal(get('project-region').hidden,true);
assert.equal(get('project-region-count').textContent,'');
assert.equal(get('project-count').textContent,'');
assert.equal(get('graph').children.length,0);
assert.equal(get('downloads').children.length,0);
assert.equal(run('lastReport'),null);
assert.notEqual(get('result-title').textContent,'Old partial result');
get('locale').value='zh';await get('locale').onchange();
assert.match(get('result-title').textContent,/配置或提交失败/);
assert.match(get('results').children[0].textContent,/目标根内入口不存在/);
get('locale').value='en';await get('locale').onchange();
assert.match(get('result-title').textContent,/Configuration or submission failed/);
get('project-filter').value='Old';get('project-filter').listeners.input();
assert.equal(get('graph').children.length,0,'Filtering after config rejection must not resurrect old projects');
seed();await run('poll("old")');
assert.equal(get('project-count').textContent,'');assert.equal(run('lastReport'),null);
run('renderJob({id:"history",state:"failed",error:"Previous failure"},true)');
assert.equal(get('downloads').children.length,0);assert.equal(run('lastReport'),null);
const report={execution:'cancelled',compliance:'inconclusive',inputIdentity:{sha256:'0'.repeat(64)},coverage:{projectCount:0,assemblyCount:0},
  buildContext:{binding:'none'},scope:{model:'declared',unlistedProjects:[],externalReferences:[],unresolvedReferences:[]},
  ruleResults:[],findings:[],projects:[],limitations:['User cancelled this run.'],executionErrors:[]};
context.cancelledReport=report;
run('renderJob({id:"publishing",state:"running",report:cancelledReport})');
assert.equal(get('downloads').children.length,0,'A running job must not expose a terminal report during publication');
run('renderJob({id:"cancelled",state:"cancelled",exitCode:130,report:cancelledReport})');
assert.equal(get('downloads').children.length,3,'Current cancelled audit reports remain downloadable');
assert.ok(get('downloads').children.every(b=>/已取消|Cancelled/.test(b.textContent)));
context.fetch=async path=>({ok:true,status:200,text:async()=>JSON.stringify(path.endsWith('/library')?{entries:[],external:[]}:[])});
const beforeLocale=JSON.stringify(report);
get('ruleset-description').value='User policy 原文';
get('locale').value='zh';await get('locale').onchange();
assert.match(get('result-title').textContent,/已取消/);
assert.match(get('status').textContent,/已取消/);
assert.equal(JSON.stringify(report),beforeLocale,'Locale switching must not change report data');
assert.equal(get('ruleset-description').value,'User policy 原文','User-authored policy must not be translated');
get('locale').value='en';await get('locale').onchange();
assert.match(get('result-title').textContent,/Cancelled/);
assert.match(get('status').textContent,/Cancelled/);
const html=fs.readFileSync(new URL('../src/ArchSift.Web/wwwroot/index.html',import.meta.url),'utf8');
const staticKeys=[...html.matchAll(/data-i18n="([^"]+)"/g),...script.matchAll(/\bt\("([^"]+)"\)/g),...script.matchAll(/\baction\("([^"]+)"/g)].map(m=>m[1]);
for(const key of staticKeys)assert.ok(run('Object.hasOwn(zhTranslations,'+JSON.stringify(key)+')'),'Missing Chinese product translation: '+key);
assert.ok(!html.includes('id="verify"'),'Global Verify must be absent');
const cancelledConfirmation=run('confirmAction("Discard this draft?")');get('confirm-no').onclick();assert.equal(await cancelledConfirmation,false);
const acceptedConfirmation=run('confirmAction("Delete synthetic policy?")');get('confirm-yes').onclick();assert.equal(await acceptedConfirmation,true);
assert.equal(get('confirm-dialog').open,false);
console.log('PASS: failed submission, filter-after-error, polling failure, history isolation, cancelled downloads, en/zh state preservation and translation catalog');
