# Generates csharp/TODO.artifact.html (body-only HTML fragment) from csharp/TODO.md (open work) and
# csharp/DONE.md (finished work), merged per phase heading.
# Stdlib only. Republish with the artifact tool at the SAME path for a stable URL.
# Grammar of TODO.md:
#   ## Phase heading
#   - [x] desc | status=done|progress|todo|deferred | evidence=... | link=...
#   - [ ] desc ...   (status inferred from the checkbox when not given)
import json
import re
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))  # .../csharp
SRC = os.path.join(ROOT, "TODO.md")
DONE = os.path.join(ROOT, "DONE.md")
OUT = os.path.join(ROOT, "TODO.artifact.html")

STATUS = {
    # key: (label, glyph, color)  -- glyph+label are the secondary encoding (dataviz)
    "done":     ("Done",            "\u2713", "#1a7f37"),
    "progress": ("In progress",     "\u25D0", "#b45309"),
    "todo":     ("Not started",     "\u25CB", "#57606a"),
    "deferred": ("Deferred boundary","\u23F8", "#0969da"),
}

def esc(s):
    return (s.replace("&", "&amp;").replace("<", "&lt;")
             .replace(">", "&gt;").replace('"', "&quot;"))

def parse(md):
    phases = []
    cur = None
    for raw in md.splitlines():
        line = raw.rstrip("\n")
        if line.startswith("## "):
            cur = {"title": line[3:].strip(), "tasks": []}
            phases.append(cur)
            continue
        m = re.match(r"^\s*-\s*\[([ xX])\]\s*(.*)$", line)
        if not m or cur is None:
            continue
        checked = m.group(1).lower() == "x"
        body = m.group(2)
        parts = [p.strip() for p in body.split("|")]
        desc = parts[0]
        fields = {}
        for p in parts[1:]:
            if "=" in p:
                k, v = p.split("=", 1)
                fields[k.strip()] = v.strip()
        status = fields.get("status")
        if status not in STATUS:
            status = "done" if checked else "todo"
        cur["tasks"].append({
            "desc": desc,
            "status": status,
            "evidence": fields.get("evidence", ""),
            "link": fields.get("link", ""),
        })
    return phases

def main():
    if not os.path.exists(SRC):
        sys.stderr.write("missing %s\n" % SRC)
        return 1
    with open(SRC, "r", encoding="utf-8") as f:
        phases = parse(f.read())
    if os.path.exists(DONE):
        with open(DONE, "r", encoding="utf-8") as f:
            done = parse(f.read())
        # one phase per heading, finished tasks first; phases in number order, the unnumbered ones last
        byname = {}
        for ph in done + phases:
            if ph["title"] in byname:
                byname[ph["title"]]["tasks"] += ph["tasks"]
            else:
                byname[ph["title"]] = {"title": ph["title"], "tasks": list(ph["tasks"])}
        def order(title):
            m = re.match(r"Phase (\d+)", title)
            return int(m.group(1)) if m else 1 << 30
        phases = sorted(byname.values(), key=lambda ph: order(ph["title"]))

    totals = {k: 0 for k in STATUS}
    for ph in phases:
        for t in ph["tasks"]:
            totals[t["status"]] += 1
    grand = sum(totals.values())

    data = {
        "phases": phases,
        "status": {k: {"label": v[0], "glyph": v[1], "color": v[2]} for k, v in STATUS.items()},
        "totals": totals,
        "grand": grand,
    }
    payload = json.dumps(data, ensure_ascii=False)

    html = FRAG.replace("__DATA__", payload)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(html)
    done = totals["done"]
    print("wrote %s  (%d phases, %d tasks, %d done)" % (OUT, len(phases), grand, done))
    return 0

