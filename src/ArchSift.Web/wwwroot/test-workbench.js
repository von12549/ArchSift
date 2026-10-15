"use strict";
(() => {
  const el = id => document.getElementById(id);
  const make = (tag, value, className) => {
    const element = document.createElement(tag);
    if (value !== undefined) element.textContent = String(value);
    if (className) element.className = className;
    return element;
  };
  let inventory = null, offset = 0, total = 0, polling = null;
  const message = (value, failed = false) => {
    el("test-message").textContent = value;
    el("test-message").className = failed ? "error" : "hint";
  };
  const row = (...cells) => {
    const tr = make("tr");
    for (const cell of cells) tr.append(make("td", cell));
    return tr;
  };
  const table = (heads, rows) => {
    const table = make("table"), head = make("tr");
    for (const label of heads) head.append(make("th", label));
    table.append(head, ...rows);
    return table;
  };
  function renderInventory(data) {
    const area = el("test-inventory"); area.replaceChildren();
    if (!data.configured) {
      el("test-start").disabled = true;
      area.append(make("p", "选择与当前 Web 目标相同的测试门禁配置。", "empty"));
      return;
    }
    inventory = data;
    el("test-config-path").value = data.configPath;
    el("test-start").disabled = false;
    area.append(make("h3", "IFX 工程与测试来源"), make("p", data.targetRoot, "path"),
      make("p", "测试工程：" + data.testProject, "path"));
    const sourceCards = make("div", undefined, "test-source-grid");
    const original = make("article", undefined, "test-source-card");
    original.append(make("h4", "IFX 专用 C# 测试 · 只读"),
      make("p", "基准 ID " + data.originalCaseIds.length + " 项。此清单读取并核对基准文件，尚未执行测试。"));
    const originalList = make("details"), originalIds = make("div", undefined, "test-id-list");
    originalList.append(make("summary", "查看原案例 ID"));
    for (const id of data.originalCaseIds) originalIds.append(make("code", id));
    originalList.append(originalIds); original.append(originalList); sourceCards.append(original);
    const json = make("article", undefined, "test-source-card");
    json.append(make("h4", "ArchSift JSON 类型依赖"),
      make("p", "规则可复制到外置规则库后编辑。保存的字节将在下一次门禁中冻结。"));
    for (const rule of data.rules) {
      const card = make("div", undefined, "test-rule-row");
      card.append(make("p", rule.path, "path"), make("p", rule.cases.join(" · ") || "无启用案例"));
      const button = make("button", rule.editable ? "在规则编辑器中打开" : "复制到外置规则库并编辑");
      button.type = "button";
      button.onclick = async () => {
        try {
          const copy = await api("test-workbench/copy-rule", {index: rule.index});
          renderInventory(await api("test-workbench"));
          await refreshRules(); await refreshLibrary();
          const card = libraryCards.find(item => item.entryId === copy.entryId);
          if (!card) throw new Error("保存的规则未出现在外置规则库。");
          await editCard(card);
        } catch (error) { message(error.message, true); }
      };
      card.append(button); json.append(card);
    }
    sourceCards.append(json);
    const deferred = make("article", undefined, "test-source-card");
    deferred.append(make("h4", "旧 ArchSift A 类与未启用 B 候选 · 未纳入"),
      make("p", data.notIncluded.length + " 项未在当前门禁执行；历史 pass 不会使它们变绿。"));
    const deferredList = make("details"), list = make("div", undefined, "test-deferred-list");
    deferredList.append(make("summary", "查看未纳入清单"));
    for (const item of data.notIncluded)
      list.append(make("p", item.id + " · " + item.source + " · " + item.reason));
    deferredList.append(list); deferred.append(deferredList); sourceCards.append(deferred);
    area.append(sourceCards);
  }
  async function historyPage() {
    if (!inventory) return;
    const page = await api("test-workbench/history?offset=" + offset + "&limit=25");
    total = page.total;
    el("test-history-page").textContent = total ? (offset + 1) + "–" + Math.min(offset + 25, total) + " / " + total : "0 / 0";
    el("test-history-prev").disabled = offset === 0;
    el("test-history-next").disabled = offset + 25 >= total;
    const area = el("test-history"); area.replaceChildren();
    for (const item of page.items) {
      const card = make("article", undefined, "test-history-card");
      card.append(make("strong", item.startedUtc + " · " + item.state + " · " + (item.gate || "无当前门禁") +
        " · exit " + (item.exitCode ?? "—")),
        make("p", "新鲜度：" + item.historyFreshness + " · " + item.runId, "path"));
      for (const reason of item.reasons || []) card.append(make("p", reason, "limitation"));
      if (item.reportPath && item.state !== "unavailable" && item.state !== "interrupted") {
        const actions = make("div", undefined, "actions");
        const view = make("button", "查看结果");
        view.onclick = () => showReport(item.runId).catch(error => message(error.message, true));
        actions.append(view);
        for (const format of ["json", "html"]) {
          const button = make("button", "下载 " + format.toUpperCase());
          button.onclick = () => download("test-workbench/report/" + encodeURIComponent(item.runId) + "/" + format,
            "architecture-test-" + item.runId + "." + format).catch(error => message(error.message, true));
          actions.append(button);
        }
        card.append(actions);
      }
      area.append(card);
    }
    if (!page.items.length) area.append(make("p", "尚无带状态标记的测试运行。", "empty"));
  }
  async function showReport(runId) {
    const report = await api("test-workbench/report/" + encodeURIComponent(runId) + "/json");
    const area = el("test-report"); area.replaceChildren();
    area.append(make("h3", "运行 " + report.run.id),
      make("p", report.outcome.runGate + " / " + report.outcome.exitCode +
        " · " + report.run.mode + " · 覆盖完整：" + report.testScope.coverageComplete,
        report.outcome.runGate === "allow" ? "pass" : "limitation"));
    for (const reason of report.outcome.reasons || []) area.append(make("p", reason, "limitation"));
    const checks = report.checks || [];
    for (const [source, title] of [["ifx-specialized", "IFX 专用 C# 测试"],
      ["json-type-dependency", "ArchSift JSON 类型依赖"]]) {
      const group = checks.filter(check => check.source === source);
      const details = make("details"); details.open = true;
      details.append(make("summary", title + " · " + group.length + " 项"));
      details.append(table(["案例 / 类", "结果", "执行", "消息"], group.map(check => row(
        (check.scope || check.id) + "\n" + (check.runnerCaseId || ""), check.result,
        check.executed ? "已执行" : "未执行", (check.messages || []).join("\n")))));
      area.append(details);
    }
    const deferred = make("details");
    deferred.append(make("summary", "未纳入本次门禁 · " + (report.notIncluded || []).length + " 项"));
    deferred.append(table(["ID", "来源", "说明"], (report.notIncluded || []).map(item => row(item.id, item.source, item.reason))));
    area.append(deferred);
    for (const finding of report.findings || []) area.append(make("pre", JSON.stringify(finding, null, 2), "test-finding"));
  }
  async function updateJob() {
    const job = await api("test-workbench/job");
    const running = job?.state === "running";
    el("test-start").disabled = !inventory || running;
    el("test-cancel").disabled = !running;
    el("test-current").textContent = job ?
      "本次运行：" + job.id + " · " + job.state +
      (job.exitCode === null ? "" : " · " + job.gate + " / " + job.exitCode) +
      (job.error ? " · " + job.error : "") : "尚未启动本次运行。";
    if (running && !polling) polling = setInterval(() => updateJob().catch(error => message(error.message, true)), 1000);
    if (!running && polling) { clearInterval(polling); polling = null; await historyPage(); }
  }
  el("test-load").onclick = async () => {
    try {
      const view = await api("test-workbench/config", {path: el("test-config-path").value.trim()});
      renderInventory(view); offset = 0; await historyPage(); await updateJob();
      message("清单已读取；尚未执行测试。");
    } catch (error) { message(error.message, true); }
  };
  el("test-mode").onchange = () => { el("test-case-id").disabled = el("test-mode").value !== "subset"; };
  el("test-start").onclick = async () => {
    try {
      const mode = el("test-mode").value;
      await api("test-workbench/run", {mode, caseId: mode === "subset" ? el("test-case-id").value.trim() : null});
      await updateJob(); message("门禁已启动；本次结果完成后才显示结论。");
    } catch (error) { message(error.message, true); }
  };
  el("test-cancel").onclick = async () => { try { await api("test-workbench/cancel", {}); await updateJob(); }
    catch (error) { message(error.message, true); } };
  el("test-history-refresh").onclick = () => historyPage().catch(error => message(error.message, true));
  el("test-history-prev").onclick = () => { offset = Math.max(0, offset - 25); historyPage().catch(error => message(error.message, true)); };
  el("test-history-next").onclick = () => { offset += 25; historyPage().catch(error => message(error.message, true)); };
  el("test-full-entry").onclick = () => showPanel("evidence");
  api("test-workbench").then(view => { renderInventory(view); if (view.configured) historyPage(); return updateJob(); })
    .catch(error => message(error.message, true));
})();
