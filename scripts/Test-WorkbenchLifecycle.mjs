// Exercise current-result behavior without launching dotnet or a target application.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

class Element {
  constructor(tag='div'){this.tagName=tag;this.children=[];this.value='';this.textContent='';this.hidden=false;this.className='';this.listeners={};}
  append(...items){this.children.push(...items);}
  replaceChildren(...items){this.children=[...items];this.textContent='';}
  get firstChild(){return this.children[0];}
  addEventListener(name,callback){this.listeners[name]=callback;}
  setAttribute(){}
}
const elements=new Map();
const get=id=>{if(!elements.has(id))elements.set(id,new Element());return elements.get(id);};
const context=vm.createContext({document:{getElementById:get,createElement:t=>new Element(t),querySelectorAll:()=>[]},Node:Element,
  location:{hash:'',pathname:'/'},sessionStorage:{getItem:()=>'',setItem(){}},history:{replaceState(){}},
  URLSearchParams,structuredClone,URL,console,setTimeout:()=>{},fetch:async()=>{throw new Error('Invalid target entry');}});
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
run('renderJob({id:"cancelled",state:"cancelled",exitCode:130,report:cancelledReport})');
assert.equal(get('downloads').children.length,3,'Current cancelled audit reports remain downloadable');
assert.ok(get('downloads').children.every(b=>/已取消|Cancelled/.test(b.textContent)));
console.log('PASS: failed submission, filter-after-error, polling failure, history isolation, cancelled audit downloads');
