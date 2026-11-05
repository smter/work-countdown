
/* ============================================================
   原型脚本：只负责「把状态演出来」，没有任何真实业务逻辑。
   ?variant=A|B|C   ?scene=eod|lunch   ?acc=blue|red|amber|teal
   ============================================================ */
(function () {
  var WINDOW_MS = 10 * 60 * 1000;

  var VARIANTS = [
    { key: "A", name: "海报", full: "A · 海报式", cap: "上下两条斜纹横幅夹住中央巨号读数；扫描线 + 四角括号。" },
    { key: "B", name: "控制台", full: "B · 控制台", cap: "左侧竖排斜纹轨 + 逐位数字格 + 三列遥测读数。" },
    { key: "C", name: "贴纸字", full: "C · 贴纸字", cap: "满幅斜纹铺底，标签与读数做成贴在斜纹上的黑色标牌。" }
  ];

  var ACCENTS = [
    { key: "blue",  name: "经典·午夜蓝", hex: "#4c8dff" },
    { key: "red",   name: "红·枫叶",     hex: "#d61d2f" },
    { key: "amber", name: "黄·琥珀",     hex: "#f8b713" },
    { key: "teal",  name: "经典·极光青", hex: "#2fd4c4" }
  ];

  var SCENES = {
    eod:   { label: "距离下班", en: "SHIFT END", en2: "STRATEGY START", target: "目标 18:00", targetShort: "18:00", stripRight: "下班 18:00", warn: "警告 · 终了接近" },
    lunch: { label: "距离午休", en: "LUNCH BREAK", en2: "LUNCH BREAK", target: "目标 11:50", targetShort: "11:50", stripRight: "午休 11:50", warn: "警告 · 午休接近" }
  };

  var params = new URLSearchParams(location.search);
  var state = {
    variant: (params.get("variant") || "A").toUpperCase(),
    scene: params.get("scene") === "lunch" ? "lunch" : "eod",
    accent: params.get("acc") || "blue",
    scale: Number(params.get("scale")) || 2,
    base: WINDOW_MS,
    initialBase: WINDOW_MS,
    t0: performance.now()
  };
  if (params.has("t")) {
    var seek = Math.max(0, Number(params.get("t")) * 1000);
    state.base = seek;
    state.initialBase = seek;
  }
  if (!VARIANTS.some(function (v) { return v.key === state.variant; })) state.variant = "A";
  if (!ACCENTS.some(function (a) { return a.key === state.accent; })) state.accent = "blue";

  if (params.get("anim") === "0") document.documentElement.classList.add("no-anim");

  var $ = function (sel, root) { return (root || document).querySelector(sel); };
  var $$ = function (sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); };

  var card = $("#card");
  var capEl = $("#cap");
  var els = {
    mmss: $$(".js-mmss"),
    cs: $$(".js-cs"),
    label: $$(".js-label"),
    target: $$(".js-target"),
    targettime: $$(".js-targettime"),
    status: $$(".js-status"),
    tminus: $$(".js-tminus"),
    pct: $$(".js-pct"),
    remain: $$(".js-remain"),
    prog: $$(".js-prog"),
    strip: $(".js-strip"),
    progmeter: []
  };
  var cells = $$(".vB__cells .cell.d");

  function pad2(n) { return n < 10 ? "0" + n : "" + n; }

  function fmt(ms, withCs) {
    if (ms < 0) ms = 0;
    var cs = Math.floor(ms / 10) % 100;
    var s = Math.floor(ms / 1000);
    var mm = Math.floor(s / 60) % 100;
    var ss = s % 60;
    var out = pad2(mm) + ":" + pad2(ss);
    return withCs ? out + "." + pad2(cs) : out;
  }

  function fmtHMS(ms) {
    var s = Math.floor(Math.max(0, ms) / 1000);
    return pad2(Math.floor(s / 3600)) + ":" + pad2(Math.floor(s / 60) % 60) + ":" + pad2(s % 60);
  }

  /* ---------- 外观开关 ---------- */
  function applyVariant() {
    var v = VARIANTS.filter(function (x) { return x.key === state.variant; })[0];
    card.setAttribute("data-variant", v.key);
    $$(".v", card).forEach(function (n) { n.style.display = n.getAttribute("data-for") === v.key ? "" : "none"; });
    $("#vkey").textContent = v.key;
    $("#vname").textContent = v.name;
    capEl.textContent = v.cap + "　｜　280 × 186 DIP（150% 屏 = 420 × 279 物理像素）";
    var url = new URL(location.href);
    url.searchParams.set("variant", v.key);
    history.replaceState(null, "", url);
    // C 的条纹字与 B 的轨条各自有独立节奏，切回来时重启动画
    $$(".haz", card).forEach(function (n) { n.style.animation = "none"; void n.offsetWidth; n.style.animation = ""; });
  }

  function applyScene() {
    var s = SCENES[state.scene];
    els.label.forEach(function (n) { n.textContent = s.label; });
    els.target.forEach(function (n) { n.textContent = s.target; });
    els.targettime.forEach(function (n) { n.textContent = s.targetShort; });
    $$(".vA__en").forEach(function (n) { n.textContent = s.en; });
    $$(".vC__en:not(.vC__en--dim)").forEach(function (n) { n.textContent = s.en2; });
    $$(".js-warn").forEach(function (n) { n.textContent = s.warn; });
    var right = $(".strip__right");
    if (right) right.textContent = s.stripRight;
    $$("[data-scene]").forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-scene") === state.scene); });
  }

  function applyAccent() {
    var a = ACCENTS.filter(function (x) { return x.key === state.accent; })[0];
    var rgb = [1, 3, 5].map(function (i) { return parseInt(a.hex.substr(i, 2), 16); });
    document.documentElement.style.setProperty("--acc", a.hex);
    document.documentElement.style.setProperty("--acc-rgb", rgb.join(", "));
    $$("#accents button").forEach(function (b) { b.classList.toggle("on", b.getAttribute("data-acc") === state.accent); });
  }

  function applyScale() {
    document.documentElement.style.setProperty("--scale", state.scale);
    $$("[data-scale]").forEach(function (b) { b.classList.toggle("on", Number(b.getAttribute("data-scale")) === state.scale); });
    $("#slot").style.width = 280 * state.scale + "px";
    $("#slot").style.height = 186 * state.scale + "px";
  }

  /* ---------- 每帧 ---------- */
  var lastDone = false;
  var lastDigits = {};

  function render(now) {
    var remaining = Math.max(0, state.base - (now - state.t0));
    var secs = remaining / 1000;

    var mmss = fmt(remaining, false);
    var cs = "." + pad2(Math.floor(remaining / 10) % 100);

    els.mmss.forEach(function (n) { if (n.textContent !== mmss) n.textContent = mmss; });
    els.cs.forEach(function (n) { if (n.textContent !== cs) n.textContent = cs; });
    els.tminus.forEach(function (n) { n.textContent = "T-" + mmss; });
    els.status.forEach(function (n) { n.textContent = "T-MINUS " + mmss + cs; });
    els.remain.forEach(function (n) { n.textContent = fmt(remaining, false); });
    if (els.strip) els.strip.textContent = fmtHMS(remaining);

    var pct = Math.min(1, 1 - remaining / WINDOW_MS);
    els.prog.forEach(function (n) { n.style.width = (pct * 100).toFixed(2) + "%"; });
    els.pct.forEach(function (n) { n.textContent = Math.round(pct * 100) + "%"; });

    // B：逐位数字，变化的位闪一下
    var digits = mmss.replace(":", "") + pad2(Math.floor(remaining / 10) % 100);
    for (var i = 0; i < cells.length && i < digits.length; i++) {
      var c = cells[i];
      if (c.textContent !== digits[i]) {
        c.textContent = digits[i];
        c.classList.remove("pop"); void c.offsetWidth; c.classList.add("pop");
      }
    }

    card.classList.toggle("is-hot", secs <= 60 && remaining > 0);
    card.classList.toggle("is-crit", secs <= 10 && remaining > 0);
    var done = remaining <= 0;
    if (done !== lastDone) { card.classList.toggle("is-done", done); lastDone = done; }
  }

  function loop(now) { render(now); requestAnimationFrame(loop); }

  /* ---------- 展开演示 ---------- */
  var timers = [];
  function clearTimers() { timers.forEach(clearTimeout); timers = []; }

  function replay(instant) {
    clearTimers();
    card.classList.remove("is-entering", "is-done");
    lastDone = false;
    state.base = state.initialBase;
    state.t0 = performance.now();
    if (instant) return;
    card.classList.add("is-collapsed");
    timers.push(setTimeout(function () {
      card.classList.remove("is-collapsed");
      // 下一帧再上入场类，确保 width/height 过渡和内容入场能同时看到
      requestAnimationFrame(function () {
        card.classList.add("is-entering");
        timers.push(setTimeout(function () { card.classList.remove("is-entering"); }, 1500));
      });
    }, 420));
  }

  /* ---------- 工具条 ---------- */
  function wire() {
    var accentsBox = $("#accents");
    ACCENTS.forEach(function (a) {
      var b = document.createElement("button");
      b.setAttribute("data-acc", a.key);
      b.title = a.name;
      b.style.background = a.hex;
      b.addEventListener("click", function () {
        state.accent = a.key;
        var url = new URL(location.href); url.searchParams.set("acc", a.key); history.replaceState(null, "", url);
        applyAccent();
      });
      accentsBox.appendChild(b);
    });

    $("#prev").addEventListener("click", function () { cycle(-1); });
    $("#next").addEventListener("click", function () { cycle(1); });

    $$("[data-scene]").forEach(function (b) {
      b.addEventListener("click", function () {
        state.scene = b.getAttribute("data-scene");
        var url = new URL(location.href); url.searchParams.set("scene", state.scene); history.replaceState(null, "", url);
        applyScene();
      });
    });

    $$("[data-scale]").forEach(function (b) {
      b.addEventListener("click", function () {
        state.scale = Number(b.getAttribute("data-scale"));
        var url = new URL(location.href); url.searchParams.set("scale", state.scale); history.replaceState(null, "", url);
        applyScale();
      });
    });

    $("#skip").addEventListener("click", function () {
      clearTimers();
      card.classList.remove("is-done");
      lastDone = false;
      state.base = 30000;
      state.t0 = performance.now();
    });

    $("#replay").addEventListener("click", function () { replay(false); });

    document.addEventListener("keydown", function (e) {
      var tag = (document.activeElement && document.activeElement.tagName) || "";
      if (/INPUT|TEXTAREA|SELECT/.test(tag)) return;
      if (e.key === "ArrowLeft") { cycle(-1); e.preventDefault(); }
      if (e.key === "ArrowRight") { cycle(1); e.preventDefault(); }
      if (e.key === "r" || e.key === "R") { replay(false); e.preventDefault(); }
    });
  }

  function cycle(dir) {
    var i = VARIANTS.map(function (v) { return v.key; }).indexOf(state.variant);
    state.variant = VARIANTS[(i + dir + VARIANTS.length) % VARIANTS.length].key;
    applyVariant();
  }

  /* ---------- 启动 ---------- */
  applyAccent();
  applyScale();
  applyVariant();
  applyScene();
  wire();
  replay(false);
  render(performance.now());
  requestAnimationFrame(loop);
})();