FRAG = r"""
<style>
  :root{
    --bg:#ffffff;--fg:#1f2328;--muted:#57606a;--line:#d0d7de;--card:#f6f8fa;--accent:#0969da;
  }
  @media (prefers-color-scheme: dark){
    :root{--bg:#0d1117;--fg:#e6edf3;--muted:#9198a1;--line:#30363d;--card:#161b22;--accent:#4493f8;}
  }
  .wrap{max-width:1080px;margin:0 auto;padding:28px 20px 72px;background:var(--bg);color:var(--fg);
        font:15px/1.6 -apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif}
  .wrap *{box-sizing:border-box}
  h1{font-size:26px;margin:0 0 2px}
  .sub{color:var(--muted);font-size:13.5px;margin:0 0 18px}
  .sub code{background:var(--card);border:1px solid var(--line);border-radius:5px;padding:1px 6px;font:12.5px ui-monospace,Consolas,monospace}
  .cards{display:grid;grid-template-columns:repeat(4,1fr);gap:10px;margin:0 0 6px}
  .stat{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:12px 14px}
  .stat .n{font-size:24px;font-weight:700;line-height:1.1}
  .stat .l{font-size:12px;color:var(--muted);display:flex;align-items:center;gap:6px}
  .bar{height:10px;border-radius:6px;background:var(--card);border:1px solid var(--line);overflow:hidden;display:flex;margin:6px 0 18px}
  .bar>i{display:block;height:100%}
  .dot{width:9px;height:9px;border-radius:50%;display:inline-block;flex:0 0 auto}
  .filters{display:flex;gap:8px;flex-wrap:wrap;margin:0 0 18px}
  .filters button{font:inherit;font-size:12.5px;cursor:pointer;color:var(--fg);background:var(--card);
        border:1px solid var(--line);border-radius:999px;padding:5px 12px;display:flex;align-items:center;gap:7px}
  .filters button[aria-pressed="true"]{border-color:var(--accent);box-shadow:0 0 0 1px var(--accent) inset}
  .phase{border:1px solid var(--line);border-radius:12px;margin:0 0 12px;overflow:hidden}
  .phase>header{width:100%;text-align:left;background:var(--card);border:0;border-bottom:1px solid var(--line);
        padding:12px 16px;cursor:pointer;display:flex;align-items:center;gap:12px;color:var(--fg);font:inherit}
  .phase>header h2{font-size:15.5px;margin:0;flex:1 1 auto}
  .phase .pbar{width:120px;height:8px;border-radius:5px;background:var(--bg);border:1px solid var(--line);overflow:hidden;display:flex;flex:0 0 auto}
  .phase .pbar>i{height:100%;display:block}
  .phase .pct{font-size:12px;color:var(--muted);flex:0 0 74px;text-align:right}
  .chev{flex:0 0 auto;color:var(--muted);transition:transform .15s}
  .phase.collapsed .chev{transform:rotate(-90deg)}
  .phase.collapsed ul{display:none}
  ul.tasks{list-style:none;margin:0;padding:6px 0}
  li.task{display:flex;gap:10px;align-items:flex-start;padding:8px 16px;border-top:1px solid var(--line)}
  li.task:first-child{border-top:0}
  .badge{flex:0 0 auto;width:22px;height:22px;border-radius:6px;display:flex;align-items:center;justify-content:center;
        color:#fff;font-size:12px;margin-top:1px}
  .tmain{flex:1 1 auto;min-width:0}
  .tdesc{color:var(--fg)}
  .tdesc .link{color:var(--accent);font:12px ui-monospace,Consolas,monospace;text-decoration:none;word-break:break-all}
  .tdesc .link:hover{text-decoration:underline}
  .ev{display:inline-block;margin-top:3px;font-size:12px;color:var(--muted)}
  .ev::before{content:"\25B8  ";color:var(--muted)}
  .foot{color:var(--muted);font-size:12px;margin-top:20px;border-top:1px solid var(--line);padding-top:12px}
  @media(max-width:640px){.cards{grid-template-columns:repeat(2,1fr)}}
</style>
<div class="wrap">
  <h1>LandSandBoat &#8594; C# Port &#8212; Progress</h1>
  <p class="sub">Phased migration of the FFXI emulator (C++ engine + Lua content + MariaDB) to .NET.
     Source of truth: <code>csharp/TODO.md</code> + <code>csharp/DONE.md</code> &#183; regenerate:
     <code>py -3 csharp\tools\gen_todo_page.py</code>.</p>
  <div class="cards" id="cards"></div>
  <div class="bar" id="obar" title="overall completion"></div>
  <div class="filters" id="filters" role="group" aria-label="filter by status"></div>
  <div id="phases"></div>
  <p class="foot" id="foot"></p>
</div>
<script>
const DATA = __DATA__;
const ORDER = ["done","progress","deferred","todo"];
const byKey = k => DATA.status[k];

function barHtml(counts, total){
  if(total<=0) return "";
  return ORDER.map(k=>{
    const n=counts[k]||0; if(!n) return "";
    const w=(n/total*100).toFixed(4);
    return '<i style="width:'+w+'%;background:'+byKey(k).color+'" title="'+byKey(k).label+': '+n+'"></i>';
  }).join("");
}

function render(){
  const t=DATA.totals, grand=DATA.grand, done=t.done||0;
  document.getElementById("cards").innerHTML = ORDER.map(k=>{
    const s=byKey(k), n=t[k]||0;
    return '<div class="stat"><div class="n">'+n+'</div>'+
      '<div class="l"><span class="dot" style="background:'+s.color+'"></span>'+s.label+'</div></div>';
  }).join("");
  document.getElementById("obar").innerHTML = barHtml(t, grand);
  document.getElementById("obar").title = done+"/"+grand+" tasks done";
  document.getElementById("foot").textContent =
    "Overall: "+done+" of "+grand+" tasks done across "+DATA.phases.length+" phases. "+
    "Colors are always paired with a glyph and label (accessible encoding).";

  document.getElementById("filters").innerHTML =
    '<button data-f="all" aria-pressed="true">All</button>'+
    ORDER.map(k=>'<button data-f="'+k+'" aria-pressed="false"><span class="dot" style="background:'+byKey(k).color+'"></span>'+byKey(k).label+'</button>').join("");

  document.getElementById("phases").innerHTML = DATA.phases.map((ph,i)=>{
    const counts={}; ph.tasks.forEach(x=>counts[x.status]=(counts[x.status]||0)+1);
    const tot=ph.tasks.length, dn=counts.done||0;
    return '<section class="phase" data-i="'+i+'">'+
      '<header><span class="chev">\u25BE</span><h2>'+esc(ph.title)+'</h2>'+
      '<div class="pbar">'+barHtml(counts,tot)+'</div>'+
      '<span class="pct">'+dn+"/"+tot+' done</span></header>'+
      '<ul class="tasks">'+ph.tasks.map(x=>{
        const s=byKey(x.status);
        const link=x.link?'<div><a class="link" href="#" onclick="return false">'+esc(x.link)+'</a></div>':'';
        const ev=x.evidence?'<span class="ev">'+esc(x.evidence)+'</span>':'';
        return '<li class="task" data-status="'+x.status+'">'+
          '<span class="badge" style="background:'+s.color+'" title="'+s.label+'" aria-label="'+s.label+'">'+s.glyph+'</span>'+
          '<div class="tmain"><div class="tdesc">'+esc(x.desc)+link+ev+'</div></div></li>';
      }).join("")+'</ul></section>';
  }).join("");
}

function esc(s){return s.replace(/&/g,"&amp;").replace(/</g,"&lt;").replace(/>/g,"&gt;");}

let activeFilter="all";
document.addEventListener("click",(e)=>{
  const fb=e.target.closest("#filters button");
  if(fb){
    activeFilter=fb.getAttribute("data-f");
    document.querySelectorAll("#filters button").forEach(b=>b.setAttribute("aria-pressed", b===fb?"true":"false"));
    document.querySelectorAll("li.task").forEach(li=>{
      li.style.display=(activeFilter==="all"||li.getAttribute("data-status")===activeFilter)?"":"none";
    });
    return;
  }
  const hd=e.target.closest(".phase>header");
  if(hd){ hd.parentElement.classList.toggle("collapsed"); }
});

render();
</script>
"""

if __name__ == "__main__":
    sys.exit(main())
