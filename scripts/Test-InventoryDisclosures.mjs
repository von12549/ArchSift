import assert from "node:assert/strict";
import fs from "node:fs";
import vm from "node:vm";

let active = null;
class Element {
  constructor(tag = "div") { this.tagName = tag; this.children = []; this.dataset = {}; this.listeners = {}; this.attributes = {}; this.textContent = ""; this.value = ""; this.open = false; this.disabled = false; }
  append(...items) { for (const item of items) { item.parent = this; this.children.push(item); } }
  replaceChildren(...items) { for (const child of this.children) child.parent = null; this.children = []; this.append(...items); }
  setAttribute(name, value) { this.attributes[name] = value; }
  addEventListener(name, callback) { this.listeners[name] = callback; }
  contains(element) { return element === this || this.children.some(child => child.contains(element)); }
  querySelectorAll(selector) { return this.children.flatMap(child => [...(selector === "[data-inventory-focus]" && child.dataset.inventoryFocus ? [child] : []), ...child.querySelectorAll(selector)]); }
  focus() { active = this; }
  get isConnected() { return this.parent?.isConnected ?? this.connected ?? false; }
}
const elements = new Map();
const el = id => { if (!elements.has(id)) { const node = new Element(); node.connected = true; elements.set(id, node); } return elements.get(id); };
const document = { getElementById: el, createElement: tag => new Element(tag), addEventListener() {}, get activeElement() { return active; } };
const context = vm.createContext({ document, console, zhTranslations: {}, t: key => key,
  api: () => new Promise(() => {}), renderResponsibilities() {}, setInterval() {}, clearInterval() {},
  refreshRules() {}, refreshLibrary() {}, showPanel() {}, localizedMessage: value => value });
let script = fs.readFileSync(new URL("../src/ArchSift.Web/wwwroot/test-workbench.js", import.meta.url), "utf8");
script = script.replace(/\}\)\(\);\s*$/, "globalThis.paintInventory=renderInventory;})();");
vm.runInContext(script, context);
const view = {
  configured: true, configPath: "D:/external/config.json", targetRoot: "D:/target",
  testProject: "tests/Architecture.csproj", auditMode: "offline-architecture", originalCaseIds: Array.from({length: 92}, (_, i) => "cs-" + i),
  rules: Array.from({length: 13}, (_, i) => ({path: "D:/rules/rule-" + i + ".json", cases: Array.from({length: i === 12 ? 29 : 31}, (_, n) => "json-" + i + "-" + n), index: i, editable: false})),
  notIncluded: [], policy: {status: "unchanged"}
};
const original = JSON.stringify(view);
const walk = node => [node, ...node.children.flatMap(walk)];
const all = () => walk(el("test-inventory"));
const details = () => all().filter(node => node.tagName === "details" && node.firstChild?.tagName === "summary");
Object.defineProperty(Element.prototype, "firstChild", {get() { return this.children[0]; }});
const jsonDetails = () => all().filter(node => node.tagName === "details" && node.children[0]?.dataset.inventoryFocus?.startsWith("summary:"));
const control = key => all().find(node => node.dataset.inventoryFocus === key);
context.paintInventory(view);
assert.equal(jsonDetails().length, 13);
assert.ok(jsonDetails().every(node => !node.open));
assert.equal(all().filter(node => node.tagName === "li").length, 401);
assert.ok(all().some(node => node.textContent.includes("JSON cases: 401") && node.textContent.includes("C# cases: 92")));
assert.ok(jsonDetails().every(node => !walk(node.children[0]).some(child => child.tagName === "button")));
assert.equal(all().filter(node => node.dataset.inventoryFocus?.startsWith("edit:")).length, 13);
control("expand-json").focus(); control("expand-json").onclick();
assert.ok(jsonDetails().every(node => node.open));
assert.equal(active, control("expand-json"));
const first = jsonDetails()[0]; first.open = false; first.listeners.toggle();
control("edit:" + view.rules[0].path.toLowerCase()).focus();
context.paintInventory(structuredClone(view));
assert.equal(jsonDetails()[0].open, false); assert.equal(jsonDetails()[1].open, true);
assert.equal(active, control("edit:" + view.rules[0].path.toLowerCase()));
context.paintInventory({...view, configPath: view.configPath.toLowerCase(), rules: view.rules.map(rule => ({...rule,path:rule.path.toLowerCase()}))});
assert.equal(jsonDetails()[1].open, true, "Windows canonical path casing preserves state");
control("collapse-json").onclick(); assert.ok(jsonDetails().every(node => !node.open));
assert.equal(JSON.stringify(view), original, "Disclosure must never mutate inventory/execution data");
context.paintInventory({...view, configPath: "D:/external/other.json"});
assert.ok(jsonDetails().every(node => !node.open));
control("expand-json").onclick();
context.paintInventory(view); assert.ok(jsonDetails().every(node => !node.open), "Config switch clears memory");
context.paintInventory({...view, rules: []});
assert.equal(control("expand-json").disabled, true); assert.equal(control("collapse-json").disabled, true);
assert.ok(all().some(node => node.textContent === "No JSON rulesets"));
context.paintInventory({...view, rules: [{...view.rules[0], cases: []}]});
assert.ok(all().some(node => node.textContent === "No enabled cases"));
context.paintInventory({configured: false});
assert.equal(jsonDetails().length, 0);
console.log("Inventory disclosure regression passed: 13 rulesets, 401 JSON + 92 C#; counts, actions, memory, focus, empty/config switch and immutable inventory.");
