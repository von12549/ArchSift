// Test-only preload: unknown endpoints fail. Production driver has no test bypass/input.
import fs from 'node:fs';
import childProcess from 'node:child_process';
import { syncBuiltinESMExports } from 'node:module';
childProcess.spawnSync=()=>{console.error('FORBIDDEN-TRANSPORT');throw new Error('No child execution in driver preparation tests');};
syncBuiltinESMExports();
console.log('SYNTHETIC-TRANSPORT-LOADED');
const r=JSON.parse(fs.readFileSync(new URL('../../docs/releases/0.11.0/trusted-publishing-reference.json',import.meta.url)));
const repository={full_name:'von12549/ArchSift'};
globalThis.fetch=async url=>{
 const p=new URL(url).pathname;let value;
 if(p.endsWith('/commits/main'))value={sha:'a'.repeat(40)};
 else if(/\/commits\/[a-f0-9]{40}\/pulls$/.test(p))value=[{state:'closed',merged_at:'2026-10-25T00:00:00Z',merge_commit_sha:'a'.repeat(40),base:{ref:'main',repo:repository},head:{sha:'d'.repeat(40),repo:repository}}];
 else if(p.endsWith(`/git/commits/${r.sourceCommit}`))value={sha:r.sourceCommit,tree:{sha:r.sourceTree}};
 else if(/\/actions\/runs\/\d+$/.test(p)){const run=r.sourceCi.find(x=>p.endsWith(`/${x.runId}`));if(!run)throw new Error('Unknown fake CI');value={repository,head_repository:repository,path:'.github/workflows/verify.yml',head_sha:run.headSha,event:run.event,status:'completed',conclusion:'success'};}
 else if(/\/actions\/workflows\/[^/]+\/runs$/.test(p)){const file=p.split('/').at(-2),q=new URL(url).searchParams;value={workflow_runs:[{id:1,repository,head_repository:repository,path:`.github/workflows/${file}`,head_sha:q.get('head_sha'),event:q.get('event'),status:'completed',conclusion:'success'}]};}
 else if(p.endsWith('/deployment-branch-policies'))value={total_count:1,branch_policies:[{type:'branch',name:'main'}]};
 else if(p.endsWith('/environments/nuget-release'))value={name:'nuget-release',can_admins_bypass:false,deployment_branch_policy:{protected_branches:false,custom_branch_policies:true},protection_rules:[{type:'required_reviewers',prevent_self_review:false,reviewers:[{type:'User',reviewer:{login:'von12549',id:2306937}}]},{type:'branch_policy'}]};
 else { console.error('FORBIDDEN-TRANSPORT'); throw new Error('No token, asset or push transport exists in this fixture'); }
 return new Response(JSON.stringify(value));
};
