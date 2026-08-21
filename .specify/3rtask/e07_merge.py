"""E07 merge: mechanical set-ops over (subject, tag). No judgment. Curator artifact."""
import json, sys, collections
RD = sys.argv[1]
EXCL = {"merge-ready","needs-rebase","needs-completion","do-not-merge","abandon","already-contained"}
claims=[]
for b in (1,2,3):
    d=json.load(open(f"{RD}/cycle01/claims-builder-{b}.json",encoding="utf-8"))
    for c in d["claims"]:
        if c.get("abstain") or not c.get("tag"): continue
        claims.append({"subject":c["subject"].strip(),"tag":c["tag"].strip(),
                       "builder":f"builder-{b}","cite":(c.get("source_citation") or "")[:220],
                       "rationale":(c.get("rationale") or "")[:220],
                       "next_action":(c.get("next_action") or "")[:160],
                       "confidence":c.get("confidence","")})
pair=collections.defaultdict(list)
subj=collections.defaultdict(set)
subj_by_builder=collections.defaultdict(lambda: collections.defaultdict(set))
for c in claims:
    pair[(c["subject"],c["tag"])].append(c)
    subj[c["subject"]].add(c["tag"])
    subj_by_builder[c["subject"]][c["builder"]].add(c["tag"])
rows=[]
for (s,t),cs in sorted(pair.items()):
    bs=sorted({c["builder"] for c in cs})
    rows.append({"subject":s,"tag":t,"builders":bs,
                 "status":"corroborated" if len(bs)>=2 else "singleton",
                 "citations":[c["cite"] for c in cs][:3],
                 "next_action":cs[0]["next_action"]})
conflicts=[]
for s,tags in sorted(subj.items()):
    ex=tags & EXCL
    if len(ex)>1:
        conflicts.append({"subject":s,"kind":"CONFLICT-CROSS","tags":sorted(ex),
                          "by_builder":{b:sorted(v&EXCL) for b,v in subj_by_builder[s].items() if v&EXCL}})
for s,per in sorted(subj_by_builder.items()):
    for b,tags in per.items():
        ex=tags & EXCL
        if len(ex)>1:
            conflicts.append({"subject":s,"kind":"CONFLICT-SELF","builder":b,"tags":sorted(ex)})
out={"cycle":1,"total_claims":len(claims),"pairs":len(rows),
     "corroborated":sum(1 for r in rows if r["status"]=="corroborated"),
     "singletons":sum(1 for r in rows if r["status"]=="singleton"),
     "subjects":len(subj),"conflicts":conflicts,"rows":rows}
json.dump(out,open(f"{RD}/e07-merge.json","w"),indent=1)
print(json.dumps({k:v for k,v in out.items() if k not in("rows","conflicts")}))
print("conflicts:",len(conflicts))
for c in conflicts[:25]: print("  ",c["kind"],c["subject"],c["tags"])
