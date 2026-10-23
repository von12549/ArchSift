"use strict";
(() => {
  const el = id => document.getElementById(id);
  const make = (tag, value, className) => {
    const element = document.createElement(tag);
    if (value !== undefined) element.textContent = String(value);
    if (className) element.className = className;
    return element;
  };
  const table = (heads, rows) => {
    const result = make("table"), header = make("tr");
    for (const label of heads) {
      const cell = make("th", label);
      cell.scope = "col";
      header.append(cell);
    }
    result.append(header, ...rows.map(values => {
      const row = make("tr");
      for (const value of values) row.append(make("td", value));
      return row;
    }));
    return result;
  };
  const canonicalInventoryPath = path => /^[a-z]:/i.test(path) ? path.split(String.fromCharCode(92)).join("/").toLowerCase() : path;
  let inventoryConfigKey = null;
  const disclosureState = new Map(), inventoryDetails = new Map();
  Object.assign(zhTranslations, {
    "Expand all JSON rules": "\u5c55\u5f00\u5168\u90e8 JSON \u89c4\u5219",
    "Collapse all JSON rules": "\u6298\u53e0\u5168\u90e8 JSON \u89c4\u5219",
    "JSON rulesets": "JSON \u89c4\u5219\u96c6",
    "JSON cases": "JSON \u7528\u4f8b",
    "C# cases": "C# \u7528\u4f8b",
    "Expanded JSON rulesets": "\u5df2\u5c55\u5f00 JSON \u89c4\u5219\u96c6",
    "No JSON rulesets": "\u6ca1\u6709 JSON \u89c4\u5219\u96c6"
  });
  let inventory = null, discovery = null, historyItems = [], reportData = null;
  let offset = 0, total = 0, polling = null, shownRunId = null, historySelected = false;

  function message(value, failed = false) {
    el("test-message").textContent = value;
    el("test-message").className = failed ? "error" : "hint";
  }
  function openTestResults() { showPanel("results", "tests"); }
  function openTestHistory() { showPanel("history", "tests"); }
  function stateName(state) {
    return t(({finished: "Finished", failed: "Failed", cancelled: "Cancelled",
      unavailable: "Unavailable", interrupted: "Interrupted", running: "Running"})[state] || state);
  }
  function caseResultsTable(checks, source) {
    const wrapper = make("div"), controls = make("div", undefined, "actions rule-sort-controls");
    const region = make("div", undefined, "test-result-table"), announcement = make("p", undefined, "hint");
    announcement.setAttribute("role", "status");
    announcement.setAttribute("aria-live", "polite");
    const caseLabel = source === "json-type-dependency" ? "Rule" : "Case / class";
    const original = checks.map((value, index) => ({value, index}));
    const ranks = ["failed", "error", "cancelled", "skipped", "not-run", "passed"];
    let field = null, direction = 1;
    const buttons = {};
    const compare = (a, b) => a < b ? -1 : a > b ? 1 : 0;
    const name = value => value.ruleId || value.scope || value.id;
    function paint() {
      const rows = [...original];
      const rank = value => ranks.includes(value.result) ? ranks.indexOf(value.result) : ranks.length;
      if (field) rows.sort((a, b) => direction * (field === "case" ?
        compare(name(a.value), name(b.value)) : rank(a.value) - rank(b.value)) || a.index - b.index);
      const view = table([t(caseLabel), t("Result"), t("Execution"), t("Message")], rows.map(({value}) => [
        name(value) + (value.ruleId && value.scope ? "\n" + value.scope : "") + "\n" + (value.runnerCaseId || ""),
        t(value.result), value.executed ? t("Executed") : t("Not executed"), (value.messages || []).join("\n")
      ]));
      for (const [index, key] of [[0, "case"], [1, "result"]]) {
        view.firstChild.children[index].setAttribute("aria-sort", field === key ?
          (direction === 1 ? "ascending" : "descending") : "none");
        buttons[key].textContent = t(key === "case" ? caseLabel : "Result") +
          (field === key ? (direction === 1 ? " ↑" : " ↓") : "");
      }
      announcement.textContent = field ? t(field === "case" ? caseLabel : "Result") + " · " +
        t(direction === 1 ? "Ascending" : "Descending") : t("Default order");
      region.replaceChildren(view);
    }
    for (const key of ["case", "result"]) {
      const button = make("button"); button.type = "button";
      button.onclick = () => { if (field !== key) { field = key; direction = 1; }
        else if (direction === 1) direction = -1;
        else { field = null; direction = 1; } paint(); };
      buttons[key] = button; controls.append(button);
    }
    const reset = make("button", t("Default order")); reset.type = "button";
    reset.onclick = () => { field = null; direction = 1; paint(); };
    controls.append(reset);
    wrapper.append(controls, announcement, region); paint(); return wrapper;
  }
  function renderInventory(data) {
    const area = el("test-inventory");
    const focused = area.contains?.(document.activeElement) ? document.activeElement.dataset.inventoryFocus : null;
    const configKey = data?.configured ? canonicalInventoryPath(data.configPath) : null;
    const sameConfig = configKey === inventoryConfigKey;
    if (configKey !== inventoryConfigKey) disclosureState.clear();
    else for (const [path, detail] of inventoryDetails) disclosureState.set(path, detail.open);
    inventoryConfigKey = configKey;
    inventoryDetails.clear();
    const paths = new Set((data?.rules || []).map(rule => canonicalInventoryPath(rule.path)));
    for (const path of disclosureState.keys()) if (!paths.has(path)) disclosureState.delete(path);
    area.replaceChildren();
    if (!data?.configured) {
      inventory = null;
      el("test-start").disabled = true;
      area.append(make("p", t("Select a daily-test configuration for this Web target."), "empty"));
      return;
    }
    inventory = data;
    el("test-config-path").value = data.configPath;
    el("test-start").disabled = false;
    area.append(make("h3", t("Test project and sources")), make("p", data.targetRoot, "path"),
      make("p", t("Test project") + ": " + data.testProject, "path"));
    area.append(make("p", "Audit: " + data.auditMode + " · " +
      (data.auditMode === "offline-architecture" ? "not-run" : "not-verified")));
    if (data.toolIdentity) area.append(make("p", data.toolIdentity + (data.toolRoot ? " · " + data.toolRoot : ""), "path"));
    const policy = make("details");
    policy.append(make("summary", t("Policy sources") + " · " + (data.policy?.status || "unknown")),
      make("pre", JSON.stringify(data.policy, null, 2), "path"));
    area.append(policy);
    const cards = make("div", undefined, "test-source-grid");
    const original = make("article", undefined, "test-source-card");
    const logical = data.identityMode === "logical-v1";
    const caseKeys = logical ? data.logicalCaseKeys : data.originalCaseIds;
    original.append(make("h4", t("IFX specialized C# tests · read-only")),
      make("p", t(logical ? "Required logical cases" : "Baseline IDs") + ": " + caseKeys.length + ". " +
        t(logical ? "This inventory is read from the verified case manifest; tests have not run yet." :
          "This inventory is read from the baseline; tests have not run yet.")));
    const originalList = make("details"), originalIds = make("div", undefined, "test-id-list");
    originalList.append(make("summary", t(logical ? "View required logical cases" : "View original case IDs")));
    for (const id of caseKeys) originalIds.append(make("code", id));
    originalList.append(originalIds);
    original.append(originalList);
    cards.append(original);
    const json = make("article", undefined, "test-source-card");
    json.append(make("h4", t("ArchSift JSON type dependencies")),
      make("p", t("Copy rules to the external library to edit them. The next run uses saved bytes.")));
    const counts = make("p", t("JSON rulesets") + ": " + data.rules.length + " · " +
      t("JSON cases") + ": " + data.rules.reduce((count, rule) => count + rule.cases.length, 0) +
      " · " + t("C# cases") + ": " + caseKeys.length, "hint");
    const controls = make("div", undefined, "actions");
    const announcement = make("p", undefined, "hint");
    announcement.setAttribute("role", "status");
    announcement.setAttribute("aria-live", "polite");
    function announce() {
      announcement.textContent = t("Expanded JSON rulesets") + ": " +
        [...inventoryDetails.values()].filter(detail => detail.open).length + " / " + data.rules.length;
    }
    for (const open of [true, false]) {
      const button = make("button", t(open ? "Expand all JSON rules" : "Collapse all JSON rules"));
      button.type = "button";
      button.disabled = data.rules.length === 0;
      button.dataset.inventoryFocus = open ? "expand-json" : "collapse-json";
      button.onclick = () => {
        for (const [path, detail] of inventoryDetails) { detail.open = open; disclosureState.set(path, open); }
        announce();
      };
      controls.append(button);
    }
    json.append(counts, controls, announcement);
    if (!data.rules.length) json.append(make("p", t("No JSON rulesets"), "empty"));
    for (const rule of data.rules) {
      const ruleKey = canonicalInventoryPath(rule.path);
      const card = make("div", undefined, "test-rule-row");
      const details = make("details"), summary = make("summary");
      const name = rule.path.split(String.fromCharCode(92)).join("/").split("/").pop();
      summary.append(make("strong", name), make("span", " · " + rule.cases.length + " " + t("JSON cases") + (rule.cases.length ? "" : " · " + t("No enabled cases"))),
        make("span", rule.path, "path"));
      summary.dataset.inventoryFocus = "summary:" + ruleKey;
      details.append(summary);
      details.open = disclosureState.get(ruleKey) || false;
      inventoryDetails.set(ruleKey, details);
      details.addEventListener("toggle", () => {
        if (inventoryConfigKey !== configKey || !details.isConnected) return;
        disclosureState.set(ruleKey, details.open);
        if (!details.open && details.contains(document.activeElement) && document.activeElement !== summary) summary.focus();
        announce();
      });
      if (rule.cases.length) {
        const cases = make("ol", undefined, "test-rule-cases");
        for (const id of rule.cases) cases.append(make("li", id));
        details.append(cases);
      } else details.append(make("p", t("No enabled cases")));
      const button = make("button", t(rule.editable ?
        "Open in rule editor" : "Copy to external library and edit"));
      button.type = "button";
      button.dataset.inventoryFocus = "edit:" + ruleKey;
      button.setAttribute("aria-label", button.textContent + ": " + rule.path);
      button.onclick = async () => {
        try {
          const copy = await api("test-workbench/copy-rule", {index: rule.index});
          renderInventory(await api("test-workbench"));
          await refreshRules();
          await refreshLibrary();
          const saved = libraryCards.find(item => item.entryId === copy.entryId);
          if (!saved) throw new Error(t("The saved rule is missing from the external library."));
          await editCard(saved);
        } catch (error) { message(localizedMessage(error.message), true); }
      };
      card.append(details, button);
      json.append(card);
    }
    cards.append(json);
    const deferred = make("article", undefined, "test-source-card");
    deferred.append(make("h4", t("Not executed as JSON")),
      make("p", data.notIncluded.length + " " +
        t("items are outside JSON execution; C# responsibility and current results are shown separately.")));
    renderResponsibilities(deferred, data.responsibilityProjection);
    const deferredList = make("details"), list = make("div", undefined, "test-deferred-list");
    deferredList.append(make("summary", t("View not-included items")));
    for (const item of data.notIncluded)
      list.append(make("p", item.id + " · " + t("Historical LT00 metadata") + " · " + item.reason));
    deferredList.append(list);
    deferred.append(deferredList);
    cards.append(deferred);
    area.append(cards);
    announce();
    if (focused && sameConfig) {
      const next = [...area.querySelectorAll("[data-inventory-focus]")].find(item => item.dataset.inventoryFocus === focused);
      if (next) next.focus();
    }
  }
  function renderDiscovery() {
    const area = el("test-case-results"), source = el("test-discovery-source");
    area.replaceChildren();
    if (!discovery?.available) {
      source.textContent = t(discovery?.reason || "Run a complete daily test to discover case UIDs.");
      return;
    }
    source.textContent = t("UIDs from complete run") + " " + discovery.runId +
      " · " + t("Freshness") + ": " + t(discovery.historyFreshness) + ". " +
      t("Re-run the complete gate after target or build changes.");
    const query = el("test-case-search").value.trim().toLocaleLowerCase();
    const matches = discovery.cases.filter(item =>
      item.displayName.toLocaleLowerCase().includes(query) ||
      item.uid.toLocaleLowerCase().includes(query));
    area.append(make("p", t("Matching cases") + ": " + matches.length +
      " / " + discovery.cases.length, "hint"));
    for (const item of matches.slice(0, 20)) {
      const row = make("div", undefined, "test-case-option");
      const name = make("strong", item.displayName);
      const uid = make("code", item.uid);
      const button = make("button", t("Use this UID"));
      button.type = "button";
      button.setAttribute("aria-label", t("Use case UID") + ": " + item.displayName);
      button.onclick = () => {
        el("test-case-id").value = item.uid;
        el("test-case-id").focus();
        message(t("Selected discovered case UID from run") + " " + discovery.runId + ".");
      };
      row.append(name, uid, button);
      area.append(row);
    }
    if (matches.length > 20)
      area.append(make("p", t("Showing the first 20; refine the search."), "hint"));
  }
  async function refreshDiscovery() {
    if (!inventory) return;
    discovery = await api("test-workbench/discovery");
    renderDiscovery();
  }
  function renderHistory() {
    const area = el("test-history");
    area.replaceChildren();
    for (const item of historyItems) {
      const card = make("article", undefined, "test-history-card");
      card.append(make("strong", item.startedUtc + " · " + stateName(item.state) +
        " · " + (item.gate || t("No current gate")) + " · " +
        t("Exit") + " " + (item.exitCode ?? "—")),
        make("p", t("Freshness") + ": " + t(item.historyFreshness) + " · " + item.runId, "path"));
      for (const reason of item.reasons || [])
        card.append(make("p", localizedMessage(reason), "limitation"));
      if (item.reportPath && item.state !== "unavailable" && item.state !== "interrupted") {
        const actions = make("div", undefined, "actions");
        const view = make("button", t("View in Results"));
        view.type = "button";
        view.onclick = () => showReport(item.runId, true).catch(error =>
          message(localizedMessage(error.message), true));
        actions.append(view);
        for (const format of ["json", "html"]) {
          const button = make("button", t("Download") + " " + format.toUpperCase());
          button.type = "button";
          button.onclick = () => download("test-workbench/report/" +
            encodeURIComponent(item.runId) + "/" + format,
            "architecture-test-" + item.runId + "." + format).catch(error =>
            message(localizedMessage(error.message), true));
          actions.append(button);
        }
        card.append(actions);
      }
      area.append(card);
    }
    if (!historyItems.length)
      area.append(make("p", t("No architecture test runs have been recorded."), "empty"));
  }
  async function historyPage() {
    if (!inventory) return;
    const page = await api("test-workbench/history?offset=" + offset + "&limit=25");
    total = page.total;
    historyItems = page.items;
    el("test-history-page").textContent = total ?
      (offset + 1) + "–" + Math.min(offset + 25, total) + " / " + total : "0 / 0";
    el("test-history-prev").disabled = offset === 0;
    el("test-history-next").disabled = offset + 25 >= total;
    renderHistory();
  }
  function renderReport(report) {
    const area = el("test-report");
    area.replaceChildren();
    if (report.schemaVersion !== 1 || report.kind !== "architecture-test-run" ||
        (report.responsibilityProjection && !supportedResponsibilities(report.responsibilityProjection))) {
      area.append(make("p", t("Unsupported report or responsibility projection."), "limitation"));
      return;
    }
    const heading = make("h3", t("Run") + " " + report.run.id);
    heading.tabIndex = -1;
    area.append(heading, make("p", report.outcome.runGate + " / " + report.outcome.exitCode +
      " · " + t(report.run.mode === "full" ? "Complete" : "Subset") +
      " · " + t("Complete coverage") + ": " + report.testScope.coverageComplete,
      report.outcome.runGate === "allow" ? "pass" : "limitation"));
    for (const reason of report.outcome.reasons || [])
      area.append(make("p", localizedMessage(reason), "limitation"));
    const checks = report.checks || [];
    const summary = make("div", undefined, "result-region outcome-region test-summary");
    summary.setAttribute("role", "region");
    summary.setAttribute("aria-label", t("Architecture test summary"));
    summary.append(make("h3", t("Architecture test summary")));
    const metrics = make("div", undefined, "metrics");
    const count = result => checks.filter(check => check.executed && check.result === result).length;
    const notRun = checks.filter(check => !check.executed || check.result === "not-run").length;
    const other = checks.filter(check => check.executed && !["passed", "failed", "skipped", "not-run"].includes(check.result)).length;
    for (const [label, value] of [["Total cases", checks.length], ["Executed", checks.filter(check => check.executed).length],
      ["passed", count("passed")], ["failed", count("failed")], ["skipped", count("skipped")],
      ["Not run", notRun], ["Other results", other]]) {
      const metric = make("span"); metric.append(make("b", value), make("span", t(label))); metrics.append(metric);
    }
    summary.append(metrics); area.append(summary);
    for (const [source, title] of [["json-type-dependency", "ArchSift JSON type dependencies"],
      ["ifx-specialized", "IFX specialized C# tests"]]) {
      const group = checks.filter(check => check.source === source);
      const details = make("details");
      details.open = source === "json-type-dependency";
      const passed = group.filter(check => check.executed && check.result === "passed").length;
      details.append(make("summary", t(title) + " · " + passed + " / " + group.length + " " + t("passed")));
      details.append(caseResultsTable(group, source));
      area.append(details);
    }
    const deferred = make("details");
    deferred.append(make("summary", t("Not executed as JSON") + " · " +
      (report.notIncluded || []).length + " " + t("items")));
    renderResponsibilities(deferred, report.responsibilityProjection);
    deferred.append(make("p", t("Historical LT00 metadata")));
    deferred.append(table([t("ID"), t("Source"), t("Description")],
      (report.notIncluded || []).map(item => [item.id, item.source, item.reason])));
    area.append(deferred);
    for (const finding of report.findings || [])
      area.append(make("pre", JSON.stringify(finding, null, 2), "test-finding"));
  }
  function supportedResponsibilities(projection) {
    return projection.schemaVersion === 1 && projection.kind === "rule-responsibility-projection" &&
      projection.semantics === "not-executed-as-json" && Array.isArray(projection.items) &&
      projection.items.every(item => item && typeof item.id === "string" && ["A", "B"].includes(item.category) &&
        ["adopted-active", "adopted-pending-activation", "unknown"].includes(item.policyState) &&
        ["ifx-csharp", "not-assigned", "unknown"].includes(item.responsibility) &&
        ["passed", "failed", "skipped", "not-run", "unavailable"].includes(item.executionResult) &&
        Array.isArray(item.caseKeys) && Array.isArray(item.decisionReferences));
  }
  function renderResponsibilities(area, projection) {
    if (!projection || !supportedResponsibilities(projection)) {
      area.append(make("p", t("Current responsibility projection unavailable; historical results remain unchanged."), "limitation"));
      return;
    }
    const items = projection.items;
    const assigned = items.filter(item => item.responsibility === "ifx-csharp");
    const remaining = items.filter(item => item.responsibility !== "ifx-csharp");
    const pending = items.filter(item => item.policyState === "adopted-pending-activation");
    area.append(make("p", t("Assigned to approved C# cases") + ": " + assigned.length + " · " +
      t("Remaining inventory") + ": " + remaining.length + " (A " + remaining.filter(item => item.category === "A").length +
      " / B " + remaining.filter(item => item.category === "B").length + ") · " +
      t("adopted-pending-activation") + ": " + pending.length));
    area.append(make("p", t("Inventory counts do not prove detection gaps or current passes.")));
    const details = make("details");
    details.append(make("summary", t("Current policy and execution responsibility")));
    const wrapper = make("div", undefined, "test-result-table test-responsibilities");
    wrapper.append(table([t("ID"), t("Policy state"), t("Execution responsibility"), t("This execution")],
      items.map(item => [item.id, t(item.policyState), t(item.responsibility), t(item.executionResult)])));
    details.append(wrapper); area.append(details);
  }
  async function showReport(runId, navigate) {
    clearReport();
    const report = await api("test-workbench/report/" + encodeURIComponent(runId) + "/json");
    reportData = report;
    shownRunId = runId;
    if (navigate) historySelected = true;
    renderReport(report);
    if (navigate) {
      openTestResults();
      const heading = el("test-report").querySelector("h3");
      heading?.focus({preventScroll: true});
      heading?.scrollIntoView({block: "center"});
    }
  }
  function clearReport() {
    shownRunId = null;
    reportData = null;
    el("test-report").replaceChildren(make("p",
      t("This run has no authoritative result yet."), "empty"));
  }
  async function updateJob() {
    const job = await api("test-workbench/job");
    const running = job?.state === "running", wasPolling = !!polling;
    el("test-start").disabled = !inventory || running;
    el("test-cancel").disabled = !running;
    el("test-cancel-result").disabled = !running;
    const status = job ? t("Current run") + ": " + job.id + " · " +
      stateName(job.state) + (job.exitCode === null ? "" :
        " · " + job.gate + " / " + job.exitCode) +
      (job.error ? " · " + localizedMessage(job.error) : "") :
      t("No architecture test run in this session.");
    el("test-current").textContent = status;
    el("test-current-brief").textContent = status;
    if (running && !polling)
      polling = setInterval(() => updateJob().catch(error =>
        message(localizedMessage(error.message), true)), 1000);
    if (!running && polling) { clearInterval(polling); polling = null; }
    if (!running && (wasPolling || job?.reportRunId && shownRunId !== job.reportRunId)) {
      await historyPage();
      await refreshDiscovery();
      if (!historySelected && job?.reportRunId && shownRunId !== job.reportRunId)
        await showReport(job.reportRunId, false);
    }
  }
  function setMode() {
    const subset = el("test-mode").value === "subset";
    el("test-case-id").disabled = !subset;
    el("test-subset-picker").hidden = !subset;
    const hint = subset ?
      "Subset mode runs one discovered case and cannot allow the daily gate. Snapshot updates remain disabled." :
      "Complete mode runs every original and JSON case. Snapshot updates are disabled; offline restore, build and tests run in sequence.";
    el("test-mode-hint").setAttribute("data-i18n", hint);
    el("test-mode-hint").textContent = t(hint);
    if (subset && !discovery) refreshDiscovery().catch(error =>
      message(localizedMessage(error.message), true));
  }
  el("test-load").onclick = async () => {
    try {
      const view = await api("test-workbench/config",
        {path: el("test-config-path").value.trim()});
      renderInventory(view);
      offset = 0;
      await historyPage();
      await refreshDiscovery();
      await updateJob();
      message(t("Inventory loaded; reading it did not run tests."));
    } catch (error) {
      renderInventory(null);
      clearReport();
      discovery = null;
      historyItems = [];
      renderDiscovery();
      renderHistory();
      message(localizedMessage(error.message), true);
    }
  };
  el("test-mode").onchange = setMode;
  el("test-case-search").oninput = renderDiscovery;
  el("test-start").onclick = async () => {
    try {
      const mode = el("test-mode").value;
      await api("test-workbench/run",
        {mode, caseId: mode === "subset" ? el("test-case-id").value.trim() : null});
      historySelected = false;
      clearReport();
      openTestResults();
      await updateJob();
      message(t("Test run started; a conclusion appears only when this run finishes."));
    } catch (error) { message(localizedMessage(error.message), true); }
  };
  const cancel = async () => {
    try { await api("test-workbench/cancel", {}); await updateJob(); }
    catch (error) { message(localizedMessage(error.message), true); }
  };
  el("test-cancel").onclick = cancel;
  el("test-cancel-result").onclick = cancel;
  el("test-view-results").onclick = async () => {
    openTestResults();
    try {
      const job = await api("test-workbench/job");
      if (job?.reportRunId) {
        historySelected = false;
        await showReport(job.reportRunId, false);
      }
    } catch (error) { message(localizedMessage(error.message), true); }
  };
  el("test-view-history").onclick = () => {
    openTestHistory();
    historyPage().catch(error => message(localizedMessage(error.message), true));
  };
  el("history-test-tab").addEventListener("click", () =>
    historyPage().catch(error => message(localizedMessage(error.message), true)));
  el("test-history-refresh").onclick = () =>
    historyPage().catch(error => message(localizedMessage(error.message), true));
  el("test-history-prev").onclick = () => {
    offset = Math.max(0, offset - 25);
    historyPage().catch(error => message(localizedMessage(error.message), true));
  };
  el("test-history-next").onclick = () => {
    offset += 25;
    historyPage().catch(error => message(localizedMessage(error.message), true));
  };
  el("test-full-entry").onclick = () => showPanel("evidence");
  document.addEventListener("archsift-locale-changed", () => {
    setMode();
    if (inventory) renderInventory(inventory);
    renderDiscovery();
    renderHistory();
    if (reportData) renderReport(reportData);
    updateJob().catch(error => message(localizedMessage(error.message), true));
  });
  api("test-workbench").then(async view => {
    renderInventory(view);
    if (view.configured) {
      await historyPage();
      await refreshDiscovery();
    }
    await updateJob();
    setMode();
  }).catch(error => message(localizedMessage(error.message), true));
})();
