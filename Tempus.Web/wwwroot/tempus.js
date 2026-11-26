(async function () {
  "use strict";
  const root = document.querySelector("#root"),
    dialog = document.querySelector("#dialog"),
    form = document.querySelector("#editor");
  const weekdays = ["Mo", "Di", "Mi", "Do", "Fr", "Sa", "So"];
  const tabs = [
    "Übersicht",
    "Arbeitszeiten",
    "Abwesenheiten",
    "Monatsauswertung",
    "Verwaltung",
    "Personalbüro",
    "Projektstunden",
  ];
  const icons = [
    "M12 5v14 M5 12h14",
    "M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z",
    "M12 3a9 9 0 1 0 .1 0 M12 7v5l4 2",
    "M3 6h18v15H3z M7 3v6 M17 3v6 M3 11h18",
    "M3 3v18h18 M7 17v-5 M12 17V7 M17 17v-8",
    "M3 7h18 M3 17h18 M8 4v6 M16 14v6",
  ];
  let s,
    tab = 0,
    month = "",
    person = "",
    projectFilter = "",
    filterDay = "",
    submitAction = null,
    noticeTimer;
  const esc = (v) =>
    String(v ?? "").replace(
      /[&<>"']/g,
      (c) =>
        ({
          "&": "&amp;",
          "<": "&lt;",
          ">": "&gt;",
          '"': "&quot;",
          "'": "&#39;",
        })[c],
    );
  const fmt = (n, sign = false) =>
    `${n < 0 ? "−" : sign && n > 0 ? "+" : ""}${Math.floor(Math.abs(n) / 60)}:${String(Math.abs(n) % 60).padStart(2, "0")}`;
  const clock = (n) =>
    `${String(Math.floor(n / 60)).padStart(2, "0")}:${String(n % 60).padStart(2, "0")}`;
  const date = (
    d,
    options = { day: "2-digit", month: "2-digit", year: "numeric" },
  ) => new Date(d + "T12:00:00").toLocaleDateString("de-CH", options);
  const icon = (i) =>
    `<svg class="ico" viewBox="0 0 24 24" aria-hidden="true"><path d="${icons[(i + 1) % 6]}"/></svg>`;
  const brand =
    '<div class="brand"><span class="mark" aria-hidden="true"><i></i><i></i><i></i></span>Tempus</div>';
  const own = () => s.actor.id === s.person.id;
  const manage = () =>
    s.actor.role === "Administration" ||
    (s.actor.role === "HR" && !own());
  const btn = (name, title, cls = "", extra = "") =>
    `<button type="button" class="${cls}" data-action="${name}" ${extra}>${esc(title)}</button>`;
  const attr = (name, v) => `data-${name}="${esc(v)}"`;
  function tell(msg) {
    const n = document.querySelector("#notice");
    n.textContent = msg;
    clearTimeout(noticeTimer);
    noticeTimer = setTimeout(() => (n.textContent = ""), 4500);
  }
  async function api(path, method = "GET", body) {
    const r = await fetch("/api/" + path, {
      method,
      headers: { "Content-Type": "application/json", "X-Tempus": "1" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (r.status === 401 && path !== "login") {
      login();
      throw Error("Bitte erneut anmelden.");
    }
    if (!r.ok) {
      const e = await r.json().catch(() => null);
      throw Error(e?.error || "Anfrage fehlgeschlagen.");
    }
    return r.status === 204 ? null : r.json();
  }
  function login() {
    s = null;
    person = "";
    month = "";
    tab = 0;
    dialog.close();
    root.innerHTML = `<main class="loginpage"><section class="logincover">${brand}<div><div class="eyebrow">Zeitjournal</div><h1>Arbeitszeit im Überblick.</h1></div><div class="ruler" aria-hidden="true"></div></section><section class="loginform"><h2>Anmelden</h2><form id="loginform" class="fields"><label>Benutzername<input name="login" required autocomplete="username" autofocus></label><label>Passwort<input name="password" type="password" required autocomplete="current-password"></label><button class="primary">Anmelden</button><p id="loginerror" class="error statusline" role="alert"></p></form><details><summary>Demozugänge</summary><table><tbody><tr><td>nora</td><td>Mitarbeit · 100 %</td></tr><tr><td>luca</td><td>Mitarbeit · 60 %</td></tr><tr><td>samira</td><td>Projektleitung · Kundenportal</td></tr><tr><td>mara</td><td>Projektleitung · Betrieb und Weiterbildung</td></tr><tr><td>jonas</td><td>Entwicklungsleitung</td></tr><tr><td>hr</td><td>Personalbüro</td></tr><tr><td>admin</td><td>Administration</td></tr></tbody></table><p class="sectionnote">Anfangspasswort für lokale Demokonten<br><code>Tempus-Demo26!</code></p></details></section></main>`;
    if (location.protocol === "https:") root.querySelector("details").remove();
    document.querySelector("#loginform").onsubmit = async (e) => {
      e.preventDefault();
      const f = e.currentTarget;
      const b = f.querySelector("button");
      b.disabled = true;
      try {
        await api("login", "POST", Object.fromEntries(new FormData(f)));
        await reload();
      } catch (err) {
        document.querySelector("#loginerror").textContent = err.message;
      } finally {
        b.disabled = false;
      }
    };
  }
  let generation = 0,
    hrRows = [],
    projectReport = null,
    slip = null;
  async function reload() {
    const seq = ++generation;
    const q = new URLSearchParams();
    if (person) q.set("person", person);
    if (month) q.set("month", month);
    const data = await api("state?" + q);
    if (seq !== generation) return;
    if (tab === 6) {
      const report = await api("project-report?month=" + data.month);
      if (seq !== generation) return;
      projectReport = report;
    }
    if (tab === 5) {
      const [rows, doc] = await Promise.all([
        api("hr/hours?month=" + data.month),
        api("hr/slip/" + data.person.id + "?month=" + data.month),
      ]);
      if (seq !== generation) return;
      hrRows = rows;
      slip = doc.slip;
    }
    s = data;
    person = s.person.id;
    month = s.month;
    render();
  }
  const opts = (arr, selected) =>
    arr
      .map(
        ([v, n]) =>
          `<option value="${esc(v)}" ${String(selected) === String(v) ? "selected" : ""}>${esc(n)}</option>`,
      )
      .join("");
  const projectOpts = (v) =>
    opts(
      s.projects.map((p) => [p.id, p.name]),
      v,
    );
  const pair = (a, b) =>
    `<div class="kv"><span>${esc(a)}</span><strong>${esc(b)}</strong></div>`;
  function table(headers, rows, empty = "Keine Einträge vorhanden.") {
    return rows.length
      ? `<div class="tablebox"><table><thead><tr>${headers.map((h) => `<th scope="col">${esc(h)}</th>`).join("")}</tr></thead><tbody>${rows.join("")}</tbody></table></div>`
      : `<p class="empty">${esc(empty)}</p>`;
  }
  function render() {
    const admin = s.actor.role === "Administration";
    const canReadProjects = admin || ["Projektleitung", "Entwicklungsleitung"].includes(s.actor.role);
    const name = esc(s.actor.name);
    root.innerHTML = `<div class="shell"><aside class="side">${brand}<nav aria-label="Navigation">${tabs.map((t, i) => ((i === 6 && !canReadProjects) || (i === 4 && !admin) || (i === 5 && !(admin || s.actor.role === "HR")) ? "" : `<button type="button" ${tab === i ? 'class="on" aria-current="page"' : ""} data-action="tab" data-id="${i}">${icon(i)}${t}</button>`)).join("")}</nav><div class="account"><strong>${name}</strong><small>${esc(s.actor.role)}</small>${btn("password", "Passwort ändern")}${btn("logout", "Abmelden")}</div></aside><main class="frame"><header class="top"><strong>Zeitjournal</strong><div class="controls">${
      s.people.length > 1 && tab !== 6
        ? `<label>Person<select id="person">${opts(
            s.people.map((p) => [p.id, p.name]),
            s.person.id,
          )}</select></label>`
        : ""
    }<label>Monat<input type="month" id="month" min="2000-01" max="2099-12" value="${s.month}"></label></div></header><section class="content"><p class="report-head">Tempus · ${esc(s.person.name)} · ${esc(month)}</p><div class="heading"><div><div class="eyebrow">${tab === 6 ? (s.actor.role === "Projektleitung" ? "Zugewiesene Projekte" : "Alle Projekte") : esc(s.person.name)} · ${esc(date(s.month + "-01", { month: "long", year: "numeric" }))}</div><h1>${tabs[tab]}</h1></div><div class="actions">${(tab === 0 || tab === 1) && own() && !s.closed ? btn("entry", "Zeit erfassen", "primary") : ""}${tab === 2 && own() ? btn("absence", "Abwesenheit beantragen", "primary") : ""}${tab === 3 ? btn("csv", "CSV exportieren") + btn("print", "Drucken") : ""}</div></div><div id="view">${[dashboard, entriesPage, absencePage, report, adminPage, hrPage, projectsPage][tab]()}</div></section></main></div>`;
  }
  function metrics() {
    const m = s.summary;
    const items = [
      ["Gleitzeitsaldo", fmt(m.balance, true), "inklusive Vortrag"],
      ["Arbeitszeit", fmt(m.actual), "gebuchte Stunden"],
      ["Sollzeit", fmt(m.target), "bis zum Stichtag"],
      ["Monatsdifferenz", fmt(m.delta, true), "inklusive Gutschriften"],
    ];
    return `<div class="summary">${items.map(([a, b, c]) => `<div class="metric"><small>${a}</small><b>${b}</b><span>${c}</span></div>`).join("")}</div>`;
  }
  function chart() {
    const days = s.days.filter((d) => d.delta !== null);
    if (!days.length)
      return '<p class="empty">Für diesen Monat liegen noch keine Tageswerte vor.</p>';
    let balance = s.summary.carry;
    const vals = [balance, ...days.map((d) => (balance += d.delta))];
    const low = Math.min(0, ...vals),
      high = Math.max(0, ...vals),
      range = high - low || 60;
    const y = (v) => 102 - (88 * (v - low)) / range;
    const xy = vals
      .map((v, i) => `${8 + (i / (vals.length - 1)) * 384},${y(v)}`)
      .join(" ");
    return `<svg class="balance" viewBox="0 0 400 113" preserveAspectRatio="none" role="img" aria-label="Gleitzeitsaldo im Monatsverlauf. Anfang ${fmt(vals[0])}, Ende ${fmt(balance)} Stunden."><line x1="8" x2="392" y1="${y(0)}" y2="${y(0)}" stroke="#a6b8a7" stroke-dasharray="3 4"/><polygon points="8,108 ${xy} 392,108" fill="#d9eac455"/><polyline fill="none" stroke="#37634a" stroke-width="2.5" vector-effect="non-scaling-stroke" points="${xy}"/></svg><div class="chartlabels"><span>Vortrag ${fmt(vals[0], true)} h</span><span>${date(days.at(-1).day)} · ${fmt(balance, true)} h</span></div>`;
  }
  function projectBars() {
    const total = s.stats.reduce((a, x) => a + x.minutes, 0);
    return total
      ? `<div class="bars">${s.stats.map((x) => `<div><div class="row"><span>${esc(x.name)}</span><strong>${fmt(x.minutes)} h</strong></div><div class="track"><i style="width:${(100 * x.minutes) / total}%"></i></div></div>`).join("")}</div>`
      : '<p class="empty">Noch keine Projektstunden gebucht.</p>';
  }
  function timer() {
    if (!own()) return "";
    return `<section class="panel"><div class="row"><h2>Stempeluhr</h2><span class="pill">${s.timer ? "Läuft" : "Gestoppt"}</span></div>${s.timer ? `<p>Seit ${date(s.timer.day)}, ${clock(s.timer.fromMin)} Uhr · ${esc(s.projects.find((p) => p.id === s.timer.projectId)?.name)}</p><div class="actions timer">${btn("stop", "Zeit buchen", "primary")}${btn("cancelTimer", "Verwerfen")}</div>` : `<p class="muted">Start und Ende mit der lokalen Uhrzeit erfassen.</p><div class="timer">${btn("start", "Jetzt einstempeln")}</div>`}</section>`;
  }
  function dashboard() {
    return (
      metrics() +
      `<div class="grid"><section class="panel"><h2>Saldoverlauf</h2>${chart()}</section><section class="panel"><h2>Stunden pro Projekt</h2>${projectBars()}</section></div>${timer()}<section class="panel"><div class="row"><h2>Letzte Buchungen</h2>${btn("tab", "Alle anzeigen", "mini", 'data-id="1"')}</div>${entriesTable([...s.entries].reverse().slice(0, 5), false)}</section>`
    );
  }
  function entriesTable(rows, edit) {
    const can = own() && !s.closed && edit;
    return table(
      [
        "Datum",
        "Projekt",
        "Beginn",
        "Ende",
        "Pause",
        "Netto",
        "Notiz",
        ...(can ? ["Aktionen"] : []),
      ],
      rows.map(
        (e) =>
          `<tr><td>${date(e.day)}</td><td>${esc(e.project)}</td><td>${clock(e.fromMin)}</td><td>${clock(e.toMin)}</td><td>${e.pause} min</td><td><strong>${fmt(e.toMin - e.fromMin - e.pause)}</strong></td><td class="note">${esc(e.note)}</td>${can ? `<td><div class="buttons">${btn("entry", "Ändern", "mini", attr("id", e.id))}${btn("delEntry", "Löschen", "mini danger", attr("id", e.id))}</div></td>` : ""}</tr>`,
      ),
      "Keine Buchungen für diese Auswahl.",
    );
  }
  function entriesPage() {
    const filtered = s.entries
      .filter(
        (e) =>
          (!projectFilter || e.projectId === Number(projectFilter)) &&
          (!filterDay || e.day === filterDay),
      )
      .reverse();
    return `<section class="panel"><div class="row"><h2>Buchungsjournal</h2><span class="pill">${s.closed ? "Abgeschlossen" : "Offen"}</span></div><div class="entryfilters"><label>Projektfilter<select id="filter"><option value="">Alle Projekte</option>${projectOpts(projectFilter)}</select></label><label>Arbeitstag<input type="date" id="filterDay" value="${esc(filterDay)}"></label>${btn("clear", "Filter löschen", "mini")}</div>${entriesTable(filtered, true)}</section>`;
  }
  function absencePage() {
    const rows = s.absences.map((x) => {
      let actions = "";
      if (x.status === "Beantragt" && own())
        actions += btn(
          "decision",
          "Zurückziehen",
          "mini",
          attr("id", x.id) + ' data-status="Zurückgezogen"',
        );
      if (x.status === "Beantragt" && manage())
        for (const [val, label] of [
          ["Bewilligt", "Bewilligen"],
          ["Abgelehnt", "Ablehnen"],
        ])
          actions += btn(
            "decision",
            label,
            "mini",
            attr("id", x.id) + " " + attr("status", val),
          );
      if (x.status === "Bewilligt" && manage())
        actions += btn(
          "decision",
          "Widerrufen",
          "mini",
          attr("id", x.id) + ' data-status="Widerrufen"',
        );
      return `<tr><td>${date(x.von)} – ${date(x.bis)}</td><td>${esc(x.kind)}</td><td><span class="pill">${esc(x.status)}</span></td><td class="note">${esc(x.note)}</td><td><div class="buttons">${actions}</div></td></tr>`;
    });
    return `<section class="panel"><h2>Anträge aller Monate</h2>${table(["Zeitraum", "Art", "Status", "Bemerkung", "Aktionen"], rows, "Bisher wurden keine Abwesenheiten beantragt.")}</section>`;
  }
  function report() {
    const m = s.summary;
    return (
      metrics() +
      `<div class="grid"><section class="panel"><h2>Monatsabrechnung</h2>${pair("Vortrag", fmt(m.carry, true) + " h")}${pair("Arbeitszeit", fmt(m.actual) + " h")}${pair("Bezahlte Abwesenheiten", fmt(m.credit) + " h")}${pair("Sollzeit bis Stichtag", fmt(m.target) + " h")}${pair("Neuer Saldo", fmt(m.balance, true) + " h")}${pair("Sollzeit ganzer Monat", fmt(m.planned) + " h")}</section><section class="panel"><h2>Projektverteilung</h2>${projectBars()}<p class="sectionnote">Stichtag ist heute oder, bei vergangenen Monaten, der letzte Kalendertag.</p><div class="actions"><span class="pill">${s.closed ? "Abgeschlossen" : "Offen"}</span>${manage() ? btn("close", s.closed ? "Wieder öffnen" : "Monat abschliessen", "mini") : ""}</div></section></div><section class="panel"><h2>Tagesauswertung</h2>${table(
        ["Tag", "Abwesenheit", "Soll", "Arbeit", "Gutschrift", "Differenz"],
        s.days.map(
          (d) =>
            `<tr><td>${date(d.day, { weekday: "short", day: "2-digit", month: "2-digit" })}</td><td>${esc(d.kind || "")}</td><td>${fmt(d.soll)}</td><td>${fmt(d.arbeit)}</td><td>${fmt(d.gutschrift)}</td><td>${d.delta === null ? "offen" : fmt(d.delta, true)}</td></tr>`,
        ),
      )}</section><section class="panel"><h2>Letzte Änderungen</h2>${table(
        ["Zeitpunkt", "Person", "Vorgang"],
        s.audit.map(
          (a) =>
            `<tr><td>${esc(a.at)}</td><td>${esc(a.actor)}</td><td class="note">${esc(a.action)}</td></tr>`,
        ),
        "Noch keine Änderungen protokolliert.",
      )}</section>`
    );
  }
  function projectsPage() {
    const projects = projectReport.projekte;
    const rows = projects.map(p => `<tr><td>${esc(p.name)}</td><td>${esc(p.leitung || "Offen")}</td><td>${p.beteiligte.length}</td><td>${fmt(p.minuten)} h</td></tr>`);
    const total = pair("Total Projektstunden", fmt(projectReport.gesamt) + " h");
    const overview = table(["Projekt", "Leitung", "Beteiligte", "Arbeitszeit"], rows, "Für diesen Zugang sind keine Projekte zugewiesen.");
    const details = projects.map(p => {
      const people = p.beteiligte.map(b => `<tr><td>${esc(b.name)}</td><td>${fmt(b.minuten)} h</td></tr>`);
      return `<section class="panel"><h2>${esc(p.name)}</h2>${table(["Person", "Arbeitszeit"], people, "Keine Buchungen im gewählten Monat.")}</section>`;
    });
    return `<section class="panel"><h2>Monatsübersicht</h2>${overview}${total}</section>${details.join("")}`;
  }
  function adminPage() {
    return `<div class="grid"><section class="panel"><h2>Konten</h2>${pair("Ausgewählte Person", s.person.name)}${pair("Rolle", s.person.role)}${pair("Eintritt", date(s.person.start))}${pair("Anfangssaldo", fmt(s.person.opening, true) + " h")}<div class="timer">${btn("account", "Konto anlegen")}</div></section><section class="panel"><h2>Projekte</h2>${s.projects.map((p) => `<div class="kv"><span>${esc(p.name)}<br><small>${esc(s.people.find(person => person.id === p.leadId)?.name || "Ohne Projektleitung")}</small></span>${btn("lead", "Leitung zuweisen", "mini", attr("id",p.id))}</div>`).join("")}<div class="timer">${btn("project", "Projekt anlegen")}</div></section></div><section class="panel"><div class="row"><h2>Wochenpläne · ${esc(s.person.name)}</h2>${btn("schedule", "Pensum festlegen")}</div>${table(
      ["Gültig ab", "Pensum", ...weekdays],
      s.plans.map(
        (p) =>
          `<tr><td>${date(p.since)}</td><td>${Number((p.tage.reduce((a, b) => a + b, 0) / 24).toFixed(1))} %</td>${p.tage.map((m) => `<td>${fmt(m)}</td>`).join("")}</tr>`,
      ),
    )}</section>`;
  }
  const input = (label, name, type = "text", value = "", other = "") =>
    `<label>${esc(label)}<input name="${name}" type="${type}" value="${esc(value)}" ${other}></label>`;
  const field = (title, body) => `<label>${title}${body}</label>`;
  const dropdown = (title, name, options) =>
    field(
      title,
      `<select aria-label="${esc(title)}" name="${name}">${options}</select>`,
    );
  const note = (v) =>
    field(
      "Bemerkung",
      `<textarea name="note" maxlength="300">${esc(v || "")}</textarea>`,
    );
  const intInput = (l, n, v, min, max) =>
    input(l, n, "number", v, `required min="${min}" max="${max}" step="1"`);
  const weekInputs = (arr) =>
    `<div><p class="sectionnote">Tägliche Sollzeit in Minuten. 2400 Minuten entsprechen 100 %.</p><div class="week">${weekdays.map((d, i) => intInput(d, "d" + i, arr[i], 0, 1440)).join("")}</div></div>`;
  const readWeek = (d) => weekdays.map((_, i) => +d["d" + i]);
  function open(title, fields, fn, submit = "Speichern") {
    form.querySelector("h2").textContent = title;
    document.querySelector("#fields").innerHTML =
      `<div class="fields">${fields}</div>`;
    document.querySelector("#formerror").textContent = "";
    document.querySelector("#submit").textContent = submit;
    submitAction = fn;
    dialog.showModal();
  }
  form.onsubmit = async (e) => {
    e.preventDefault();
    const b = document.querySelector("#submit");
    b.disabled = true;
    try {
      const result = await submitAction(Object.fromEntries(new FormData(form)));
      dialog.close();
      if (result !== "logout") await reload();
      tell("Änderung gespeichert.");
    } catch (err) {
      document.querySelector("#formerror").textContent = err.message;
    } finally {
      b.disabled = false;
    }
  };
  const confirm = (title, desc, fn, label) =>
    open(title, `<p>${esc(desc)}</p>`, fn, label);
  function csv() {
    const rows = [
      [
        "Person",
        "Monat",
        "Datum",
        "Abwesenheit",
        "Soll (min)",
        "Arbeit (min)",
        "Gutschrift (min)",
        "Differenz (min)",
        "Saldo (min)",
      ],
    ];
    let balance = s.summary.carry;
    for (const d of s.days) {
      if (d.delta !== null) balance += d.delta;
      rows.push([
        s.person.name,
        s.month,
        d.day,
        d.kind,
        d.soll,
        d.arbeit,
        d.gutschrift,
        d.delta,
        d.delta === null ? "" : balance,
      ]);
    }
    downloadCsv(rows, `Tempus-${s.person.login}-${s.month}.csv`);
  }
  function downloadCsv(rows, filename) {
    const cells = (v) => `"${String(v ?? "").replace(/"/g, '""')}"`;
    const safe = (v) =>
      typeof v === "string" && /^[=+@\t\r-]/.test(v) ? "'" + v : v;
    const blob = new Blob(
      [
        "\ufeff" +
          rows.map((r) => r.map((v) => cells(safe(v))).join(";")).join("\r\n"),
      ],
      { type: "text/csv;charset=utf-8" },
    );
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }
  function hrPage() {
    const rows = hrRows.map(
      (r) =>
        `<tr><td>${esc(r.name)}</td><td>${fmt(r.sum.actual)}</td><td>${fmt(r.sum.target)}</td><td>${fmt(r.sum.credit)}</td><td>${fmt(r.unpaid)}</td><td>${fmt(r.sum.delta, true)}</td><td>${fmt(r.sum.balance, true)}</td><td>${r.closed ? "Abgeschlossen" : "Offen"}</td></tr>`,
    );
    const total = (k) => hrRows.reduce((v, r) => v + r.sum[k], 0);
    return `<section class="panel hr-hours"><div class="row"><h2>Stundenauswertung · alle Mitarbeitenden</h2><div class="actions">${btn("hrCsv", "Stundenliste als CSV")}${btn("printHours", "Liste drucken")}</div></div>${table(["Person", "Arbeit", "Soll", "Gutschrift", "Unbezahlt¹", "Differenz", "Saldo", "Status"], rows)}<p class="sectionnote">Total Arbeitszeit ${fmt(total("actual"))} h · Soll ${fmt(total("target"))} h · Gutschriften ${fmt(total("credit"))} h. ¹ Geplante Stunden während unbezahltem Urlaub und Sabbatical bis zum Stichtag.</p></section><section class="panel hr-slip"><div class="row"><h2>Lohnblatt · ${esc(s.person.name)}</h2><div class="actions">${!slip?.eingabe.definitiv ? btn("editSlip", slip ? "Lohnblatt bearbeiten" : "Lohnblatt erstellen", "primary") : btn("openSlip", "Wieder öffnen")}${slip ? btn("printSlip", "Lohnblatt drucken / PDF") : ""}</div></div>${slip ? slipContent() : `<p class="empty">Für ${esc(s.person.name)} ist im gewählten Monat noch kein Lohnblatt gespeichert.</p>`}</section>`;
  }
  const money = (v) =>
    new Intl.NumberFormat("de-CH", {
      style: "currency",
      currency: "CHF",
    }).format(v);
  function slipContent() {
    const x = slip.eingabe,
      b = slip.betrag;
    return `<div class="payhead"><strong>${esc(x.arbeitgeber)}</strong><span class="pill">${x.definitiv ? "Definitiv" : "Entwurf"}</span></div><p>${esc(slip.person)} · ${esc(slip.monat)} · ${esc(x.modell)}</p><div class="grid paycols"><div>${pair(x.modell === "Stundenlohn" ? "Ansatz pro Stunde" : "Vereinbarter Monatslohn", money(x.ansatz))}${pair("Arbeitszeit inkl. bezahlter Gutschriften", fmt(b.minuten) + " h")}${pair("Grundlohn", money(b.basis))}${pair("Lohnkorrektur", money(x.korrektur))}${pair("Zulagen", money(x.zulagen))}${pair("Bruttolohn", money(b.brutto))}</div><div>${pair("Sozialbeiträge", money(x.sozialbeitraege))}${pair("Pensionskasse", money(x.pensionskasse))}${pair("Versicherung", money(x.versicherung))}${pair("Quellensteuer", money(x.steuer))}${pair("Weitere Abzüge", money(x.weitere))}${pair("Total Abzüge", money(b.abzuege))}</div></div><div class="net">${pair("Auszahlungsbetrag", money(b.netto))}</div>${x.bemerkung ? `<p class="sectionnote">${esc(x.bemerkung)}</p>` : ""}<p class="sectionnote">Stand ${esc(slip.stand)} · ${esc(slip.erstelltVon)}</p>`;
  }
  function editSlip() {
    const x = slip?.eingabe || {};
    const num = (l, k, v = 0, neg = false) =>
      input(
        l,
        k,
        "number",
        x[k] ?? v,
        `required step="0.01" min="${neg ? -1000000 : 0}" max="1000000"`,
      );
    const fields =
      input(
        "Arbeitgeber",
        "arbeitgeber",
        "text",
        x.arbeitgeber || "",
        "required maxlength=100",
      ) +
      dropdown(
        "Lohnmodell",
        "modell",
        opts(
          ["Stundenlohn", "Monatslohn"].map((t) => [t, t]),
          x.modell || "Stundenlohn",
        ),
      ) +
      num("Stundenansatz bzw. vereinbarter Monatslohn in CHF", "ansatz") +
      `<p class="sectionnote">Stundenlohn umfasst gebuchte Arbeit und bezahlte Zeitgutschriften. Beim Monatslohn den vereinbarten Betrag für das tatsächliche Pensum eingeben. Korrekturen für Eintritte oder unbezahlte Abwesenheiten in CHF erfassen.</p><div class="two">${num("Lohnkorrektur (+/−)", "korrektur", 0, true)}${num("Zulagen", "zulagen")}${num("Sozialbeiträge", "sozialbeitraege")}${num("Pensionskasse", "pensionskasse")}${num("Versicherung", "versicherung")}${num("Quellensteuer", "steuer")}${num("Weitere Abzüge", "weitere")}</div>` +
      input(
        "Bemerkung",
        "bemerkung",
        "text",
        x.bemerkung || "",
        "maxlength=300",
      ) +
      dropdown(
        "Speichern als",
        "definitiv",
        opts(
          [
            ["0", "Entwurf"],
            ["1", "Definitives Lohnblatt"],
          ],
          "0",
        ),
      );
    open("Lohnblatt bearbeiten", fields, (d) => {
      for (const k of [
        "ansatz",
        "korrektur",
        "zulagen",
        "sozialbeitraege",
        "pensionskasse",
        "versicherung",
        "steuer",
        "weitere",
      ])
        d[k] = Number(d[k]);
      d.definitiv = d.definitiv === "1";
      return api(`hr/slip/${s.person.id}?month=${s.month}`, "PUT", d);
    });
  }
  function hrCsv() {
    const rows = [
      [
        "Monat",
        "Person",
        "Arbeit min",
        "Soll min",
        "Gutschriften min",
        "Unbezahlt min",
        "Monatsdifferenz min",
        "Saldo min",
        "Monatsstatus",
      ],
      ...hrRows.map((r) => [
        s.month,
        r.name,
        r.sum.actual,
        r.sum.target,
        r.sum.credit,
        r.unpaid,
        r.sum.delta,
        r.sum.balance,
        r.closed ? "Abgeschlossen" : "Offen",
      ]),
    ];
    downloadCsv(rows, `HR-Stunden-${s.month}.csv`);
  }
  function printPart(mode) {
    document.body.dataset.print = mode;
    window.print();
  }

  async function action(a, id, status) {
    const path = "people/" + s.person.id;
    switch (a) {
      case "tab":
        tab = Number(id);
        if (tab === 5 || tab === 6) await reload();
        else render();
        break;
      case "logout":
        await api("logout", "POST");
        login();
        break;
      case "clear":
        projectFilter = "";
        filterDay = "";
        render();
        break;
      case "entry": {
        const e = s.entries.find((row) => row.id === +id);
        const day =
          e?.day ||
          filterDay ||
          (s.today.startsWith(s.month) ? s.today : s.month + "-01");
        open(
          e ? "Buchung ändern" : "Zeit erfassen",
          input("Arbeitstag", "day", "date", day, "required") +
            dropdown("Projekt", "projectId", projectOpts(e?.projectId)) +
            `<div class="two">${input("Beginn", "from", "time", clock(e?.fromMin ?? 480), "required")}${input("Ende (HH:mm)", "to", "text", clock(e?.toMin ?? 720), 'required placeholder="17:00" maxlength="5"')}</div>` +
            intInput("Pause in Minuten", "pause", e?.pause ?? 0, 0, 1439) +
            note(e?.note),
          (d) =>
            api("entries" + (e ? "/" + e.id : ""), e ? "PUT" : "POST", {
              ...d,
              pause: +d.pause,
              projectId: +d.projectId,
            }),
        );
        break;
      }
      case "delEntry":
        confirm(
          "Buchung löschen",
          "Die Arbeitszeit wird aus dem Journal entfernt.",
          () => api("entries/" + id, "DELETE"),
          "Löschen",
        );
        break;
      case "absence":
        open(
          "Abwesenheit beantragen",
          dropdown(
            "Art",
            "kind",
            opts(
              [
                "Ferien",
                "Krankheit",
                "Unbezahlter Urlaub",
                "Sabbatical",
                "Gleitzeitabbau",
              ].map((t) => [t, t]),
            ),
          ) +
            `<div class="two">${input("Von", "von", "date", s.today, "required")}${input("Bis und mit", "bis", "date", s.today, "required")}</div>` +
            note() +
            `<p class="sectionnote">Anträge gelten für ganze Tage. Bewilligte Ferien und Krankheit schreiben die geplante Arbeitszeit gut. Unbezahlter Urlaub und Sabbatical reduzieren das Soll. Gleitzeitabbau belastet den Saldo.</p>`,
          (d) => api("absences", "POST", d),
          "Beantragen",
        );
        break;
      case "decision":
        confirm(
          "Antrag bearbeiten",
          `Neuer Status: ${status}.`,
          () => api("absences/" + id, "PUT", { status }),
          "Bestätigen",
        );
        break;
      case "start":
        open(
          "Einstempeln",
          dropdown("Projekt", "projectId", projectOpts()) + note(),
          (d) => api("timer/start", "POST", { ...d, projectId: +d.projectId }),
          "Starten",
        );
        break;
      case "stop":
        open(
          "Ausstempeln",
          intInput("Pause in Minuten", "pause", 0, 0, 1439),
          (d) => api("timer/stop", "POST", { pause: +d.pause }),
          "Zeit buchen",
        );
        break;
      case "cancelTimer":
        confirm(
          "Stempeluhr verwerfen",
          "Die laufende Zeit wird ohne Buchung beendet.",
          () => api("timer", "DELETE"),
          "Verwerfen",
        );
        break;
      case "close":
        confirm(
          "Monatsabschluss",
          s.closed
            ? "Korrekturen werden für diesen Monat wieder möglich."
            : "Buchungen, Pensen und Abwesenheiten in diesem Monat werden für Änderungen gesperrt.",
          () =>
            api(path + "/close", "POST", { month: s.month, closed: !s.closed }),
          "Bestätigen",
        );
        break;
      case "lead": {
        const project = s.projects.find(p => p.id === Number(id));
        const leads = s.people.filter(p => p.role === "Projektleitung");
        open("Leitung für " + project.name, dropdown("Projektleitung", "personId",
          opts([["", "Ohne Projektleitung"], ...leads.map(p => [p.id, p.name])], project.leadId ?? "")),
          d => api("projects/" + project.id + "/lead", "PUT", {personId: d.personId ? Number(d.personId) : null}));
        break;
      }
      case "project":
        open(
          "Projekt anlegen",
          input("Projektname", "name", "text", "", "required maxlength=80"),
          (d) => api("projects", "POST", d),
        );
        break;
      case "schedule":
        open(
          "Pensum festlegen",
          input("Gültig ab", "since", "date", s.today, "required") +
            weekInputs(
              (s.plans.filter((p) => p.since <= s.today).at(-1) || s.plans[0])
                .tage,
            ),
          (d) =>
            api(path + "/plan", "POST", {
              since: d.since,
              minutes: readWeek(d),
            }),
        );
        break;
      case "account":
        open(
          "Konto anlegen",
          input("Name", "name", "text", "", "required maxlength=80") +
            input(
              "Benutzername",
              "login",
              "text",
              "",
              "required maxlength=40",
            ) +
            input(
              "Anfangspasswort",
              "password",
              "password",
              "",
              "required minlength=10 maxlength=128 autocomplete=new-password",
            ) +
            dropdown(
              "Rolle",
              "role",
              opts(
                ["Mitarbeit", "Projektleitung", "Entwicklungsleitung", "HR", "Administration"].map(
                  (v) => [v, v],
                ),
              ),
            ) +
            input("Eintritt", "start", "date", s.today, "required") +
            intInput("Anfangssaldo in Minuten", "opening", 0, -600000, 600000) +
            weekInputs([480, 480, 480, 480, 480, 0, 0]),
          (d) =>
            api("people", "POST", {
              ...d,
              opening: +d.opening,
              minutes: readWeek(d),
            }),
        );
        break;
      case "password":
        open(
          "Passwort ändern",
          input(
            "Bisheriges Passwort",
            "old",
            "password",
            "",
            "required autocomplete=current-password",
          ) +
            input(
              "Neues Passwort",
              "newPassword",
              "password",
              "",
              "required minlength=10 maxlength=128 autocomplete=new-password",
            ),
          async (d) => {
            await api("password", "POST", d);
            login();
            return "logout";
          },
        );
        break;
      case "editSlip":
        editSlip();
        break;
      case "openSlip":
        confirm(
          "Lohnblatt wieder öffnen",
          "Das gespeicherte Lohnblatt wird als Entwurf bearbeitbar.",
          () =>
            api("hr/slip/" + s.person.id + "/reopen?month=" + s.month, "POST"),
          "Öffnen",
        );
        break;
      case "hrCsv":
        hrCsv();
        break;
      case "printHours":
        printPart("hours");
        break;
      case "printSlip":
        printPart("slip");
        break;
      case "csv":
        csv();
        break;
      case "print":
        printPart("report");
        break;
    }
  }
  document.addEventListener("click", async (e) => {
    const b = e.target.closest("[data-action]");
    if (!b) return;
    const { action: a, id, status } = b.dataset;
    if (a === "dismiss") {
      dialog.close();
      return;
    }
    try {
      await action(a, id, status);
    } catch (err) {
      tell(err.message);
    }
  });
  root.addEventListener("change", async (e) => {
    try {
      switch (e.target.id) {
        case "person":
          person = e.target.value;
          filterDay = "";
          await reload();
          break;
        case "month":
          if (!/^(20\d\d)-(0[1-9]|1[0-2])$/.test(e.target.value)) return;
          month = e.target.value;
          filterDay = "";
          await reload();
          break;
        case "filter":
          projectFilter = e.target.value;
          render();
          break;
        case "filterDay":
          filterDay = e.target.value;
          render();
          break;
      }
    } catch (err) {
      tell(err.message);
    }
  });
  try {
    await reload();
  } catch (err) {
    if (!root.children.length) {
      login();
      tell(err.message);
    }
  }
})();
