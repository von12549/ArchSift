"use strict";
const byId = id => document.getElementById(id);
let csrf = "";
let rules = [];
let displayedRun = null;
let lastCurrentId = null;
let busy = false;
const terminal = new Set(["completed", "failed", "cancelled"]);
const short = value => value ? String(value).slice(0, 12) + "…" : "—";

function message(value, error = false) {
  const node = byId("message");
  node.textContent = value;
  node.classList.toggle("error", error);
  node.hidden = !value;
}
async function api(path, method = "GET", data) {
  const response = await fetch(path, {
    method,
    headers: method === "POST" ? {
      "Content-Type": "application/json", "X-ArchSift-Session": csrf
    } : {},
    body: data === undefined ? undefined : JSON.stringify(data),
    cache: "no-store"
  });
  let payload = null;
  try { payload = await response.json(); } catch { /* empty response */ }
  if (!response.ok) throw new Error(payload?.error || "请求失败（" + response.status + "）");
  return payload;
}
function phaseText(phase) {
  return ({
    queued: "排队", preflight: "准备", restore: "恢复依赖",
    build: "编译", test: "测试", evaluate: "评估",
    completed: "完成", failed: "失败", cancelled: "已取消"
  })[phase] || phase || "—";
}
function freshnessText(value) {
  return ({ current: "当前输入", stale: "输入已变更", historical: "历史记录" })[value] || "—";
}
function diagnosticText(value) {
  if (value === "The operation was canceled.") return "运行已取消。";
  return value;
}
function reasonText(value) {
  return ({
    allow: "允许", block: "阻断", passed: "规则通过",
    cancelled: "运行取消", violation: "发现违规", pass: "规则通过",
    "invalid-input": "输入无效", failed: "运行失败",
    "source-unbound": "源码关联不足"
  })[value] || value || "—";
}
function renderFindings(report) {
  const box = byId("findings");
  box.replaceChildren();
  if (!report) return;
  const heading = document.createElement("h3");
  heading.textContent = "发现 " + (report.findings?.length || 0) + " 项";
  box.append(heading);
  const list = document.createElement("ul");
  for (const finding of (report.findings || []).slice(0, 30)) {
    const item = document.createElement("li");
    item.textContent = finding.ruleId + " · " + finding.subject + " — " + finding.message;
    list.append(item);
  }
  if (report.findings?.length > 30) {
    const item = document.createElement("li");
    item.textContent = "另有 " + (report.findings.length - 30) + " 项，请查看 JSON 报告。";
    list.append(item);
  }
  if (list.childElementCount) box.append(list);
}
async function showRun(task, current) {
  displayedRun = task?.runId || null;
  byId("gate-title").textContent = task
    ? (task.exitCode === 0 ? (task.freshness === "current" ? "门禁通过" :
       "历史门禁通过 · 已过期") :
       task.phase === "completed" ? "门禁未通过" : phaseText(task.phase))
    : "尚未运行";
  const fresh = byId("freshness");
  fresh.textContent = task ? freshnessText(task.freshness) : "尚无结果";
  fresh.classList.toggle("stale", task?.freshness === "stale");
  byId("gate-description").textContent = diagnosticText(task?.diagnostic) ||
    (task?.freshness === "stale" ? "源码、规则或工具已变化；旧结论不可代表当前输入。" :
     task ? "此运行的状态与材料来自同一个 worktree。" :
     "运行日常检查，生成与当前源码绑定的门禁结论。");
  byId("run-id").textContent = task?.runId || "—";
  byId("phase").textContent = task
    ? phaseText(task.phase) + " / " + (task.evidenceGrade === "full" ? "完整" : "日常")
    : "—";
  byId("coverage").textContent = "—";
  byId("decision").textContent = task?.exitCode === null || task?.exitCode === undefined
    ? "等待结果" : "退出码 " + task.exitCode;
  byId("cancel").disabled = !task || task.runId !== current?.runId ||
    terminal.has(task.phase);
  byId("findings").replaceChildren();
  byId("report-link").hidden = true;
  byId("manifest-link").hidden = true;
  if (!task) { byId("evidence").textContent = "尚无运行材料。"; return; }
  byId("evidence").textContent = "材料状态：" + task.materialsState +
    (task.archivePath ? "；归档：" + task.archivePath : "");
  if (task.resultPath && task.materialsState === "available") {
    try {
      const report = await api("/api/report/" + encodeURIComponent(task.runId));
      if (displayedRun !== task.runId) return;
      byId("coverage").textContent = report.coverage?.executedRules?.length + " / " +
        report.coverage?.requiredRules?.length + (report.coverage?.complete ? " 完整" : " 不完整");
      byId("decision").textContent = reasonText(report.gate?.decision) + " · " +
        reasonText(report.gate?.reason) + " · 退出码 " + report.gate?.exitCode;
      renderFindings(report);
      byId("report-link").href = "/api/report/" + encodeURIComponent(task.runId);
      byId("report-link").hidden = false;
    } catch (error) { message(error.message, true); }
  }
  if (task.evidenceGrade === "full") {
    try {
      const evidence = await api("/api/evidence/" + encodeURIComponent(task.runId));
      if (displayedRun === task.runId)
        byId("evidence").textContent = "完整证据：" +
          (evidence.fullManifestAvailable ? "manifest 可用" : evidence.materialsState) +
          (evidence.archivePath ? "；归档：" + evidence.archivePath : "");
      if (displayedRun === task.runId && evidence.fullManifestAvailable) {
        byId("manifest-link").href = "/api/evidence/" + encodeURIComponent(task.runId) + "/manifest";
        byId("manifest-link").hidden = false;
      }
    } catch (error) { message(error.message, true); }
  }
}
async function refresh() {
  if (busy) return;
  busy = true;
  try {
    const state = await api("/api/state");
    byId("connection").textContent = "本机连接 · " + state.worktreeId.slice(0, 12);
    byId("target").textContent = state.target;
    byId("worktree").textContent = state.worktree;
    byId("version").textContent = state.version + " / " + state.configuration;
    byId("rules-hash").textContent = short(state.rulesSha256);
    const history = byId("history");
    history.replaceChildren();
    for (const task of state.history) {
      const button = document.createElement("button");
      button.type = "button";
      button.classList.toggle("active", task.runId === displayedRun);
      const title = document.createElement("span");
      title.textContent = phaseText(task.phase) + " · " +
        (task.evidenceGrade === "full" ? "完整" : "日常") + " · " + short(task.runId);
      const detail = document.createElement("small");
      detail.textContent = new Date(task.startedUtc).toLocaleString("zh-CN") +
        " · " + freshnessText(task.freshness);
      button.append(title, detail);
      button.addEventListener("click", () => {
        for (const sibling of history.querySelectorAll("button"))
          sibling.classList.remove("active");
        button.classList.add("active");
        showRun(task, state.current);
      });
      history.append(button);
    }
    if (!state.history.length) history.textContent = "尚无记录。";
    const followCurrent = displayedRun === null || displayedRun === lastCurrentId;
    const selected = state.history.find(task => task.runId === displayedRun);
    lastCurrentId = state.current?.runId || null;
    await showRun((followCurrent ? state.current : selected) || state.current, state.current);
  } catch (error) {
    byId("connection").textContent = "连接失败";
    message(error.message, true);
  } finally { busy = false; }
}
async function loadRules() {
  rules = await api("/api/rules");
  const select = byId("rules-select");
  const previous = select.value;
  select.replaceChildren();
  for (const rule of rules) {
    const option = document.createElement("option");
    option.value = rule.name;
    option.textContent = rule.name;
    select.append(option);
  }
  if (rules.some(item => item.name === previous)) select.value = previous;
  displayRule();
}
function displayRule() {
  const rule = rules.find(item => item.name === byId("rules-select").value);
  byId("rules-editor").value = rule?.content || "";
  byId("rules-state").textContent = rule ? "SHA-256 " + short(rule.sha256) : "无规则文件";
  byId("save-rules").disabled = !rule;
}
async function retention() {
  try {
    const value = await api("/api/retention");
    byId("retention").textContent = "日常保留 " + value.dailyDays + " 天 / 最多 " +
      value.dailyMaxRuns + " 次；待精简 " + value.selectedRuns + " 次" +
      (value.overBudget ? "；受保护材料超过预算" : "") +
      (value.limitations?.length ? "；限制 " + value.limitations.length + " 项：" +
        value.limitations.slice(0, 2).map(item =>
          item.replace("Unregistered report is protected:", "未登记报告受保护：")).join("；") +
        (value.limitations.length > 2 ? "；其余见 CLI 预览" : "") : "");
  } catch (error) { byId("retention").textContent = error.message; }
}
async function action(callback) {
  try { message(""); await callback(); await refresh(); }
  catch (error) { message(error.message, true); }
}
byId("run-daily").addEventListener("click", () => action(async () => {
  await api("/api/gate", "POST", { evidenceGrade: "daily" });
  message("日常门禁已启动。");
}));
byId("run-full").addEventListener("click", () => action(async () => {
  await api("/api/gate", "POST", { evidenceGrade: "full" });
  message("完整取证已启动。");
}));
byId("cancel").addEventListener("click", () => action(async () => {
  await api("/api/cancel", "POST", { runId: displayedRun });
  message("已请求取消当前运行。");
}));
byId("rules-select").addEventListener("change", displayRule);
byId("reload-rules").addEventListener("click", () => action(loadRules));
byId("save-rules").addEventListener("click", () => action(async () => {
  const rule = rules.find(item => item.name === byId("rules-select").value);
  if (!rule) throw new Error("请先选择规则文件。");
  await api("/api/rules", "POST", {
    name: rule.name, expectedSha256: rule.sha256, content: byId("rules-editor").value
  });
  await loadRules();
  message("规则已保存，旧结果会标记为过期。");
}));
byId("refresh-retention").addEventListener("click", () => action(retention));
(async () => {
  try {
    csrf = (await api("/api/session")).token;
    await Promise.all([refresh(), loadRules(), retention()]);
    setInterval(refresh, 3000);
  } catch (error) { message(error.message, true); }
})();
