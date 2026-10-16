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
  function renderInventory(data) {
    const area = el("test-inventory");
    area.replaceChildren();
    if (!data.configured) {
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
    const cards = make("div", undefined, "test-source-grid");
    const original = make("article", undefined, "test-source-card");
    original.append(make("h4", t("IFX specialized C# tests · read-only")),
      make("p", t("Baseline IDs") + ": " + data.originalCaseIds.length + ". " +
        t("This inventory is read from the baseline; tests have not run yet.")));
    const originalList = make("details"), originalIds = make("div", undefined, "test-id-list");
    originalList.append(make("summary", t("View original case IDs")));
    for (const id of data.originalCaseIds) originalIds.append(make("code", id));
    originalList.append(originalIds);
    original.append(originalList);
    cards.append(original);
    const json = make("article", undefined, "test-source-card");
    json.append(make("h4", t("ArchSift JSON type dependencies")),
      make("p", t("Copy rules to the external library to edit them. The next run uses saved bytes.")));
    for (const rule of data.rules) {
      const card = make("div", undefined, "test-rule-row");
      card.append(make("p", rule.path, "path"),
        make("p", rule.cases.join(" · ") || t("No enabled cases")));
      const button = make("button", t(rule.editable ?
        "Open in rule editor" : "Copy to external library and edit"));
      button.type = "button";
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
      card.append(button);
      json.append(card);
    }
    cards.append(json);
    const deferred = make("article", undefined, "test-source-card");
    deferred.append(make("h4", t("Earlier A and inactive B candidates · not included")),
      make("p", data.notIncluded.length + " " +
        t("items are not run by this gate; historical passes do not count here.")));
    const deferredList = make("details"), list = make("div", undefined, "test-deferred-list");
    deferredList.append(make("summary", t("View not-included items")));
    for (const item of data.notIncluded)
      list.append(make("p", item.id + " · " + item.source + " · " + item.reason));
    deferredList.append(list);
    deferred.append(deferredList);
    cards.append(deferred);
    area.append(cards);
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
    const heading = make("h3", t("Run") + " " + report.run.id);
    heading.tabIndex = -1;
    area.append(heading, make("p", report.outcome.runGate + " / " + report.outcome.exitCode +
      " · " + t(report.run.mode === "full" ? "Complete" : "Subset") +
      " · " + t("Complete coverage") + ": " + report.testScope.coverageComplete,
      report.outcome.runGate === "allow" ? "pass" : "limitation"));
    for (const reason of report.outcome.reasons || [])
      area.append(make("p", localizedMessage(reason), "limitation"));
    const checks = report.checks || [];
    for (const [source, title] of [["json-type-dependency", "ArchSift JSON type dependencies"],
      ["ifx-specialized", "IFX specialized C# tests"]]) {
      const group = checks.filter(check => check.source === source);
      const details = make("details");
      details.open = source === "json-type-dependency";
      const passed = group.filter(check => check.executed && check.result === "passed").length;
      details.append(make("summary", t(title) + " · " + passed + " / " + group.length + " " + t("passed")));
      details.append(table([t("Case / class"), t("Result"), t("Execution"), t("Message")],
        group.map(check => [
          (check.scope || check.id) + "\n" + (check.runnerCaseId || ""),
          t(check.result), check.executed ? t("Executed") : t("Not executed"),
          (check.messages || []).join("\n")
        ])));
      area.append(details);
    }
    const deferred = make("details");
    deferred.append(make("summary", t("Not included in this gate") + " · " +
      (report.notIncluded || []).length + " " + t("items")));
    deferred.append(table([t("ID"), t("Source"), t("Description")],
      (report.notIncluded || []).map(item => [item.id, item.source, item.reason])));
    area.append(deferred);
    for (const finding of report.findings || [])
      area.append(make("pre", JSON.stringify(finding, null, 2), "test-finding"));
  }
  async function showReport(runId, navigate) {
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
    } catch (error) { message(localizedMessage(error.message), true); }
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
